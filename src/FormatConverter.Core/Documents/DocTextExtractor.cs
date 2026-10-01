using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>
/// 老版 .doc(Word 97-2003 二进制)文本提取:
/// OLE2 → WordDocument 流 → FIB(0x01A2 fcClx)→ CLX → PlcPcd 片段表 → 按片段解码(cp1252/UTF-16)。
/// 解析思路对照 NPOI HWPF(TextPieceTable/ComplexFileTable)与 LibreOffice ww8 过滤器;只取文本,不解析格式。
/// </summary>
public static class DocTextExtractor
{
    public static string Extract(string path)
    {
        using var ole = Ole2Reader.Open(path);
        var word = ole.ReadStream("WordDocument")
            ?? throw new InvalidDataException("不是有效的 Word 文档:缺少 WordDocument 流。");
        if (word.Length < 0x1AC)
            throw new InvalidDataException("WordDocument 流不完整,文件已损坏。");
        var wIdent = Ole2Reader.ReadU16(word, 0);
        if (wIdent == 0xA5DC)
            throw new InvalidDataException("文档为 Word 6/95 早期格式,请另存为 Word 97-2003(.doc)或 .docx 后重试。");
        if (wIdent != 0xA5EC)
            throw new InvalidDataException("不是 Word 97-2003 文档。");

        var nFib = Ole2Reader.ReadU16(word, 2);
        if (nFib < 0x00C1)
            throw new InvalidDataException("文档为 Word 6/95 早期格式,请另存为 Word 97-2003(.doc)或 .docx 后重试。");

        var flags = Ole2Reader.ReadU16(word, 0x0A);
        if ((flags & 0x0100) != 0)
            throw new InvalidDataException("文档已加密,无法提取文本。");

        var ccpText = Ole2Reader.ReadU32(word, 0x4C);
        var fcClx = Ole2Reader.ReadU32(word, 0x01A2);
        var lcbClx = Ole2Reader.ReadU32(word, 0x01A6);
        var tableName = (flags & 0x0200) != 0 ? "1Table" : "0Table";
        var table = ole.ReadStream(tableName)
            ?? throw new InvalidDataException($"文档缺少表流 {tableName},文件已损坏。");

        var pieces = ParseClx(table, fcClx, lcbClx);
        if (pieces.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var p in pieces)
        {
            var chars = (int)Math.Min(p.CpEnd, ccpText) - (int)p.CpStart;
            if (chars <= 0 || p.Fc >= word.Length) continue;
            if (p.Compressed)
            {
                var count = Math.Min(chars, word.Length - p.Fc);
                for (var i = 0; i < count; i++)
                    sb.Append(LegacyOfficeText.MapCp1252(word[p.Fc + i]));
            }
            else
            {
                var count = Math.Min(chars * 2, word.Length - p.Fc) & ~1;
                sb.Append(Encoding.Unicode.GetString(word, p.Fc, count));
            }
        }
        return LegacyOfficeText.Cleanup(sb.ToString());
    }

    private sealed record Piece(uint CpStart, uint CpEnd, int Fc, bool Compressed);

    /// <summary>解析 CLX:跳过 Prc(0x01)块,取 PlcPcd(0x02)片段表(n+1 个 CP + n 个 8B PCD)。
    /// 边界按表流长度串行推进(lcbClx 只作参考——它不含 5 字节 CLX 标记头,直接裁剪会截掉 PCD 表)。</summary>
    private static List<Piece> ParseClx(byte[] table, uint fcClx, uint lcbClx)
    {
        var pieces = new List<Piece>();
        if (fcClx >= table.Length) return pieces;
        var pos = (int)fcClx;
        var end = table.Length;

        while (pos + 1 <= end)
        {
            var type = table[pos];
            if (type == 0x01)
            {
                if (pos + 3 > end) break;
                var cb = Ole2Reader.ReadU16(table, pos + 1);
                pos += 3 + cb;
            }
            else if (type == 0x02)
            {
                if (pos + 5 > end) break;
                var lcb = Ole2Reader.ReadU32(table, pos + 1);
                var pcdStart = pos + 5;
                var pcdEnd = (int)Math.Min(pcdStart + (long)lcb, end);
                var n = (pcdEnd - pcdStart - 4) / 12;
                if (n <= 0) break;
                var cpBase = pcdStart;
                var pcdBase = pcdStart + (n + 1) * 4;
                for (var i = 0; i < n; i++)
                {
                    var cpStart = Ole2Reader.ReadU32(table, cpBase + i * 4);
                    var cpEnd = Ole2Reader.ReadU32(table, cpBase + (i + 1) * 4);
                    var fcRaw = Ole2Reader.ReadU32(table, pcdBase + i * 8 + 2);
                    var compressed = (fcRaw & 0x40000000) != 0;
                    var fc = (int)(fcRaw & 0x3FFFFFFF);
                    if (!compressed) fc &= ~1;
                    if (cpEnd > cpStart)
                        pieces.Add(new Piece(cpStart, cpEnd, fc, compressed));
                }
                break; // PlcPcd 是 CLX 的末段
            }
            else
            {
                break;
            }
        }
        return pieces;
    }
}
