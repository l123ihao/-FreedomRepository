using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Presets;

/// <summary>新建/编辑预设的校验(返回中文错误消息;null = 通过)。</summary>
public static class PresetValidator
{
    public static string? Validate(ConversionPreset preset, IReadOnlyList<ConversionPreset> existing, string? selfId = null)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
            return "预设名称不能为空。";

        if (existing.Any(p =>
                !string.Equals(p.Id, selfId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
            return "预设名称已存在。";

        var name = preset.Name.Trim();
        if (FormatRegistry.AllFormats.Any(f =>
                string.Equals(f.Extension, name, StringComparison.OrdinalIgnoreCase)))
            return $"预设名称不能与格式扩展名 {name} 同名。";

        var ext = preset.TargetExtension.TrimStart('.').ToLowerInvariant();
        if (!FormatRegistry.IsTargetFormat(ext))
            return $"目标格式 {ext} 不受支持。";

        if (!string.IsNullOrWhiteSpace(preset.OutputFileNameTemplate)
            && !OutputTemplateEngine.TryValidate(preset.OutputFileNameTemplate, out var error))
            return error;

        return null;
    }
}
