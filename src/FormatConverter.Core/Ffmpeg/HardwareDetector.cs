using System.Diagnostics;

namespace FormatConverter.Core.Ffmpeg;

/// <summary>
/// 检测 ffmpeg 可用的硬件视频编码器(NVENC 优先)。
/// 结果缓存:整个进程生命周期只探测一次(-encoders 输出解析)。
/// </summary>
public static class HardwareDetector
{
    private static string? _preferred;
    private static bool _detected;

    /// <summary>优先硬件编码器名(如 h264_nvenc);不可用返回 null。</summary>
    public static string? PreferredEncoder
    {
        get
        {
            if (!_detected)
            {
                _preferred = Detect();
                _detected = true;
            }
            return _preferred;
        }
    }

    private static string? Detect()
    {
        if (!FfmpegLocator.IsAvailable) return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegLocator.FfmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-encoders");

            using var process = Process.Start(psi);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            if (process.ExitCode != 0) return null;

            if (output.Contains("h264_nvenc", StringComparison.Ordinal)) return "h264_nvenc";
            if (output.Contains("h264_qsv", StringComparison.Ordinal)) return "h264_qsv";
            if (output.Contains("h264_amf", StringComparison.Ordinal)) return "h264_amf";
            return null;
        }
        catch
        {
            return null;
        }
    }
}
