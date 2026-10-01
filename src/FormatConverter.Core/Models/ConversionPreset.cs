namespace FormatConverter.Core.Models;

/// <summary>
/// 转换预设 = 目标格式 + 参数覆盖 + 输出文件名模板 + 转换后动作。
/// 内置预设 Id 固定为 "builtin.*"(定义在 Core,不进 settings.json);用户预设 Id 为 Guid。
/// </summary>
public sealed record ConversionPreset
{
    /// <summary>唯一 Id(内置 "builtin.*",用户预设 Guid "N" 格式)。</summary>
    public required string Id { get; init; }

    /// <summary>显示名称(忽略大小写唯一,且不得与格式扩展名同名,防 CLI 歧义)。</summary>
    public required string Name { get; init; }

    /// <summary>目标格式扩展名(不含点,必须是 FormatRegistry.IsTargetFormat 的格式)。</summary>
    public required string TargetExtension { get; init; }

    /// <summary>输出文件名模板(占位符语法见 OutputTemplateEngine);null = 跟随全局输出逻辑。</summary>
    public string? OutputFileNameTemplate { get; init; }

    public PostConversionAction PostConversionAction { get; init; }

    /// <summary>归档目标文件夹;null = 输出所在目录\归档。</summary>
    public string? ArchiveFolder { get; init; }

    public ConversionOptionsOverride? Options { get; init; }

    /// <summary>内置预设(可删除、不可编辑)。</summary>
    public bool IsBuiltIn { get; init; }
}
