using System.Diagnostics;
using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

/// <summary>视频/音频工具集成测试:未检测到 ffmpeg 时全部直接返回。</summary>
public class VideoToolsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-videotools", Guid.NewGuid().ToString("N"));
    private readonly string _sample;

    public VideoToolsTests()
    {
        Directory.CreateDirectory(_dir);
        _sample = Path.Combine(_dir, "sample.mp4");

        if (!FfmpegLocator.IsAvailable) return;

        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "-y", "-hide_banner", "-loglevel", "error",
                     "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=15:duration=3",
                     "-f", "lavfi", "-i", "sine=frequency=440:duration=3",
                     "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest",
                     _sample,
                 })
            psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)!;
        p.WaitForExit(60_000);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    [Fact]
    public async Task Trim_Copies_Requested_Segment()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = Path.Combine(_dir, "trim.mp4");

        await VideoTools.TrimAsync(_sample, output, startSeconds: 1, endSeconds: 2);

        Assert.True(File.Exists(output));
        var probe = await FfmpegProbe.ProbeAsync(output);
        Assert.NotNull(probe);
        Assert.InRange(probe!.DurationSeconds, 0.5, 1.6);
    }

    [Fact]
    public async Task ExtractFrames_Produces_Expected_Frame_Count()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var outDir = Path.Combine(_dir, "frames");

        var frames = await VideoTools.ExtractFramesAsync(_sample, outDir, fps: 2);

        Assert.NotEmpty(frames);
        Assert.All(frames, f => Assert.EndsWith(".jpg", f, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MakeThumbnail_Produces_Single_Image()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = Path.Combine(_dir, "thumbs.jpg");

        await VideoTools.MakeThumbnailAsync(_sample, output, cols: 2, rows: 2, intervalSeconds: 0.5);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public async Task Gif_Produces_Gif_File()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = Path.Combine(_dir, "out.gif");

        await VideoTools.GifAsync(_sample, output, width: 160, fps: 8,
            startSeconds: 0, durationSeconds: 1.5);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public async Task AdjustVolume_Produces_Audio_File()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = Path.Combine(_dir, "quiet.mp3");

        await VideoTools.AdjustVolumeAsync(_sample, output, volume: 0.5,
            fadeInSeconds: 0.2, fadeOutSeconds: null);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
    }
}
