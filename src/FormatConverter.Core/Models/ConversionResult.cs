namespace FormatConverter.Core.Models;

/// <summary>单个文件的转换结果。</summary>
public sealed record ConversionResult(
    ConversionJob Job,
    bool Success,
    string? OutputPath,
    string? ErrorMessage,
    TimeSpan Elapsed);
