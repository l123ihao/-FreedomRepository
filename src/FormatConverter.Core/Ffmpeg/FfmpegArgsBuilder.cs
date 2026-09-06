using FormatConverter.Core.Models;

namespace FormatConverter.Core.Ffmpeg;

/// <summary>
/// 按目标格式生成 ffmpeg 完整参数列表(不含 -progress 等全局选项,由 runner 补充)。
/// 参数以列表形式传递,绝不拼接字符串,保证中文路径/空格安全。
/// </summary>
public static class FfmpegArgsBuilder
{
    private static readonly HashSet<string> AudioTargets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b",
        };

    private static readonly HashSet<string> VideoTargets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "mp4", "mkv", "avi", "mov", "webm", "gif", "flv", "ts", "m4v", "3gp", "wmv",
        };

    public static bool IsAudioTarget(string extension) => AudioTargets.Contains(extension);
    public static bool IsVideoTarget(string extension) => VideoTargets.Contains(extension);

    /// <summary>目标扩展名对应的 ffmpeg 容器/muxer 名(.part 临时文件扩展名无法推断格式,必须显式指定)。</summary>
    public static string GetMuxer(string targetExtension) => targetExtension.ToLowerInvariant() switch
    {
        "mp4" or "mov" or "m4v" => "mp4",
        "mkv" => "matroska",
        "avi" => "avi",
        "webm" => "webm",
        "gif" => "gif",
        "flv" => "flv",
        "ts" => "mpegts",
        "3gp" => "3gp",
        "wmv" => "asf",
        "mp3" => "mp3",
        "wav" => "wav",
        "flac" => "flac",
        "m4a" or "m4b" => "ipod",
        "aac" => "adts",
        "ogg" or "opus" => "ogg",
        "aiff" => "aiff",
        "wma" => "asf",
        _ => throw new ArgumentException($"不支持的目标格式: {targetExtension}"),
    };

    public static IReadOnlyList<string> Build(
        string inputPath, string targetExtension, string outputPath,
        ConversionOptions options, ProbeResult? probe, string? hardwareEncoder = null)
    {
        var args = new List<string> { "-i", inputPath };
        var target = targetExtension.ToLowerInvariant();

        if (IsAudioTarget(target))
            BuildAudioArgs(args, target, options);
        else if (target == "gif")
            BuildGifArgs(args, options);
        else
            BuildVideoArgs(args, target, options, probe, hardwareEncoder);

        args.Add("-f");
        args.Add(GetMuxer(target));
        args.Add(outputPath);
        return args;
    }

    // ---------- 音频 ----------

    private static void BuildAudioArgs(List<string> args, string target, ConversionOptions options)
    {
        args.Add("-vn");
        args.Add("-c:a");
        switch (target)
        {
            case "mp3":
                args.Add("libmp3lame");
                args.Add("-b:a");
                args.Add($"{options.AudioBitrateKbps}k");
                break;
            case "wav":
                args.Add("pcm_s16le");
                break;
            case "flac":
                args.Add("flac");
                break;
            case "m4a":
            case "aac":
            case "m4b":
                args.Add("aac");
                args.Add("-b:a");
                args.Add($"{options.AudioBitrateKbps}k");
                break;
            case "ogg":
                args.Add("libvorbis");
                args.Add("-q:a");
                args.Add("5");
                break;
            case "opus":
                args.Add("libopus");
                args.Add("-b:a");
                args.Add("128k");
                break;
            case "aiff":
                args.Add("pcm_s16be");
                break;
            case "wma":
                args.Add("wmav2");
                args.Add("-b:a");
                args.Add($"{options.AudioBitrateKbps}k");
                break;
        }
    }

    // ---------- GIF ----------

    private static void BuildGifArgs(List<string> args, ConversionOptions options)
    {
        // 单遍调色板:palettegen 与 paletteuse 用 split 串联,一个进程搞定(进度可用)
        args.Add("-filter_complex");
        args.Add(
            $"fps={options.GifFps},scale={options.GifWidth}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse=dither=bayer");
        args.Add("-loop");
        args.Add("0");
    }

    // ---------- 视频 ----------

    private static void BuildVideoArgs(
        List<string> args, string target, ConversionOptions options,
        ProbeResult? probe, string? hardwareEncoder)
    {
        var videoStream = probe?.Streams.FirstOrDefault(s => s.CodecType == "video");
        var audioStream = probe?.Streams.FirstOrDefault(s => s.CodecType == "audio");

        var canCopy = options.VideoMode == VideoMode.CopyFirst
                      && target != "webm"                 // webm 必须转码 vp9
                      && videoStream is not null
                      && (videoStream.CodecName is "h264" or "hevc"); // gif/mjpeg 等源流无法 copy 进 mp4

        if (canCopy && audioStream is not null)
        {
            // avi 容器对 aac 兼容性差,音频强制转 mp3
            if (target == "avi" && audioStream.CodecName != "mp3")
            {
                args.Add("-c:v");
                args.Add("copy");
                args.Add("-c:a");
                args.Add("libmp3lame");
                args.Add("-b:a");
                args.Add($"{options.AudioBitrateKbps}k");
            }
            else
            {
                args.Add("-c");
                args.Add("copy");
            }
            return;
        }

        // 转码路径
        args.Add("-c:v");
        if (hardwareEncoder is not null && target != "webm")
        {
            // 硬件编码(NVENC/QSV/AMF 统一近似参数;失败由转换器回退软件编码)
            args.Add(hardwareEncoder);
            if (hardwareEncoder == "h264_nvenc")
            {
                args.Add("-preset");
                args.Add("p4");
                args.Add("-cq");
                args.Add(options.VideoCrf.ToString());
                args.Add("-rc");
                args.Add("vbr");
                args.Add("-b:v");
                args.Add("0");
            }
            else if (hardwareEncoder == "h264_qsv")
            {
                args.Add("-global_quality");
                args.Add(options.VideoCrf.ToString());
            }
            else // h264_amf
            {
                args.Add("-quality");
                args.Add("balanced");
                args.Add("-rc");
                args.Add("cqp");
                args.Add("-qp_p");
                args.Add(options.VideoCrf.ToString());
                args.Add("-qp_i");
                args.Add(options.VideoCrf.ToString());
            }
            args.Add("-pix_fmt");
            args.Add("yuv420p");
            // 保证宽高为偶数(硬件编码器同样要求)
            args.Add("-vf");
            args.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
        }
        else switch (target)
        {
            case "webm":
                args.Add("libvpx-vp9");
                args.Add("-crf");
                args.Add("32");
                args.Add("-b:v");
                args.Add("0");
                args.Add("-deadline");
                args.Add("good");
                args.Add("-cpu-used");
                args.Add("2");
                break;
            default: // mp4/mov/mkv/avi
                args.Add("libx264");
                args.Add("-preset");
                args.Add("medium");
                args.Add("-crf");
                args.Add(options.VideoCrf.ToString());
                args.Add("-pix_fmt");
                args.Add("yuv420p");
                // 保证宽高为偶数(x264 要求),奇数源(如 gif)会失败
                args.Add("-vf");
                args.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
                break;
        }

        // 目标 mp4/mov/m4v 加 faststart(利于流式播放)
        if (target is "mp4" or "mov" or "m4v")
        {
            args.Add("-movflags");
            args.Add("+faststart");
        }

        // 音频
        if (audioStream is not null)
        {
            args.Add("-c:a");
            switch (target)
            {
                case "webm":
                    args.Add("libopus");
                    args.Add("-b:a");
                    args.Add("128k");
                    break;
                case "avi":
                    args.Add("libmp3lame");
                    args.Add("-b:a");
                    args.Add($"{options.AudioBitrateKbps}k");
                    break;
                case "wmv":
                    args.Add("wmav2");
                    args.Add("-b:a");
                    args.Add($"{options.AudioBitrateKbps}k");
                    break;
                default:
                    args.Add("aac");
                    args.Add("-b:a");
                    args.Add($"{options.AudioBitrateKbps}k");
                    break;
            }
        }
        else
        {
            args.Add("-an");
        }
    }
}
