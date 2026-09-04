using System.Text;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Markdown;

namespace FormatConverter.Core.Tests;

public class DocumentConversionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));

    public DocumentConversionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private string PathIn(string name) => Path.Combine(_dir, name);

    [Fact]
    public void Text_To_Docx_RoundTrip_Preserves_Chinese()
    {
        var model = TextToModel.Convert("第一行内容\n\n第二行内容。");
        var docx = PathIn("out.docx");
        DocxWriter.Write(model, docx);

        Assert.True(File.Exists(docx));
        var read = DocxReader.Read(docx);
        var back = ModelToTxt.Convert(read);
        Assert.Contains("第一行内容", back);
        Assert.Contains("第二行内容。", back);
    }

    [Fact]
    public void Markdown_Headings_And_Lists_Map_To_Model()
    {
        var model = MarkdownToModel.Convert("# 标题一\n\n- 项目甲\n- 项目乙\n\n## 标题二\n\n正文段落");
        Assert.Contains(model.Blocks, b => b is HeadingBlock { Level: 1, Text: "标题一" });
        Assert.Contains(model.Blocks, b => b is HeadingBlock { Level: 2, Text: "标题二" });
        Assert.Contains(model.Blocks, b => b is ListBlock l && l.Items.SequenceEqual(new[] { "项目甲", "项目乙" }));
    }

    [Fact]
    public void Markdown_To_Html_Uses_Markdig_Renderer()
    {
        var html = MarkdownToHtml.Convert("# 你好\n\n**加粗**文本");
        Assert.Contains("<h1", html);
        Assert.Contains("你好", html);
        Assert.Contains("<strong>加粗</strong>", html);
    }

    [Fact]
    public void Model_To_Html_Escapes_Specials()
    {
        Assert.Equal("&lt;a&gt; &amp; &quot;b&quot;", ModelToHtml.Escape("<a> & \"b\""));
        var html = ModelToHtml.Convert(
            new DocumentModel(new Block[] { new ParagraphBlock(new[] { new Run("<script>", false, false, false) }) }),
            "t");
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Model_To_Markdown_Formats_Bold_And_Headings()
    {
        var model = new DocumentModel(new Block[]
        {
            new HeadingBlock(2, "章节"),
            new ParagraphBlock(new[] { new Run("普通", false, false, false), new Run("加粗", true, false, false) }),
        });
        var md = ModelToMarkdown.Convert(model);
        Assert.Contains("## 章节", md);
        Assert.Contains("**加粗**", md);
    }

    [Fact]
    public void TextFile_Falls_Back_To_Gb18030_For_Ansi_Chinese()
    {
        // 触发 TextFile 静态构造,注册 CodePages provider
        var path = PathIn("gb.txt");
        var gb = Encoding.GetEncoding("GB18030");
        File.WriteAllBytes(path, gb.GetBytes("中文编码测试"));

        Assert.Equal("中文编码测试", TextFile.ReadAllText(path));
    }

    [Fact]
    public void TextFile_Handles_Utf8_Bom()
    {
        var path = PathIn("bom.txt");
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("带BOM内容")).ToArray();
        File.WriteAllBytes(path, bytes);

        Assert.Equal("带BOM内容", TextFile.ReadAllText(path));
        Assert.DoesNotContain('﻿', TextFile.ReadAllText(path));
    }
}
