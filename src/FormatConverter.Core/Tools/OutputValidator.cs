namespace FormatConverter.Core.Tools;

/// <summary>转换后输出校验:文件必须存在且非 0 字节,否则视为失败。</summary>
public static class OutputValidator
{
    public static void EnsureNonEmpty(string path)
    {
        if (!File.Exists(path))
            throw new IOException("输出文件未生成。");
        if (new FileInfo(path).Length == 0)
            throw new IOException("输出文件为空。");
    }
}
