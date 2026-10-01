namespace FormatConverter.Core.Documents;

/// <summary>老版 Office 文本提取的共享小工具(cp1252 单字节映射,与 NPOI/LibreOffice 处理一致)。</summary>
internal static class LegacyOfficeText
{
    /// <summary>cp1252 的 0x80-0x9F 区间映射(未定义槽位用 U+FFFD);其余字节与 Latin-1 一致。</summary>
    private static readonly char[] Cp1252High =
    [
        '€', '�', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '�', 'Ž', '�',
        '�', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '�', 'ž', 'Ÿ',
    ];

    public static char MapCp1252(byte b) =>
        b < 0x80 ? (char)b
        : b < 0xA0 ? Cp1252High[b - 0x80]
        : (char)b;

    /// <summary>清洗提取出的原始文本:段/换行/单元格控制符 → 换行/制表符,丢弃域标记与其余控制字符。</summary>
    public static string Cleanup(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            switch (c)
            {
                case '\r':      // 段落结束
                case '\u000B':  // 软换行
                case '\f':      // 分页
                    sb.Append('\n');
                    break;
                case '\u0007':  // 单元格分隔
                    sb.Append('\t');
                    break;
                case '\u0013':  // 域 begin
                case '\u0014':  // 域 sep
                case '\u0015':  // 域 end
                case '\0':
                    break;
                default:
                    if (c < ' ' && c != '\t') break;
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
