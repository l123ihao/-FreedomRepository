using System.IO;
using System.Text.Json;

namespace FormatConverter.App.Services;

/// <summary>应用设置持久化:%APPDATA%\FormatConverter\settings.json。缺失或损坏时回默认值。</summary>
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FormatConverter", "settings.json");

    private sealed record Settings(bool DontAskBeforeConvert, string? Theme);

    private static Settings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new Settings(false, null);
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath))
                   ?? new Settings(false, null);
        }
        catch
        {
            return new Settings(false, null);
        }
    }

    private static void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 设置写入失败不影响主流程
        }
    }

    /// <summary>读取「不再询问拖入转换确认」;默认 false。</summary>
    public static bool LoadDontAskBeforeConvert() => Load().DontAskBeforeConvert;

    public static void SaveDontAskBeforeConvert(bool value) =>
        Save(Load() with { DontAskBeforeConvert = value });

    /// <summary>读取主题偏好;默认跟随系统。</summary>
    public static AppTheme LoadTheme()
    {
        var raw = Load().Theme;
        return Enum.TryParse<AppTheme>(raw, ignoreCase: true, out var theme)
            ? theme
            : AppTheme.System;
    }

    public static void SaveTheme(AppTheme theme) =>
        Save(Load() with { Theme = theme.ToString() });
}
