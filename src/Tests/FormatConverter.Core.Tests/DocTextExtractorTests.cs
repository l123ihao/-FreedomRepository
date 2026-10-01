using System.Text;
using FormatConverter.Core.Documents;

namespace FormatConverter.Core.Tests;

public class DocTextExtractorTests : IDisposable
{
    private readonly string _dir;

    public DocTextExtractorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    static DocTextExtractorTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private string Write(byte[] bytes, string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string TestFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    /// <summary>构造最小 Word 97 .doc:WordDocument(FIB+文本@0x600)+ 1Table(CLX 单片段)。</summary>
    private static byte[] BuildDoc(string text, bool compressed)
    {
        var textBytes = compressed
            ? Encoding.GetEncoding(1252).GetBytes(text)
            : Encoding.Unicode.GetBytes(text);

        var word = new byte[0x600 + textBytes.Length];
        word[0] = 0xEC; word[1] = 0xA5;            // wIdent
        word[2] = 0xC1; word[3] = 0x00;            // nFib(Word 97)
        word[0x0A] = 0x00; word[0x0B] = 0x02;      // flags:fWhichTblStm → 1Table
        WriteU32(word, 0x4C, (uint)text.Length);   // ccpText
        WriteU32(word, 0x01A2, 0);                 // fcClx → 1Table 偏移 0
        WriteU32(word, 0x01A6, 20);                // lcbClx
        textBytes.CopyTo(word, 0x600);

        var clx = new byte[1 + 4 + 4 * 2 + 8];
        clx[0] = 0x02;                             // PlcPcd 标记
        WriteU32(clx, 1, 20);                      // lcb
        WriteU32(clx, 5, 0);                       // CP[0]
        WriteU32(clx, 9, (uint)text.Length);       // CP[1]
        WriteU32(clx, 13 + 2, compressed ? 0x600u | 0x40000000u : 0x600u); // PCD.fc
        // flags/prm 默认 0

        return TestOle2Builder.Build(("WordDocument", word), ("1Table", clx));
    }

    [Fact]
    public void Synthetic_Utf16_Piece_Extracts_And_Cleans_Text()
    {
        var path = Write(BuildDoc("你好,World!\r第二段", compressed: false), "a.doc");
        var text = DocTextExtractor.Extract(path);
        Assert.Equal("你好,World!\n第二段", text); // \r 段落符 → 换行
    }

    private static void WriteU32(byte[] buf, int off, uint value)
    {
        buf[off] = (byte)value;
        buf[off + 1] = (byte)(value >> 8);
        buf[off + 2] = (byte)(value >> 16);
        buf[off + 3] = (byte)(value >> 24);
    }
}
