using System.Diagnostics;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Converters;

/// <summary>
/// ffmpeg 转换器基类:probe → 参数 → 运行 → 结果。
/// 视频/音频两个子类只负责声明 CanConvert 路由。
/// </summary>
public abstract class FfmpegConverterBase : IConverter
{
    private readonly FfmpegRunner _runner = new();

    public abstract bool CanConvert(ConversionJob job);

    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!FfmpegLocator.IsAvailable)
                return Fail(job, sw, "未找到 ffmpeg/ffprobe,程序安装不完整(缺少 ffmpeg 目录)。");

            var probe = await FfmpegProbe.ProbeAsync(job.SourcePath, ct);

            // 输出先写 .part 半成品(与 runner 约定同一路径),成功后再由 runner 原子改名
            var dir = Path.GetDirectoryName(job.OutputPath)!;
            Directory.CreateDirectory(dir);
            var partPath = FfmpegRunner.BuildPartPath(job.OutputPath);
            var args = FfmpegArgsBuilder.Build(job.SourcePath, job.TargetExtension, partPath, job.Options, probe);

            var ffmpegProgress = progress is null
                ? null
                : new SyncProgress<FfmpegProgress>(fp => progress.Report(new ProgressInfo(
                    fp.Percent, fp.SecondsDone, fp.TotalSeconds, fp.Speed, 0, 0, null)));

            var run = await _runner.RunAsync(args, job.OutputPath, probe?.DurationSeconds, ffmpegProgress, ct);

            if (run.Success)
                return new ConversionResult(job, true, run.FinalOutputPath, null, sw.Elapsed);

            return Fail(job, sw, run.ErrorOutput ?? "转换失败");
        }
        catch (OperationCanceledException)
        {
            return Fail(job, sw, "已取消");
        }
        catch (Exception ex)
        {
            return Fail(job, sw, ex.Message);
        }
    }

    private static ConversionResult Fail(ConversionJob job, Stopwatch sw, string error) =>
        new(job, false, null, error, sw.Elapsed);
}
