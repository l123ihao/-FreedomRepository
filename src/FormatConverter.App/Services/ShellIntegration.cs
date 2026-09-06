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
    }
}
