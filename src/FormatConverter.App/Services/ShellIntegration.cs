using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;
using Microsoft.Win32;

namespace FormatConverter.App.Services;

/// <summary>
/// Windows 资源管理器右键菜单集成(HKCU,免管理员):
/// 右键任意文件 → 万能格式转换器 → 转为 MP4/MP3/PDF/PNG/JPG/GIF/DOCX/WAV。
/// 每个子命令以 --convert &lt;target&gt; "%1" 调用本程序。
/// </summary>
public static class ShellIntegration
{
    private const string MenuRoot = @"Software\Classes\*\shell\FormatConverter";
    private const string CommandPrefix = "FormatConverter.";
    private const string PresetKeyPrefix = "FormatConverter.Preset.";

    /// <summary>右键菜单展示的常用目标格式(与 FormatRegistry 目标格式对齐)。</summary>
    public static readonly IReadOnlyList<string> Targets = new[]
    {
        "mp4", "mp3", "pdf", "png", "jpg", "gif", "docx", "wav",
    };

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["mp4"] = "转为 MP4 视频",
        ["mp3"] = "转为 MP3 音频",
        ["pdf"] = "转为 PDF 文档",
        ["png"] = "转为 PNG 图片",
        ["jpg"] = "转为 JPG 图片",
        ["gif"] = "转为 GIF 图片",
        ["docx"] = "转为 Word 文档",
        ["wav"] = "转为 WAV 音频",
    };

    public static bool IsInstalled
    {
        get
        {
            try { return Registry.CurrentUser.OpenSubKey(MenuRoot) is not null; }
            catch { return false; }
        }
    }

    public static void Install()
    {
        var exe = Environment.ProcessPath
                  ?? throw new InvalidOperationException("无法确定程序路径。");

        using (var root = Registry.CurrentUser.CreateSubKey(MenuRoot))
        {
            root.SetValue("", "万能格式转换器");
            root.SetValue("Icon", exe);
            root.SetValue("SubCommands", string.Join(";", Targets.Select(t => CommandPrefix + t)));
        }

        foreach (var target in Targets)
        {
            using var sub = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + CommandPrefix + target);
            sub.SetValue("", Labels[target]);
            using var cmd = sub.CreateSubKey("command");
            cmd.SetValue("", $"\"{exe}\" --convert {target} \"%1\"");
        }

        // 同步用户预设子命令
        var settings = SettingsService.Load();
        SyncPresets(PresetMerger.Merge(
            BuiltInPresets.Defaults, settings.UserPresets,
            settings.PresetOrder, settings.DeletedBuiltInPresetIds));
    }

    public static void Uninstall()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(MenuRoot, throwOnMissingSubKey: false); } catch { }
        foreach (var target in Targets)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Classes\" + CommandPrefix + target, throwOnMissingSubKey: false);
            }
            catch { }
        }
        DeletePresetSubKeys();
    }

    /// <summary>
    /// 同步右键菜单中的预设子命令(预设增删改排序后调用)。
    /// 未安装时 no-op;清理已不存在的预设残留子键;全部 try/catch,失败不影响主流程。
    /// </summary>
    public static void SyncPresets(IReadOnlyList<ConversionPreset> presets)
    {
        try
        {
            if (!IsInstalled) return;

            var exe = Environment.ProcessPath ?? "";
            var validIds = presets.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 清理残留:已删除/改名的预设子键
            var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classes is not null)
            {
                foreach (var name in classes.GetSubKeyNames())
                {
                    if (!name.StartsWith(PresetKeyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var id = name[PresetKeyPrefix.Length..];
                    if (validIds.Contains(id)) continue;
                    try { classes.DeleteSubKeyTree(name); } catch { /* 尽力而为 */ }
                }

                foreach (var preset in presets)
                {
                    using var sub = classes.CreateSubKey(PresetKeyPrefix + preset.Id);
                    if (sub is null) continue;
                    sub.SetValue("", $"转为 {preset.Name}");
                    using var cmd = sub.CreateSubKey("command");
                    if (exe.Length > 0)
                        cmd?.SetValue("", $"\"{exe}\" --preset {preset.Id} \"%1\"");
                }
            }

            // 根键 SubCommands 追加预设命令 key(级联菜单才会展示)
            using var root = Registry.CurrentUser.OpenSubKey(MenuRoot, writable: true);
            if (root is not null)
            {
                var commands = Targets.Select(t => CommandPrefix + t)
                    .Concat(presets.Select(p => PresetKeyPrefix + p.Id))
                    .ToList();
                root.SetValue("SubCommands", string.Join(";", commands));
            }
        }
        catch
        {
            // 注册表同步失败不影响主流程
        }
    }

    /// <summary>删除全部预设子命令键(HKCU\Software\Classes\FormatConverter.Preset.*)。</summary>
    private static void DeletePresetSubKeys()
    {
        try
        {
            var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classes is null) return;
            foreach (var name in classes.GetSubKeyNames())
            {
                if (!name.StartsWith(PresetKeyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                try { classes.DeleteSubKeyTree(name); } catch { /* 尽力而为 */ }
            }
        }
        catch { /* 尽力而为 */ }
    }
}
