using FormatConverter.Core.Converters;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class ConversionEngineTests : IDisposable
{
    private readonly string _dir;

    public ConversionEngineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "");
        return path;
    }

    /// <summary>按 job 类别路由到媒体/非媒体 fake,记录各自最大并发。</summary>
    private sealed class FakeConverter : IConverter
    {
        private readonly bool _media;
        private int _active;
        private int _maxActive;

        public FakeConverter(bool media) => _media = media;

        public int MaxActive => _maxActive;

        public bool CanConvert(ConversionJob job) =>
            _media == (job.Category is FileCategory.Video or FileCategory.Audio);

        public async Task<ConversionResult> ConvertAsync(
            ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            var now = Interlocked.Increment(ref _active);
            while (true)
            {
                var cur = _maxActive;
                if (now <= cur || Interlocked.CompareExchange(ref _maxActive, now, cur) == cur) break;
            }

            await Task.Delay(60, ct);
            Interlocked.Decrement(ref _active);
            return new ConversionResult(job, true, job.OutputPath, null, TimeSpan.Zero);
        }
    }

    private static ConversionJob Job(string ext, string target)
    {
        var src = $"C:\\f\\a.{ext}";
        return new ConversionJob(Guid.NewGuid(), src, $"C:\\f\\out.{target}", target, new ConversionOptions());
    }

    [Fact]
    public async Task SmartParallelism_Runs_Images_Parallel_And_Media_Serial()
    {
        var image = new FakeConverter(media: false);
        var media = new FakeConverter(media: true);
        var factory = new ConverterFactory(image, media);
        var engine = new ConversionEngine(factory, smartParallelism: true);

        var jobs = new List<ConversionJob>
        {
            Job("png", "jpg"), Job("png", "jpg"), Job("png", "jpg"), Job("png", "jpg"),
            Job("mp4", "mkv"), Job("mp4", "mkv"),
        };

        var results = await engine.ConvertAllAsync(jobs, progress: null, CancellationToken.None);

        Assert.Equal(6, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.True(image.MaxActive >= 2, $"图片转换应并行,实际最大并发 {image.MaxActive}");
        Assert.Equal(1, media.MaxActive);
    }

    [Fact]
    public async Task DefaultEngine_Is_Serial_For_All()
    {
        var image = new FakeConverter(media: false);
        var media = new FakeConverter(media: true);
        var factory = new ConverterFactory(image, media);
        var engine = new ConversionEngine(factory); // 默认串行

        var jobs = new List<ConversionJob>
        {
            Job("png", "jpg"), Job("png", "jpg"), Job("png", "jpg"),
        };

        await engine.ConvertAllAsync(jobs, progress: null, CancellationToken.None);

        Assert.Equal(1, image.MaxActive);
    }

    // ---------- 转换后动作 ----------

    /// <summary>写真实输出文件的 fake 转换器。</summary>
    private sealed class WritingConverter : IConverter
    {
        public bool CanConvert(ConversionJob job) => true;

        public async Task<ConversionResult> ConvertAsync(
            ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            await File.WriteAllTextAsync(job.OutputPath, "ok", ct);
            return new ConversionResult(job, true, job.OutputPath, null, TimeSpan.Zero);
        }
    }

    private sealed class FailingConverter : IConverter
    {
        public bool CanConvert(ConversionJob job) => true;

        public Task<ConversionResult> ConvertAsync(
            ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
            => Task.FromResult(new ConversionResult(job, false, null, "输入损坏", TimeSpan.Zero));
    }

    [Fact]
    public async Task PostAction_DeleteSource_Runs_On_Success()
    {
        var source = Touch("a.png");
        var output = Path.Combine(_dir, "a.jpg");
        var job = new ConversionJob(Guid.NewGuid(), source, output, "jpg",
            new ConversionOptions(), PostConversionAction.DeleteSource);
        var engine = new ConversionEngine(new ConverterFactory(new WritingConverter()));

        var r = Assert.Single(await engine.ConvertAllAsync([job], null, CancellationToken.None));

        Assert.True(r.Success);
        Assert.True(r.PostAction!.Success);
        Assert.False(File.Exists(source)); // 原文件被删除
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task PostAction_Skipped_On_Failure()
    {
        var source = Touch("a.png");
        var job = new ConversionJob(Guid.NewGuid(), source, Path.Combine(_dir, "a.jpg"), "jpg",
            new ConversionOptions(), PostConversionAction.DeleteSource);
        var engine = new ConversionEngine(new ConverterFactory(new FailingConverter()));

        var r = Assert.Single(await engine.ConvertAllAsync([job], null, CancellationToken.None));

        Assert.False(r.Success);
        Assert.Null(r.PostAction);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task PostAction_Skipped_On_Cancel()
    {
        var source = Touch("a.png");
        var job = new ConversionJob(Guid.NewGuid(), source, Path.Combine(_dir, "a.jpg"), "jpg",
            new ConversionOptions(), PostConversionAction.DeleteSource);
        var engine = new ConversionEngine(new ConverterFactory(new WritingConverter()));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var r = Assert.Single(await engine.ConvertAllAsync([job], null, cts.Token));

        Assert.Equal("已取消", r.ErrorMessage);
        Assert.Null(r.PostAction);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task Archive_Action_Moves_Output_And_Updates_OutputPath()
    {
        var source = Touch("a.png");
        var output = Path.Combine(_dir, "a.jpg");
        var job = new ConversionJob(Guid.NewGuid(), source, output, "jpg",
            new ConversionOptions(), PostConversionAction.MoveToArchiveFolder);
        var engine = new ConversionEngine(new ConverterFactory(new WritingConverter()));

        var r = Assert.Single(await engine.ConvertAllAsync([job], null, CancellationToken.None));

        Assert.True(r.Success);
        var archived = Path.Combine(_dir, "归档", "a.jpg");
        Assert.Equal(archived, r.OutputPath); // 结果输出路径更新为归档后的位置
        Assert.Equal(archived, r.PostAction!.NewOutputPath);
        Assert.True(File.Exists(archived));
        Assert.False(File.Exists(output));
    }
}
