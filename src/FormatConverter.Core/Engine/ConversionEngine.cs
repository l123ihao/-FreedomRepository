using System.Diagnostics;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Engine;

/// <summary>
/// 批量转换引擎:路由到各转换器、补全进度信息(第 x/y 个文件)、整体取消。
/// 默认串行(maxDegreeOfParallelism = 1),避免 libx264 转码吃满 CPU 互相拖慢。
/// </summary>
public sealed class ConversionEngine
{
    private readonly ConverterFactory _factory;
    private readonly int _maxDegreeOfParallelism;

    public ConversionEngine(ConverterFactory factory, int maxDegreeOfParallelism = 1)
    {
        _factory = factory;
        _maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism);
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
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = ct,
        };

        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, count), options, async (i, innerCt) =>
            {
                var job = jobs[i];
                var converter = _factory.GetConverter(job);

                var local = progress is null
                    ? null
                    : new SyncProgress<ProgressInfo>(pi => progress.Report(pi with
                    {
                        FileIndex = i + 1,
                        FileCount = count,
                        FileName = Path.GetFileName(job.SourcePath),
                    }));

                results[i] = converter is null
                    ? new ConversionResult(job, false, null,
                        $"不支持将 {job.Category} 转换为 {job.TargetExtension}", TimeSpan.Zero)
                    : await converter.ConvertAsync(job, local, innerCt);
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 整体取消:部分已启动的转换器已各自返回"已取消",未启动的补上
        }

        for (var i = 0; i < count; i++)
            results[i] ??= new ConversionResult(jobs[i], false, null, "已取消", TimeSpan.Zero);

        return results;
    }
}
