namespace FormatConverter.Core.Models;

/// <summary>进度上报(经 <see cref="IProgress{T}"/> 回调,Core 不依赖 UI 线程模型)。</summary>
public sealed record ProgressInfo(
    /// <summary>当前文件进度百分比 0-100;null 表示不确定(如无法预取时长)。</summary>
    double? Percent,
    /// <summary>当前文件已处理/总时长(秒),不可用时为 null。</summary>
    double? SecondsDone,
    double? TotalSeconds,
    /// <summary>ffmpeg 报告的 speed(如 "1.05x"),无则为 null。</summary>
    string? Speed,
    /// <summary>第几个文件(从 1 开始)。</summary>
    int FileIndex,
    /// <summary>总文件数。</summary>
    int FileCount,
    /// <summary>当前文件名(不含目录)。</summary>
    string? FileName);
