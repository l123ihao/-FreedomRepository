using System.Diagnostics;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

/// <summary>
/// LibreOffice 转换器测试:路由测试恒跑;真实转换集成测试在未检测到 soffice 时自动跳过
/// (与 ffmpeg 集成测试同策略,CI 无 LibreOffice 也不报错)。
/// </summary>
public class LibreOfficeConverterTests : IDisposable
{
    private readonly string _dir;

    public LibreOfficeConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private static ConversionJob Job(string source, string target) => new(
        Guid.NewGuid(), source, Path.Combine(Path.GetTempPath(), "out." + target),
        target, new ConversionOptions());

    private static string TestFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", name);

    /// <summary>LibreOffice 缺失时的文本模式回退(装有 LibreOffice 的机器走高保真路径,用例自动跳过)。</summary>
    [Theory]
    [InlineData("47304.doc", "pdf")]
    [InlineData("47304.doc", "docx")]
    [InlineData("47304.doc", "html")]
    [InlineData("42474-1.ppt", "pdf")]
    public async Task TextMode_Fallback_Works_When_LibreOffice_Missing(string file, string target)
    {
        if (LibreOfficeLocator.IsAvailable) return;
        var job = new ConversionJob(Guid.NewGuid(), TestFile(file),
            Path.Combine(_dir, Path.GetFileNameWithoutExtension(file) + "." + target),
            target, new ConversionOptions());
        var result = await new LibreOfficeConverter().ConvertAsync(job, null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Note);
        Assert.True(new FileInfo(result.OutputPath!).Length > 0);
    }

    [Fact]
    public async Task Ppt_To_Pptx_Still_Requires_LibreOffice()
    {
        if (LibreOfficeLocator.IsAvailable) return;
        var job = new ConversionJob(Guid.NewGuid(), TestFile("42474-1.ppt"),
            Path.Combine(_dir, "out.pptx"), "pptx", new ConversionOptions());
        var result = await new LibreOfficeConverter().ConvertAsync(job, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("LibreOffice", result.ErrorMessage);
    }

    [Fact]
    public void CanConvert_Routes_Only_Doc_And_Ppt()
    {
        var converter = new LibreOfficeConverter();
        Assert.True(converter.CanConvert(Job("C:\\f\\a.doc", "pdf")));
        Assert.True(converter.CanConvert(Job("C:\\f\\a.ppt", "pdf")));
        Assert.False(converter.CanConvert(Job("C:\\f\\a.docx", "pdf")));
        Assert.False(converter.CanConvert(Job("C:\\f\\a.pdf", "txt")));
    }

    [Fact]
    public async Task Convert_Doc_To_Docx_When_LibreOffice_Available()
    {
        if (!LibreOfficeLocator.IsAvailable) return; // 无 LibreOffice:跳过

        // 准备 .doc:先用 soffice 把 txt 转成 doc
        var txt = Path.Combine(_dir, "素材.txt");
        await File.WriteAllTextAsync(txt, "测试老版文档转换");
        var doc = Path.Combine(_dir, "素材.doc");
        Assert.Equal(0, Soffice(txt, "doc", _dir));
        Assert.True(File.Exists(doc));

        var job = new ConversionJob(Guid.NewGuid(), doc, Path.Combine(_dir, "out.docx"),
            "docx", new ConversionOptions());
        var result = await new LibreOfficeConverter().ConvertAsync(job, null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(result.OutputPath));
        Assert.True(new FileInfo(result.OutputPath!).Length > 0);
    }

    [Fact]
    public async Task Convert_Doc_To_Pdf_When_LibreOffice_Available()
    {
        if (!LibreOfficeLocator.IsAvailable) return;

        var txt = Path.Combine(_dir, "素材.txt");
        await File.WriteAllTextAsync(txt, "测试老版文档转 PDF");
        var doc = Path.Combine(_dir, "素材.doc");
        Assert.Equal(0, Soffice(txt, "doc", _dir));

        var job = new ConversionJob(Guid.NewGuid(), doc, Path.Combine(_dir, "out.pdf"),
            "pdf", new ConversionOptions());
        var result = await new LibreOfficeConverter().ConvertAsync(job, null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(new FileInfo(result.OutputPath!).Length > 0);
    }

    /// <summary>用 soffice 无头模式生成测试素材(仅集成测试内部用)。</summary>
    private static int Soffice(string source, string filter, string outDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = LibreOfficeLocator.Find()!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add(filter);
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(source);
        using var p = Process.Start(psi)!;
        p.WaitForExit(120_000);
        return p.HasExited ? p.ExitCode : -1;
    }
}
