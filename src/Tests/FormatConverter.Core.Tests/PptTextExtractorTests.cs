using System.Text;
using FormatConverter.Core.Documents;

namespace FormatConverter.Core.Tests;

public class PptTextExtractorTests : IDisposable
{
    private readonly string _dir;

    public PptTextExtractorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    static PptTextExtractorTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private string Write(byte[] bytes, string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string TestFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    /// <summary>构造最小 .ppt:Document 容器 → SlideListWithText 容器 → TextCharsAtom + TextBytesAtom(原子 recVer=0)。</summary>
    private static byte[] BuildPpt()
    {
        var chars = Encoding.Unicode.GetBytes("第一页标题");
        var textChars = Record(4000, chars, container: false);
        var textBytes = Record(4008, Encoding.GetEncoding(1252).GetBytes("Note"), container: false);
        var slideList = Record(4080, Concat(textChars, textBytes), container: true);
        var document = Record(1000, slideList, container: true);
        return TestOle2Builder.Build(("PowerPoint Document", document));
    }

    private static byte[] Record(int type, byte[] body, bool container)
    {
        var rec = new byte[8 + body.Length];
        WriteU16(rec, 0, container ? (ushort)0x000F : (ushort)0x0000); // recVer:容器 0xF,原子 0
        WriteU16(rec, 2, (ushort)type);
        WriteU32(rec, 4, (uint)body.Length);
        body.CopyTo(rec, 8);
        return rec;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var all = new byte[arrays.Sum(a => a.Length)];
        var off = 0;
        foreach (var a in arrays)
        {
            a.CopyTo(all, off);
            off += a.Length;
        }
        return all;
    }

    [Fact]
    public void Synthetic_Extracts_Text_Atoms()
    {
        var path = Write(BuildPpt(), "a.ppt");
        var text = PptTextExtractor.Extract(path);
        Assert.Contains("第一页标题", text);
        Assert.Contains("Note", text);
    }

    [Fact]
    public void Real_Ppt_42474_Extracts_NonEmpty_Text()
    {
        var text = PptTextExtractor.Extract(TestFile("42474-1.ppt"));
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void Real_Ppt_45537_Extracts_NonEmpty_Text()
    {
        var text = PptTextExtractor.Extract(TestFile("45537_Footer.ppt"));
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void Non_Ppt_File_Throws()
    {
        var path = Write("不是文档"u8.ToArray(), "b.txt");
        Assert.Throws<InvalidDataException>(() => PptTextExtractor.Extract(path));
    }

    private static void WriteU16(byte[] buf, int off, ushort value)
    {
        buf[off] = (byte)value;
        buf[off + 1] = (byte)(value >> 8);
    }

    private static void WriteU32(byte[] buf, int off, uint value)
    {
        buf[off] = (byte)value;
        buf[off + 1] = (byte)(value >> 8);
        buf[off + 2] = (byte)(value >> 16);
        buf[off + 3] = (byte)(value >> 24);
    }
}
