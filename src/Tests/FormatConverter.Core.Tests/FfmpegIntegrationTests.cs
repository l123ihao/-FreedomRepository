using System.Diagnostics;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

/// <summary>
/// ffmpeg 集成测试:用 lavfi 自生成测试素材,走完整转换链路,ffprobe 校验输出。
/// 未检测到 ffmpeg 时全部直接返回(CI 环境无 ffmpeg 也不报错)。
/// </summary>
public class FfmpegIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sample;
    private static readonly ConversionOptions TranscodeOpts = new() { VideoMode = VideoMode.AlwaysTranscode };

    public FfmpegIntegrationTests()
    {
        Directory.CreateDirectory(_dir);
        _sample = Path.Combine(_dir, "sample.mp4");

        if (!FfmpegLocator.IsAvailable) return;

        // testsrc2 彩条 + sine 正弦音 → 3 秒测试视频
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

    private string PathIn(string name) => Path.Combine(_dir, name);

    private async Task<IReadOnlyList<ConversionResult>> RunAsync(IReadOnlyList<ConversionJob> jobs)
    {
        var engine = new ConversionEngine(ConverterFactory.CreateDefault());
        return await engine.ConvertAllAsync(jobs, null, CancellationToken.None);
    }

    [Fact]
    public async Task Mp4_To_Mkv_Copies_And_Keeps_Duration()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = PathIn("out.mkv");
        var results = await RunAsync(new[] { Job(_sample, "mkv", output, new ConversionOptions()) });

        Assert.True(results[0].Success, results[0].ErrorMessage);
        Assert.True(File.Exists(output));
        var probe = await FfmpegProbe.ProbeAsync(output);
        Assert.NotNull(probe);
        Assert.InRange(probe!.DurationSeconds, 2.5, 3.5);
    }

    [Fact]
    public async Task Mp4_To_Webm_Transcodes_To_Vp9()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = PathIn("out.webm");
        var results = await RunAsync(new[] { Job(_sample, "webm", output, TranscodeOpts) });

        Assert.True(results[0].Success, results[0].ErrorMessage);
        var probe = await FfmpegProbe.ProbeAsync(output);
        Assert.Contains(probe!.Streams, s => s.CodecType == "video" && s.CodecName == "vp9");
    }

    [Fact]
    public async Task Mp4_To_Gif_Produces_Animated_Gif()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = PathIn("out.gif");
        var results = await RunAsync(new[] { Job(_sample, "gif", output, new ConversionOptions()) });

        Assert.True(results[0].Success, results[0].ErrorMessage);
        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public async Task Mp4_To_Mp3_Extracts_Audio_Only()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = PathIn("out.mp3");
        var results = await RunAsync(new[] { Job(_sample, "mp3", output, new ConversionOptions()) });

        Assert.True(results[0].Success, results[0].ErrorMessage);
        var probe = await FfmpegProbe.ProbeAsync(output);
        Assert.NotNull(probe);
        Assert.Contains(probe!.Streams, s => s.CodecType == "audio");
        Assert.DoesNotContain(probe.Streams, s => s.CodecType == "video");
        Assert.InRange(probe.DurationSeconds, 2.5, 3.5);
    }

    [Fact]
    public async Task Mp3_To_Wav_To_Flac_Chain()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var mp3 = PathIn("step1.mp3");
        var wav = PathIn("step2.wav");
        var flac = PathIn("step3.flac");

        var r1 = await RunAsync(new[] { Job(_sample, "mp3", mp3, new ConversionOptions()) });
        Assert.True(r1[0].Success, r1[0].ErrorMessage);

        var r2 = await RunAsync(new[] { Job(mp3, "wav", wav, new ConversionOptions()) });
        Assert.True(r2[0].Success, r2[0].ErrorMessage);

        var r3 = await RunAsync(new[] { Job(wav, "flac", flac, new ConversionOptions()) });
        Assert.True(r3[0].Success, r3[0].ErrorMessage);

        var probe = await FfmpegProbe.ProbeAsync(flac);
        Assert.InRange(probe!.DurationSeconds, 2.5, 3.5);
    }

    [Fact]
    public async Task Chinese_Filename_Works()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var src = PathIn("测试 视频.mp4");
        File.Copy(_sample, src);
        var output = PathIn("转换结果 副本.mkv");

        var results = await RunAsync(new[] { Job(src, "mkv", output, new ConversionOptions()) });

        Assert.True(results[0].Success, results[0].ErrorMessage);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task Cancel_Kills_Process_And_Leaves_No_Part_File()
    {
        if (!FfmpegLocator.IsAvailable) return;

        // 长素材 + 强制转码,保证取消时仍在运行
        var longSample = PathIn("long.mp4");
        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "-y", "-hide_banner", "-loglevel", "error",
                     "-f", "lavfi", "-i", "testsrc2=size=640x480:rate=30:duration=30",
                     "-f", "lavfi", "-i", "sine=frequency=440:duration=30",
                     "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
                     "-c:a", "aac", "-shortest", longSample,
                 })
            psi.ArgumentList.Add(arg);
        using (var p = Process.Start(psi)!)
            p.WaitForExit(120_000);

        var output = PathIn("cancelled.mkv");
        var engine = new ConversionEngine(ConverterFactory.CreateDefault());
        using var cts = new CancellationTokenSource();
        var task = engine.ConvertAllAsync(new[] { Job(longSample, "mkv", output, TranscodeOpts) }, null, cts.Token);
        cts.CancelAfter(700);

        var results = await task;

        Assert.False(results[0].Success);
        Assert.Equal("已取消", results[0].ErrorMessage);
        Assert.Empty(Directory.GetFiles(_dir, "*.part"));
    }

    [Fact]
    public async Task Engine_Reports_Progress_With_File_Index()
    {
        if (!FfmpegLocator.IsAvailable) return;
        var output = PathIn("progress.mkv");
        var reports = new List<ProgressInfo>();
        var progress = new Progress<ProgressInfo>(reports.Add);

        var engine = new ConversionEngine(ConverterFactory.CreateDefault());
        await engine.ConvertAllAsync(new[] { Job(_sample, "mkv", output, new ConversionOptions()) }, progress, CancellationToken.None);

        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal(1, r.FileIndex));
        Assert.All(reports, r => Assert.Equal("sample.mp4", r.FileName));
    }

    private ConversionJob Job(string src, string targetExt, string output, ConversionOptions options) =>
        new(Guid.NewGuid(), src, output, targetExt, options);
}
