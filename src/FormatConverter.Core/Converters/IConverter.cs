using FormatConverter.Core.Models;

namespace FormatConverter.Core.Converters;

/// <summary>
/// 单个转换器:负责一类转换(音视频/图片/文档)。
/// 实现方必须捕获所有异常并返回带错误信息的 ConversionResult,不得向上抛出(取消除外)。
/// </summary>
public interface IConverter
{
    /// <summary>判断该转换器能否处理此任务(按源格式分类 + 目标扩展名路由)。</summary>
    bool CanConvert(ConversionJob job);

    /// <summary>
    /// 执行转换。job.OutputPath 已由调用方解析好(含重名策略)。
    /// progress 上报的 ProgressInfo 只填 Percent/Seconds/Speed,文件序号由引擎补全。
    /// </summary>
    Task<ConversionResult> ConvertAsync(ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct);
}
