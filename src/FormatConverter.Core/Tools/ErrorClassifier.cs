namespace FormatConverter.Core.Tools;

/// <summary>把失败消息归类为可读原因,用于 UI 展示「[类别] 详情」。</summary>
public static class ErrorClassifier
{
    public static string Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        var m = message;

        if (m == "已取消") return "";

        if (ContainsAny(m,
                "Invalid data", "invalid data", "corrupt", "moov atom not found",
                "not found in the headers", "could not find codec", "header missing",
                "无法打开", "不是有效的"))
            return "输入损坏";

        if (ContainsAny(m,
                "No space left", "disk full", "磁盘空间", "There is not enough space",
                "not enough space"))
            return "磁盘空间不足";

        if (ContainsAny(m,
                "Access is denied", "being used by another process", "拒绝访问",
                "正由另一进程使用", "UnauthorizedAccess", "because it is being used"))
            return "文件被占用或无权限";

        if (ContainsAny(m,
                "Unknown encoder", "Encoder not found", "unable to find a suitable output format",
                "Invalid argument", "Error initializing"))
            return "编码不支持";

        return "";
    }

    /// <summary>给错误消息加分类前缀(无分类或已取消时原样返回)。</summary>
    public static string WithCategory(string? message)
    {
        var m = message ?? "未知错误";
        var category = Classify(m);
        return category.Length > 0 ? $"[{category}] {m}" : m;
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
}
