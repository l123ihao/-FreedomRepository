using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>文本文件读写:UTF-8 无 BOM 优先,读入时兼容 BOM 与 GB18030(国内 txt 常见编码)。</summary>
public static class TextFile
{
    static TextFile() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // BOM 检测
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        // 严格 UTF-8 解码失败 → GB18030
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

    public static void WriteAllText(string path, string content) =>
        File.WriteAllText(path, content, new UTF8Encoding(false));
}
