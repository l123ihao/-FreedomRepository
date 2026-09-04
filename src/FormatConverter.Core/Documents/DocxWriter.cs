using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SixLabors.ImageSharp;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace FormatConverter.Core.Documents;

/// <summary>中间模型 → docx。标题用内置 Heading1-3 样式,列表用项目符号段落(不依赖 numbering.xml)。</summary>
public static class DocxWriter
{
    public static void Write(DocumentModel model, string outputPath)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        var body = main.Document.Body!;

        uint imageId = 1;
        foreach (var block in model.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    body.Append(MakeParagraph(h.Text, style: $"Heading{h.Level}"));
                    break;
                case ParagraphBlock p:
                    body.Append(MakeParagraph(p));
                    break;
                case ListBlock l:
                    foreach (var item in l.Items)
                    {
                        body.Append(new W.Paragraph(
                            new W.ParagraphProperties(new W.Indentation { Left = "420" }),
                            new W.Run(new W.Text("• " + item) { Space = SpaceProcessingModeValues.Preserve })));
                    }
                    break;
                case TableBlock t:
                    body.Append(MakeTable(t));
                    break;
                case ImageBlock img:
                    if (TryMakeImageParagraph(main, img, ref imageId) is { } para)
                        body.Append(para);
                    break;
                case CodeBlock c:
                    foreach (var line in c.Code.Replace("\r", "").Split('\n'))
                    {
                        body.Append(new W.Paragraph(
                            new W.ParagraphProperties(new W.ParagraphMarkRunProperties(
                                new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" })),
                            new W.Run(new W.Text(line) { Space = SpaceProcessingModeValues.Preserve })));
                    }
                    break;
            }
        }

        main.Document.Save();
    }

    private static W.Paragraph MakeParagraph(string text, string? style = null)
    {
        var p = new W.Paragraph();
        if (style is not null)
            p.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));
        p.Append(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return p;
    }

    private static W.Paragraph MakeParagraph(ParagraphBlock p)
    {
        var paragraph = new W.Paragraph();
        foreach (var run in p.Runs)
        {
            var r = new W.Run();
            if (run.Bold || run.Italic || run.Underline)
            {
                var rp = new W.RunProperties();
                if (run.Bold) rp.Append(new W.Bold());
                if (run.Italic) rp.Append(new W.Italic());
                if (run.Underline) rp.Append(new W.Underline { Val = W.UnderlineValues.Single });
                r.Append(rp);
            }
            r.Append(new W.Text(run.Text) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(r);
        }
        return paragraph;
    }

    private static W.Table MakeTable(TableBlock t)
    {
        var table = new W.Table(
            new W.TableProperties(
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));

        foreach (var row in t.Rows)
        {
            var tr = new W.TableRow();
            foreach (var cell in row)
            {
                tr.Append(new W.TableCell(new W.Paragraph(
                    new W.Run(new W.Text(cell) { Space = SpaceProcessingModeValues.Preserve }))));
            }
            table.Append(tr);
        }
        return table;
    }

    private static W.Paragraph? TryMakeImageParagraph(MainDocumentPart main, ImageBlock img, ref uint imageId)
    {
        // ImagePartType 是幻型(静态类),不能作变量类型;按扩展名直接分发
        var imagePart = img.Extension.ToLowerInvariant() switch
        {
            "png" => main.AddImagePart(ImagePartType.Png),
            "jpg" or "jpeg" => main.AddImagePart(ImagePartType.Jpeg),
            "gif" => main.AddImagePart(ImagePartType.Gif),
            "bmp" => main.AddImagePart(ImagePartType.Bmp),
            "ico" => main.AddImagePart(ImagePartType.Icon),
            _ => null,
        };
        if (imagePart is null) return null;

        using (var ms = new MemoryStream(img.Data))
            imagePart.FeedData(ms);

        var relId = main.GetIdOfPart(imagePart);

        long cx = 200 * 9525, cy = 150 * 9525;
        try
        {
            using var image = Image.Load(img.Data);
            cx = image.Width * 9525;
            cy = image.Height * 9525;
        }
        catch { /* 尺寸解析失败用默认值 */ }

        var drawing = new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent(),
            new DW.DocProperties { Id = imageId, Name = $"图片{imageId}" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new A.Blip { Embed = relId })
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                }))
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
        };
        imageId++;

        return new W.Paragraph(new W.Run(drawing));
    }
}
