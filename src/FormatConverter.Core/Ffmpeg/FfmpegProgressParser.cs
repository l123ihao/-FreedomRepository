using System.Globalization;

namespace FormatConverter.Core.Ffmpeg;

/// <summary>
/// 解析 ffmpeg `-progress pipe:1` 的 key=value 输出。
/// 说明:ffmpeg 的 out_time_ms/out_time_us 实际单位都是微秒。
/// </summary>
public sealed class FfmpegProgressState
{
    /// <summary>累计最大输出时间(微秒)。</summary>
    public long MaxOutputTimeUs { get; set; }

    public string? Speed { get; set; }

    public bool Finished { get; set; }
}

public static class FfmpegProgressParser
{
    public static FfmpegProgressState OnLine(FfmpegProgressState state, string line)
    {
        var idx = line.IndexOf('=');
        if (idx <= 0) return state;

        var key = line.AsSpan(0, idx);
        var value = line[(idx + 1)..];

        if (key.SequenceEqual("out_time_us") || key.SequenceEqual("out_time_ms"))
        {
            // 单位都是微秒;同一周期两者都会出现,取最大值即可
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us))
                state.MaxOutputTimeUs = Math.Max(state.MaxOutputTimeUs, us);
        }
        else if (key.SequenceEqual("speed"))
        {
            var speed = value.Trim();
            if (speed.Length > 0) state.Speed = speed;
        }
        else if (key.SequenceEqual("progress") && value.Trim() == "end")
        {
            state.Finished = true;
        }
        return state;
    }

    /// <summary>根据累计输出时间与总时长计算百分比;总时长未知返回 null。</summary>
    public static double? ToPercent(long maxOutputTimeUs, double? totalSeconds)
    {
        if (totalSeconds is null or <= 0) return null;
        var percent = maxOutputTimeUs / 1_000_000.0 / totalSeconds.Value * 100.0;
        return Math.Clamp(percent, 0, 100);
    }
}
