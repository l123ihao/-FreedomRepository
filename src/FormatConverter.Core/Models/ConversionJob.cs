using FormatConverter.Core.Formats;

namespace FormatConverter.Core.Models;

/// <summary>一个待执行的转换任务。</summary>
public sealed record ConversionJob(
    Guid Id,
    string SourcePath,
    string OutputPath,
    string TargetExtension,
    ConversionOptions Options)
{
    public FileCategory Category => FormatRegistry.GetCategory(SourcePath);
}
