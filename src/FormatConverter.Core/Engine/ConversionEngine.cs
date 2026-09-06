using System.Diagnostics;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Engine;

/// <summary>
/// 批量转换引擎:路由到各转换器、补全进度信息(第 x/y 个文件)、整体取消。
/// 默认串行(maxDegreeOfParallelism = 1)。smartParallelism = true 时:
/// 图片/文档按 CPU 核数并行,视频/音频保持串行(避免转码吃满 CPU 互相拖慢)。
/// </summary>
public sealed class ConversionEngine
{
    private readonly ConverterFactory _factory;
    private readonly int _maxDegreeOfParallelism;
    private readonly bool _smartParallelism;
    private readonly int _nonMediaParallelism;

    public ConversionEngine(
        ConverterFactory factory,
        int maxDegreeOfParallelism = 1,
        bool smartParallelism = false,
        int nonMediaParallelism = 0)
    {
        _factory = factory;
        _maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism);
        _smartParallelism = smartParallelism;
        _nonMediaParallelism = nonMediaParallelism > 0
            ? nonMediaParallelism
            : Math.Max(1, Environment.ProcessorCount);
    }

    /// <summary>
    /// 顺序返回与 jobs 一一对应的结果(每项都不会为 null)。
    /// 单个文件失败不影响后续文件;整体取消时未开始的文件标记为"已取消"。
    /// </summary>
    public async Task<IReadOnlyList<ConversionResult>> ConvertAllAsync(
        IReadOnlyList<ConversionJob> jobs,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct)
    {
        var count = jobs.Count;
        var results = new ConversionResult[count];

        // 分组:媒体(ffmpeg 视频/音频)串行,其余(图片/文档)并行
        var groups = BuildGroups(jobs, count);

        try
        {
            foreach (var (indices, parallelism) in groups)
                await RunGroupAsync(indices, parallelism, results, jobs, progress, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 整体取消:部分已启动的转换器已各自返回"已取消",未启动的下面补上
        }

        for (var i = 0; i < count; i++)
            results[i] ??= new ConversionResult(jobs[i], false, null, "已取消", TimeSpan.Zero);

        return results;
    }

    private IReadOnlyList<(List<int> Indices, int Parallelism)> BuildGroups(
        IReadOnlyList<ConversionJob> jobs, int count)
    {
        if (!_smartParallelism)
            return new[] { (Enumerable.Range(0, count).ToList(), _maxDegreeOfParallelism) };

        var mediaIndices = new List<int>();
        var otherIndices = new List<int>();

        for (var i = 0; i < count; i++)
        {
            // 媒体(视频/音频)串行,避免转码互相抢 CPU;图片/文档并行
            if (jobs[i].Category is FileCategory.Video or FileCategory.Audio)
                mediaIndices.Add(i);
            else
                otherIndices.Add(i);
        }

        // 先跑图片/文档(并行),再跑媒体(串行),保证队列顺序大致观感
        var groups = new List<(List<int>, int)> { (otherIndices, _nonMediaParallelism) };
        if (mediaIndices.Count > 0) groups.Add((mediaIndices, 1));
        return groups;
    }

    private async Task RunGroupAsync(
        List<int> indices,
        int parallelism,
        ConversionResult[] results,
        IReadOnlyList<ConversionJob> jobs,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct)
    {
        if (indices.Count == 0) return;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, parallelism),
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(indices, options, async (i, innerCt) =>
        {
            var job = jobs[i];
            var converter = _factory.GetConverter(job);

            var local = progress is null
                ? null
                : new SyncProgress<ProgressInfo>(pi => progress.Report(pi with
                {
                    FileIndex = i + 1,
                    FileCount = jobs.Count,
                    FileName = Path.GetFileName(job.SourcePath),
                }));

            results[i] = converter is null
                ? new ConversionResult(job, false, null,
                    $"不支持将 {job.Category} 转换为 {job.TargetExtension}", TimeSpan.Zero)
                : await converter.ConvertAsync(job, local, innerCt);
        });
    }
}
