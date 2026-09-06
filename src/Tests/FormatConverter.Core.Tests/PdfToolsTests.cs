using FormatConverter.Core.Tools;
using UglyToad.PdfPig.Writer;

namespace FormatConverter.Core.Tests;

public class PdfToolsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-pdf-" + Guid.NewGuid().ToString("N"));

    public PdfToolsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string MakePdf(string name, int pages)
    {
        var path = Path.Combine(_dir, name);
        var builder = new PdfDocumentBuilder();
        for (var i = 0; i < pages; i++) builder.AddPage(100, 100);
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    [Fact]
    public void GetPageCount_Returns_Pages()
    {
        var p = MakePdf("a.pdf", 3);
        Assert.Equal(3, PdfTools.GetPageCount(p));
    }

    [Fact]
    public void Merge_Combines_All_Pages_In_Order()
    {
        var a = MakePdf("a.pdf", 2);
        var b = MakePdf("b.pdf", 3);
        var output = Path.Combine(_dir, "merged.pdf");

        PdfTools.Merge(new[] { a, b }, output);

        Assert.True(File.Exists(output));
        Assert.Equal(5, PdfTools.GetPageCount(output));
    }

    [Fact]
    public void SplitByRanges_Produces_Range_Files()
    {
        var input = MakePdf("in.pdf", 5);
        var outDir = Path.Combine(_dir, "split");

        var outputs = PdfTools.SplitByRanges(input, outDir,
            new[] { new PageRange(1, 2), new PageRange(4, 5) });

        Assert.Equal(2, outputs.Count);
        Assert.Equal(2, PdfTools.GetPageCount(outputs[0]));
        Assert.Equal(2, PdfTools.GetPageCount(outputs[1]));
        Assert.EndsWith("in_p1-2.pdf", outputs[0]);
        Assert.EndsWith("in_p4-5.pdf", outputs[1]);
    }

    [Fact]
    public void SplitEveryN_Produces_Chunks()
    {
        var input = MakePdf("in.pdf", 5);
        var outDir = Path.Combine(_dir, "chunks");

        var outputs = PdfTools.SplitEveryN(input, outDir, 2);

        Assert.Equal(3, outputs.Count);
        Assert.Equal(2, PdfTools.GetPageCount(outputs[0]));
        Assert.Equal(2, PdfTools.GetPageCount(outputs[1]));
        Assert.Equal(1, PdfTools.GetPageCount(outputs[2]));
    }

    [Fact]
    public void SplitByRanges_Clamps_Out_Of_Range()
    {
        var input = MakePdf("in.pdf", 5);
        var outDir = Path.Combine(_dir, "clamp");

        var outputs = PdfTools.SplitByRanges(input, outDir, new[] { new PageRange(3, 99) });

        Assert.Single(outputs);
        Assert.Equal(3, PdfTools.GetPageCount(outputs[0])); // 3..5 共 3 页
    }
}
