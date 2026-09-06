using System.Diagnostics;
using System.Text.Json;

namespace FormatConverter.Core.Ffmpeg;

public sealed record ProbeStream(string CodecType, string CodecName, int Width = 0, int Height = 0, long BitRate = 0);

/// <summary>媒体探测结果:时长(秒)、流信息、整体码率(bps)。</summary>
public sealed record ProbeResult(double DurationSeconds, IReadOnlyList<ProbeStream> Streams, double BitRate = 0);

/// <summary>用 ffprobe 预取时长与流信息(决定 copy 还是转码、计算进度百分比)。</summary>
public static class FfmpegProbe
{
    public static async Task<ProbeResult?> ProbeAsync(string inputPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.FfprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration,bit_rate:stream=codec_type,codec_name,width,height,bit_rate");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add(inputPath);

        using var process = Process.Start(psi);
        if (process is null) return null;

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return null;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            double duration = 0;
            double bitRate = 0;
            if (root.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var dur) &&
                dur.ValueKind == JsonValueKind.String)
            {
                double.TryParse(dur.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out duration);

                if (format.TryGetProperty("bit_rate", out var br) && br.ValueKind == JsonValueKind.String)
                    double.TryParse(br.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out bitRate);
            }

            var streams = new List<ProbeStream>();
            if (root.TryGetProperty("streams", out var arr))
            {
                foreach (var s in arr.EnumerateArray())
                {
                    var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() ?? "" : "";
                    var name = s.TryGetProperty("codec_name", out var n) ? n.GetString() ?? "" : "";
                    var width = s.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0;
                    var height = s.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0;
                    var streamBitRate = s.TryGetProperty("bit_rate", out var sbr) && sbr.ValueKind == JsonValueKind.String
                        ? long.Parse(sbr.GetString()!)
                        : 0;
                    streams.Add(new ProbeStream(type, name, width, height, streamBitRate));
                }
            }
            return new ProbeResult(duration, streams, bitRate);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
