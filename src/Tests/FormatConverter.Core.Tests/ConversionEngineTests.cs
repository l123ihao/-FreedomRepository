using FormatConverter.Core.Converters;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class ConversionEngineTests
{
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
}
