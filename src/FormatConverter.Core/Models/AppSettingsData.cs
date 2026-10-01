using System.Text.Json;
using System.Text.Json.Serialization;
using FormatConverter.Core.Formats;

namespace FormatConverter.Core.Models;

/// <summary>
/// 应用设置 DTO(settings.json 反/序列化载体;App 侧 SettingsService 负责文件读写)。
/// 预设:内置预设固定 Id 定义在 Presets.BuiltInPresets,不进 JSON;
/// JSON 只存用户预设 + 全量顺序 + 已删除内置 Id 列表。
/// </summary>
public sealed class AppSettingsData
{
    /// <summary>schema 版本(留档,未来迁移用)。</summary>
    public int Version { get; set; } = 1;

    public bool DontAskBeforeConvert { get; set; }

    /// <summary>"System"/"Light"/"Dark"(App 侧映射 AppTheme)。</summary>
    public string? Theme { get; set; }

    public string OutputDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "格式转换输出");

    public bool OutputToSourceFolder { get; set; }

    public bool AutoRename { get; set; } = true;

    public int AudioBitrateKbps { get; set; } = 192;

    public bool VideoCopyFirst { get; set; } = true;

    public bool VideoHardwareAcceleration { get; set; } = true;

    /// <summary>全部成功后自动退出程序。</summary>
    public bool ExitAfterConversions { get; set; }

    /// <summary>自动退出倒计时秒数(0-10,Sanitize 钳制)。</summary>
    public int ExitDelaySeconds { get; set; } = 5;

    public List<ConversionPreset> UserPresets { get; set; } = new();

    /// <summary>预设全量显示顺序(内置 Id 与用户 Id 混排);未列入的按默认规则追加。</summary>
    public List<string> PresetOrder { get; set; } = new();

    /// <summary>用户删除的内置预设 Id(合并时跳过,不复活)。</summary>
    public List<string> DeletedBuiltInPresetIds { get; set; } = new();

    public static AppSettingsData Defaults() => new();

    /// <summary>
    /// 清洗非法值:延时钳制 0-10;丢弃非法用户预设(空 Id/空名/未知目标格式);顺序表去重并剔无效 Id。
    /// </summary>
    public void Sanitize()
    {
        ExitDelaySeconds = Math.Clamp(ExitDelaySeconds, 0, 10);
        AudioBitrateKbps = Math.Clamp(AudioBitrateKbps, 32, 320);

        Theme = Theme switch
        {
            var t when string.Equals(t, "System", StringComparison.OrdinalIgnoreCase) => "System",
            var t when string.Equals(t, "Light", StringComparison.OrdinalIgnoreCase) => "Light",
            var t when string.Equals(t, "Dark", StringComparison.OrdinalIgnoreCase) => "Dark",
            _ => null,
        };

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        UserPresets = UserPresets
            .Where(p => !string.IsNullOrWhiteSpace(p.Id)
                        && !string.IsNullOrWhiteSpace(p.Name)
                        && FormatRegistry.IsTargetFormat(p.TargetExtension))
            .Where(p => seenIds.Add(p.Id))
            .ToList();

        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in UserPresets) validIds.Add(p.Id);
        foreach (var id in Presets.BuiltInPresets.Defaults) validIds.Add(id.Id);

        PresetOrder = PresetOrder
            .Where(validIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        DeletedBuiltInPresetIds = DeletedBuiltInPresetIds
            .Where(id => id.StartsWith("builtin.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>全局设置 → 默认转换参数(预设覆盖的基底)。</summary>
    public ConversionOptions ToConversionOptions() => new()
    {
        AudioBitrateKbps = AudioBitrateKbps,
        OverwritePolicy = AutoRename ? OverwritePolicy.Rename : OverwritePolicy.Overwrite,
        VideoMode = VideoCopyFirst ? VideoMode.CopyFirst : VideoMode.AlwaysTranscode,
        HardwareAcceleration = VideoHardwareAcceleration,
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
