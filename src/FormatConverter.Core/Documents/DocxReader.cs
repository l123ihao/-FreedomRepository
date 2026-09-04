using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace FormatConverter.Core.Documents;

/// <summary>docx → 中间模型。按 body 子元素顺序遍历,保留段落/标题/列表/表格/图片的先后关系。</summary>
public static class DocxReader
{
    public static DocumentModel Read(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var mainPart = doc.MainDocumentPart
                       ?? throw new InvalidDataException("docx 文档结构无效:缺少 MainDocumentPart。");
        var body = mainPart.Document!.Body!;

        var blocks = new List<Block>();
        var pendingList = new List<string>();

        void FlushList()
        {
            if (pendingList.Count > 0)
            {
                blocks.Add(new ListBlock(pendingList.ToArray()));
                pendingList.Clear();
            }
        }

        foreach (var child in body.ChildElements)
        {
            switch (child)
            {
                case W.Paragraph p:
                {
                    var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                    if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                        && style.Length > "Heading".Length
                        && int.TryParse(style.AsSpan("Heading".Length), out var level)
                        && level is >= 1 and <= 6)
                    {
                        FlushList();
                        blocks.Add(new HeadingBlock(level, p.InnerText.Trim()));
                        break;
                    }

                    if (p.ParagraphProperties?.NumberingProperties is not null)
                    {
                        pendingList.Add(p.InnerText.Trim());
                        break;
                    }

                    if (TryReadImage(mainPart, p) is { } image)
                    {
                        FlushList();
                        blocks.Add(image);
                        break;
                    }

                    FlushList();
                    var runs = ReadRuns(p);
                    if (runs.Count > 0) blocks.Add(new ParagraphBlock(runs));
                    break;
                }
                case W.Table t:
                {
                    FlushList();
                    var rows = t.Elements<W.TableRow>()
                        .Select(r => (IReadOnlyList<string>)r.Elements<W.TableCell>()
                            .Select(c => c.InnerText.Trim()).ToArray())
                        .ToArray();
                    if (rows.Length > 0) blocks.Add(new TableBlock(rows));
                    break;
                }
            }
        }
        FlushList();

        return new DocumentModel(blocks);
    }

    private static IReadOnlyList<Run> ReadRuns(W.Paragraph p)
    {
        var runs = new List<Run>();
        foreach (var r in p.Elements<W.Run>())
        {
            var text = string.Concat(r.Elements<W.Text>().Select(t => t.Text));
            if (text.Length == 0) continue;

            var rp = r.RunProperties;
            runs.Add(new Run(text, IsOn(rp?.Bold), IsOn(rp?.Italic), IsUnderlined(rp?.Underline)));
        }
        return runs;
    }

    /// <summary>提取段落内嵌图片(经 relationship 找 ImagePart,取原始字节)。</summary>
    private static ImageBlock? TryReadImage(MainDocumentPart mainPart, W.Paragraph p)
    {
        var relId = p.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;
        if (relId is null) return null;

        try
        {
            if (mainPart.GetPartById(relId) is not ImagePart imagePart) return null;
            using var stream = imagePart.GetStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return new ImageBlock(ms.ToArray(), ExtensionFromContentType(imagePart.ContentType));
        }
        catch
        {
            return null; // 关系损坏的图片跳过,不影响其余内容
        }
    }

    private static string ExtensionFromContentType(string? contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/bmp" => "bmp",
        "image/tiff" => "tiff",
        "image/x-emf" => "emf",
        "image/x-wmf" => "wmf",
        _ => "png",
    };

    private static bool IsOn(W.OnOffType? onOff) => onOff is null || onOff.Val?.Value != false;

    private static bool IsUnderlined(W.Underline? underline) =>
        underline is not null && underline.Val?.Value != W.UnderlineValues.None;
}
