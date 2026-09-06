using System.Diagnostics;
using System.Globalization;
using FormatConverter.Core.Ffmpeg;

namespace FormatConverter.Core.Tools;

/// <summary>
/// 视频/音频工具(ffmpeg):剪辑、抽帧序列、缩略图拼图、转 GIF(带参数)、音量与淡入淡出。
/// 所有方法在 ffmpeg 不可用时抛 InvalidOperationException。
/// </summary>
public static class VideoTools
{
    private static readonly FfmpegRunner Runner = new();

    public static bool IsAvailable => FfmpegLocator.IsAvailable;

    // ---------- 剪辑 ----------

    /// <summary>剪辑片段(秒级,start/end 可为 null;流复制,速度快)。</summary>
    public static async Task<string> TrimAsync(
        string input, string output, double? startSeconds, double? endSeconds,
        CancellationToken ct = default)
    {
        var args = new List<string>();
        if (startSeconds is double s)
        {
            args.Add("-ss");
            args.Add(FormatSeconds(s));
        }
        if (endSeconds is double e)
        {
            args.Add("-to");
            args.Add(FormatSeconds(e));
        }
        args.Add("-i");
        args.Add(input);
        args.Add("-c");
        args.Add("copy");
        args.Add("-f");
        args.Add(FfmpegArgsBuilder.GetMuxer(Path.GetExtension(output).TrimStart('.')));
        args.Add(FfmpegRunner.BuildPartPath(output));

        var result = await Runner.RunAsync(args, output, null, null, ct);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorOutput ?? "剪辑失败。");
        return output;
    }

    // ---------- 抽帧 ----------

    /// <summary>按帧率抽取帧序列到目录,返回按文件名排序的输出图片路径。</summary>
    public static async Task<IReadOnlyList<string>> ExtractFramesAsync(
        string input, string outputDir, double fps, CancellationToken ct = default)
    {
        EnsureAvailable();
        Directory.CreateDirectory(outputDir);
        var pattern = Path.Combine(outputDir, "frame_%04d.jpg");

        await RunSimpleAsync(new[]
        {
            "-i", input,
            "-vf", $"fps={fps.ToString(CultureInfo.InvariantCulture)}",
            "-q:v", "2",
            "-f", "image2",
            pattern,
        }, ct);

        return Directory.EnumerateFiles(outputDir, "frame_*.jpg")
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    // ---------- 缩略图拼图 ----------

    /// <summary>按固定间隔抽帧并拼成 cols×rows 缩略图。</summary>
    public static async Task<string> MakeThumbnailAsync(
        string input, string output, int cols, int rows, double intervalSeconds,
        CancellationToken ct = default)
    {
        EnsureAvailable();
        var args = new List<string>
        {
            "-i", input,
            "-vf", $"fps=1/{intervalSeconds.ToString(CultureInfo.InvariantCulture)},scale=320:-1,tile={cols}x{rows}",
            "-frames:v", "1",
            "-f", "image2",
            FfmpegRunner.BuildPartPath(output),
        };

        var result = await Runner.RunAsync(args, output, null, null, ct);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorOutput ?? "缩略图生成失败。");
        return output;
    }

    // ---------- 转 GIF(带参数) ----------

    /// <summary>视频转 GIF,可指定宽度/fps/起止时间;与格式互转共用同一调色板策略。</summary>
    public static async Task<string> GifAsync(
        string input, string output, int width, int fps,
        double? startSeconds, double? durationSeconds, CancellationToken ct = default)
    {
        EnsureAvailable();
        var args = new List<string>();
        if (startSeconds is double s)
        {
            args.Add("-ss");
            args.Add(FormatSeconds(s));
        }
        if (durationSeconds is double d)
        {
            args.Add("-t");
            args.Add(FormatSeconds(d));
        }
        args.Add("-i");
        args.Add(input);
        args.Add("-filter_complex");
        args.Add($"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse=dither=bayer");
        args.Add("-loop");
        args.Add("0");
        args.Add("-f");
        args.Add("gif");
        args.Add(FfmpegRunner.BuildPartPath(output));

        var result = await Runner.RunAsync(args, output, null, null, ct);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorOutput ?? "GIF 生成失败。");
        return output;
    }

    // ---------- 音频增强 ----------

    /// <summary>音量调节(1.0 = 原音量),可选淡入/淡出(秒)。</summary>
    public static async Task<string> AdjustVolumeAsync(
        string input, string output, double volume,
        double? fadeInSeconds, double? fadeOutSeconds, CancellationToken ct = default)
    {
        EnsureAvailable();
        var filters = new List<string>
        {
            $"volume={volume.ToString(CultureInfo.InvariantCulture)}",
        };

        if (fadeInSeconds is double fi && fi > 0)
            filters.Add($"afade=t=in:st=0:d={fi.ToString(CultureInfo.InvariantCulture)}");

        if (fadeOutSeconds is double fo && fo > 0)
        {
            var probe = await FfmpegProbe.ProbeAsync(input, ct);
            var duration = probe?.DurationSeconds ?? 0;
            if (duration > fo)
            {
                var start = duration - fo;
                filters.Add($"afade=t=out:st={start.ToString(CultureInfo.InvariantCulture)}:d={fo.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        var args = new List<string>
        {
            "-i", input,
            "-af", string.Join(",", filters),
            "-c:a", GetAudioCodec(output),
            "-f", FfmpegArgsBuilder.GetMuxer(Path.GetExtension(output).TrimStart('.')),
            FfmpegRunner.BuildPartPath(output),
        };

        var result = await Runner.RunAsync(args, output, null, null, ct);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorOutput ?? "音频处理失败。");
        return output;
    }

    // ---------- 内部 ----------

    private static void EnsureAvailable()
    {
        if (!FfmpegLocator.IsAvailable)
            throw new InvalidOperationException("未检测到 ffmpeg,无法使用视频/音频工具。");
    }

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetAudioCodec(string outputPath)
    {
        var ext = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "mp3" => "libmp3lame",
            "m4a" or "aac" => "aac",
            "ogg" => "libvorbis",
            "flac" => "flac",
            "wav" => "pcm_s16le",
            _ => "aac",
        };
    }

    /// <summary>直接运行 ffmpeg(无进度解析,无 .part 隔离),用于输出到目录的序列类任务。</summary>
    private static async Task RunSimpleAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        EnsureAvailable();
        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 ffmpeg。");

        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            await process.WaitForExitAsync();
            throw;
        }

        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr)
                ? "ffmpeg 执行失败(无错误详情)。"
                : string.Join('\n', stderr.Trim().Split('\n').TakeLast(5)).Trim();
            throw new InvalidOperationException(msg);
        }
    }
}
