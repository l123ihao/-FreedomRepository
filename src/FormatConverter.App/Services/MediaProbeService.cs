using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.App.Services;

/// <summary>
/// 行内媒体信息探测:对视频/音频文件异步调用 ffprobe 获取时长、分辨率、编码。
/// ffmpeg 缺失或探测失败时静默返回 null,不影响主流程。
/// </summary>
public static class MediaProbeService
{
    public static async Task<string?> ProbeTextAsync(
        string sourcePath, FileCategory category, CancellationToken ct = default)
    {
        if (category is not (FileCategory.Video or FileCategory.Audio)) return null;
        if (!FfmpegLocator.IsAvailable) return null;

        try
        {
            var probe = await FfmpegProbe.ProbeAsync(sourcePath, ct);
            if (probe is null) return null;

            var parts = new List<string>();
            if (probe.DurationSeconds >= 1)
                parts.Add(FormatDuration(probe.DurationSeconds));

            var video = probe.Streams.FirstOrDefault(s => s.CodecType == "video");
            if (video is not null)
            {
                if (video.Width > 0 && video.Height > 0)
                    parts.Add($"{video.Width}×{video.Height}");
                if (video.CodecName.Length > 0)
                    parts.Add(video.CodecName);
            }

            var audio = probe.Streams.FirstOrDefault(s => s.CodecType == "audio");
            if (audio is not null && audio.CodecName.Length > 0)
                parts.Add(audio.CodecName);

            if (probe.BitRate > 0)
                parts.Add(FormatBitRate(probe.BitRate));

            return parts.Count > 0 ? " · " + string.Join(" · ", parts) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }

    private static string FormatBitRate(double bps) => bps switch
    {
        >= 1_000_000 => $"{bps / 1_000_000:0.#} Mbps",
        >= 1_000 => $"{bps / 1_000:0} kbps",
        _ => $"{bps:0} bps",
    };
}
