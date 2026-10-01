namespace FormatConverter.Core.Documents;

/// <summary>
/// 定位 LibreOffice(soffice.exe):转换 .doc/.ppt 等老版二进制 Office 格式的可选外部引擎。
/// 查找顺序:应用目录内的便携版(PortableApps 布局,随包分发无需对方安装)→ 常见安装目录 → PATH;
/// 结果进程内缓存(与 HardwareDetector 同策略)。
/// </summary>
public static class LibreOfficeLocator
{
    private static string? _cached;

    /// <summary>soffice.exe 完整路径;未找到返回 null。</summary>
    public static string? Find()
    {
        if (_cached is not null) return _cached.Length == 0 ? null : _cached;
        _cached = Search() ?? "";
        return _cached.Length == 0 ? null : _cached;
    }

    public static bool IsAvailable => Find() is not null;

    private static string? Search()
    {
        // 应用目录内的便携版(PortableApps 布局):LibreOfficePortable\App\libreoffice\program\soffice.exe
        var appBase = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(appBase, "LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"),
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        ];
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // PATH 逐目录查找
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "soffice.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // 非法 PATH 条目跳过
            }
        }
        return null;
    }
}
