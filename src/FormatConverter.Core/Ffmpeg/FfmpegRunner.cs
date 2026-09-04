using System.Diagnostics;
using System.Text;

namespace FormatConverter.Core.Ffmpeg;

/// <summary>ffmpeg 进程运行时的进度更新。</summary>
public sealed record FfmpegProgress(
    double? Percent,
    double? SecondsDone,
    double? TotalSeconds,
    string? Speed,
    bool Finished);

public sealed record FfmpegRunResult(bool Success, int ExitCode, string? ErrorOutput, string? FinalOutputPath);

/// <summary>
/// ffmpeg 子进程封装:参数传递、进度解析(-progress pipe:1)、取消、半成品 .part 隔离。
/// </summary>
public sealed class FfmpegRunner
{
    /// <summary>
    /// 运行 ffmpeg。输出先写 .part 临时文件,成功后原子改名为最终文件;失败/取消则清理临时文件。
    /// </summary>
    public async Task<FfmpegRunResult> RunAsync(
        IReadOnlyList<string> args,
        string finalOutputPath,
        double? totalDurationSeconds,
        IProgress<FfmpegProgress>? progress,
        CancellationToken ct)
    {
        // ffmpeg 实际写入的路径 = 参数最后一项(调用方用 BuildPartPath 生成,与 finalOutputPath 同目录)
        if (args.Count == 0)
            throw new ArgumentException("参数列表为空。", nameof(args));
        var partPath = args[^1];

        // 全局选项插在输入之后
        var fullArgs = new List<string>(args.Count + 10) { args[0], args[1] };
        fullArgs.AddRange(
        [
            "-progress", "pipe:1",
            "-nostats",
            "-loglevel", "error",
            "-hide_banner",
            "-y",
        ]);
        fullArgs.AddRange(args.Skip(2));

        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in fullArgs)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"无法启动 ffmpeg: {psi.FileName}");

        // stderr 不带 ct 读取:取消时先杀进程,让读取自然结束
        var stderrTask = process.StandardError.ReadToEndAsync();

        var state = new FfmpegProgressState();
        var cancelled = false;

        try
        {
            while (!cancelled && await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                FfmpegProgressParser.OnLine(state, line);
                progress?.Report(new FfmpegProgress(
                    FfmpegProgressParser.ToPercent(state.MaxOutputTimeUs, totalDurationSeconds),
                    state.MaxOutputTimeUs > 0 ? state.MaxOutputTimeUs / 1_000_000.0 : null,
                    totalDurationSeconds,
                    state.Speed,
                    state.Finished));
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        if (cancelled)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* 进程可能已退出 */ }
            await process.WaitForExitAsync();
        }

        var stderr = await stderrTask;

        if (cancelled)
        {
            TryDelete(partPath);
            return new FfmpegRunResult(false, -1, "已取消", null);
        }

        if (process.ExitCode != 0)
        {
            TryDelete(partPath);
            return new FfmpegRunResult(false, process.ExitCode, TrimError(stderr), null);
        }

        // 成功:原子改名(同目录 rename)
        try
        {
            File.Move(partPath, finalOutputPath, overwrite: true);
        }
        catch (Exception ex)
        {
            TryDelete(partPath);
            return new FfmpegRunResult(false, process.ExitCode, $"写入最终文件失败: {ex.Message}", null);
        }

        return new FfmpegRunResult(true, process.ExitCode, null, finalOutputPath);
    }

    /// <summary>生成 .part 半成品路径:同目录下 .~原名.扩展名.guid.part。</summary>
    public static string BuildPartPath(string finalOutputPath)
    {
        var dir = Path.GetDirectoryName(finalOutputPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(finalOutputPath);
        var ext = Path.GetExtension(finalOutputPath).TrimStart('.');
        return Path.Combine(dir, $".~{name}.{ext}.{Guid.NewGuid():N}.part");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* 尽力而为 */ }
    }

    private static string? TrimError(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return "ffmpeg 转换失败(无错误详情)";
        var lines = stderr.Trim().Split('\n');
        // 取最后几行(最有价值的错误信息在末尾)
        return string.Join('\n', lines.TakeLast(Math.Min(5, lines.Length))).Trim();
    }
}
