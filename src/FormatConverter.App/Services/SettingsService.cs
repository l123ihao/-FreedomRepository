using System.IO;
using System.Text.Json;

namespace FormatConverter.App.Services;

/// <summary>应用设置持久化:%APPDATA%\FormatConverter\settings.json。缺失或损坏时回默认值。</summary>
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FormatConverter", "settings.json");

    private sealed record Settings(bool DontAskBeforeConvert);

    /// <summary>读取「不再询问拖入转换确认」;默认 false。</summary>
    public static bool LoadDontAskBeforeConvert()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return false;
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
            return settings?.DontAskBeforeConvert ?? false;
        }
        catch
        {
            return false;
        }
    }

    public static void SaveDontAskBeforeConvert(bool value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(new Settings(value), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 设置写入失败不影响主流程
        }
    }
}
