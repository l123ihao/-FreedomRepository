using FormatConverter.Core.Documents;

namespace FormatConverter.Core.Tests;

public class Ole2ReaderTests : IDisposable
{
    private readonly string _dir;

    public Ole2ReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private string Write(byte[] bytes, string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void RoundTrip_Two_Streams_Including_Multi_Mini_Sector()
    {
        var a = new byte[200]; // 跨多个 64B mini 扇区
        for (var i = 0; i < a.Length; i++) a[i] = (byte)(i * 7);
        var b = "短流内容"u8.ToArray();
        var path = Write(TestOle2Builder.Build(("Alpha", a), ("Beta", b)), "a.ole");

        using var ole = Ole2Reader.Open(path);
        Assert.Equal(a, ole.ReadStream("alpha")); // 名称忽略大小写
        Assert.Equal(b, ole.ReadStream("BETA"));
        Assert.Null(ole.ReadStream("不存在"));
    }

    [Fact]
    public void Non_Ole2_File_Throws()
    {
        var path = Write("plain text"u8.ToArray(), "b.txt");
        Assert.Throws<InvalidDataException>(() => Ole2Reader.Open(path));
    }

    [Fact]
    public void Real_Doc_Reads_WordDocument_And_Table_Streams()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "47304.doc");
        using var ole = Ole2Reader.Open(path);
        var word = ole.ReadStream("WordDocument");
        Assert.NotNull(word);
        Assert.True(word!.Length > 1024);
        var table = ole.ReadStream("0Table") ?? ole.ReadStream("1Table");
        Assert.NotNull(table);
    }
}
