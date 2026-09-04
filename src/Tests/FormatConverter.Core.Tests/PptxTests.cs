using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Models;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace FormatConverter.Core.Tests;

/// <summary>pptx 来源:读取器与转换路由测试(测试内用 OpenXML 自造最小演示文稿)。</summary>
public class PptxTests
{
    [Fact]
    public void Read_Extracts_SlideTitles_And_BodyParagraphs()
    {
        var path = CreateTestPptx();
        try
        {
            var model = PptxReader.Read(path);
            var heading = Assert.Single(model.Blocks.OfType<HeadingBlock>()); // 第二页没有标题占位符
            Assert.Equal("第一页标题", heading.Text);

            var paragraphs = model.Blocks.OfType<ParagraphBlock>().ToList();
            Assert.Equal(3, paragraphs.Count);
            Assert.Contains(paragraphs, p => p.Runs[0].Text == "正文段落一");
            Assert.Contains(paragraphs, p => p.Runs[0].Text == "正文段落二");
            Assert.Contains(paragraphs, p => p.Runs[0].Text == "第二页只有正文");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Pptx_To_Txt_Through_Factory()
    {
        var src = CreateTestPptx();
        var outPath = Path.Combine(Path.GetTempPath(), $"pptx-out-{Guid.NewGuid():N}.txt");
        try
        {
            var job = new ConversionJob(Guid.NewGuid(), src, outPath, "txt", new ConversionOptions());
            var converter = ConverterFactory.CreateDefault().GetConverter(job);
            Assert.NotNull(converter);
            var result = await converter!.ConvertAsync(job, null, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            var text = File.ReadAllText(outPath);
            Assert.Contains("第一页标题", text);
            Assert.Contains("正文段落一", text);
        }
        finally
        {
            if (File.Exists(src)) File.Delete(src);
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    // ---------- 测试素材:最小可读的 pptx(无母版/版式,仅 OpenXML 读取器可消费) ----------

    private static string CreateTestPptx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pptx-test-{Guid.NewGuid():N}.pptx");
        using (var doc = PresentationDocument.Create(path, PresentationDocumentType.Presentation))
        {
            var part = doc.AddPresentationPart();
            part.Presentation = new Presentation();
            AddSlide(part, "第一页标题", "正文段落一", "正文段落二");
            AddSlide(part, null, "第二页只有正文");
            doc.Save();
        }
        return path;
    }

    private static void AddSlide(PresentationPart part, string? title, params string[] bodyParagraphs)
    {
        var slidePart = part.AddNewPart<SlidePart>();
        slidePart.Slide = new Slide
        {
            CommonSlideData = new CommonSlideData { ShapeTree = new ShapeTree() },
        };
        var tree = slidePart.Slide.CommonSlideData.ShapeTree;
        if (title is not null)
            tree.Append(MakeShape(title, "标题", PlaceholderValues.Title));
        foreach (var p in bodyParagraphs)
            tree.Append(MakeShape(p, "正文", PlaceholderValues.Body));

        var list = part.Presentation!.SlideIdList ??= new SlideIdList();
        list.Append(new SlideId
        {
            Id = (uint)(256 + list.Elements<SlideId>().Count()),
            RelationshipId = part.GetIdOfPart(slidePart),
        });
    }

    private static Shape MakeShape(string text, string name, PlaceholderValues placeholderType)
    {
        return new Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 1U, Name = name },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties
                {
                    PlaceholderShape = new PlaceholderShape { Type = placeholderType },
                }),
            new ShapeProperties(),
            new TextBody(new Drawing.Paragraph(new Drawing.Run(new Drawing.Text(text)))));
    }
}
