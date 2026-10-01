using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>
/// 老版 .ppt(PowerPoint 97-2003 二进制)文本提取:
/// OLE2 → PowerPoint Document 流 → 递归遍历记录树,收集 TextCharsAtom(4000,UTF-16)与
/// TextBytesAtom(4008,单字节 cp1252)文本。解析思路对照 NPOI HSLF 与 LibreOffice ppt 过滤器。
/// </summary>
public static class PptTextExtractor
{
    private const int TextCharsAtom = 4000;
    private const int TextBytesAtom = 4008;

    public static string Extract(string path)
    {
        using var ole = Ole2Reader.Open(path);
        var doc = ole.ReadStream("PowerPoint Document")
            ?? throw new InvalidDataException("不是有效的 PPT 文档:缺少 PowerPoint Document 流。");

        var texts = new List<string>();
        Walk(doc, 0, doc.Length, texts, 0);

        // 相邻去重(同一文本可能在多级容器中重复出现)
        var result = new List<string>();
        foreach (var t in texts)
            if (result.Count == 0 || !string.Equals(result[^1], t, StringComparison.Ordinal))
                result.Add(t);
        return LegacyOfficeText.Cleanup(string.Join("\n", result).Trim());
    }

    /// <summary>记录头 8B:verInst(recVer=低 4 位,0xF 为容器)/ type / len;容器递归,越界即停。</summary>
    private static void Walk(byte[] data, int start, int end, List<string> texts, int depth)
    {
        if (depth > 12) return;
        var pos = start;
        while (pos + 8 <= end)
        {
            var verInst = Ole2Reader.ReadU16(data, pos);
            var type = Ole2Reader.ReadU16(data, pos + 2);
            var len = (int)Ole2Reader.ReadU32(data, pos + 4);
            if (len < 0 || pos + 8 + len > end) break;
            var body = pos + 8;

            if (type == TextCharsAtom && len >= 2)
            {
                var text = Encoding.Unicode.GetString(data, body, len & ~1).TrimEnd('\0').Trim();
                if (text.Length > 0) texts.Add(text);
            }
            else if (type == TextBytesAtom && len >= 1)
            {
                var sb = new StringBuilder(len);
                for (var i = 0; i < len; i++)
                    sb.Append(LegacyOfficeText.MapCp1252(data[body + i]));
                var text = sb.ToString().TrimEnd('\0').Trim();
                if (text.Length > 0) texts.Add(text);
            }

            if ((verInst & 0x0F) == 0x0F)
                Walk(data, body, body + len, texts, depth + 1);
            pos = body + len;
        }
    }
}
