using System.IO;
using System.Text.Json;
using FormatConverter.Core.Models;

namespace FormatConverter.App.Services;

/// <summary>
/// 应用设置持久化:%APPDATA%\FormatConverter\settings.json(缺失/损坏回默认值)。
/// 数据模型与清洗逻辑在 Core 的 AppSettingsData;这里只负责文件 IO 与主题映射。
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FormatConverter", "settings.json");

    /// <summary>读取设置;旧版(仅 2 字段)JSON 由同名字段天然迁移,缺失字段回默认。</summary>
    public static AppSettingsData Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return AppSettingsData.Defaults();
            var data = JsonSerializer.Deserialize<AppSettingsData>(
                           File.ReadAllText(SettingsPath), AppSettingsData.JsonOptions)
                       ?? AppSettingsData.Defaults();
            data.Sanitize();
            return data;
        }
        catch
        {
            return AppSettingsData.Defaults();
        }
    }

    /// <summary>保存设置(先清洗);写入失败不影响主流程。</summary>
    public static void Save(AppSettingsData data)
    {
        try
        {
            data.Sanitize();
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, AppSettingsData.JsonOptions));
        }
        catch
        {
            // 设置写入失败不影响主流程
        }
    }

    /// <summary>读取主题偏好;默认跟随系统。</summary>
    public static AppTheme LoadTheme()
    {
        var raw = Load().Theme;
        return Enum.TryParse<AppTheme>(raw, ignoreCase: true, out var theme)
            ? theme
            : AppTheme.System;
    }
}
