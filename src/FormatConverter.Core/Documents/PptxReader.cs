using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace FormatConverter.Core.Documents;

/// <summary>
/// PPTX 读取器:把演示文稿提取为文档中间模型(复用 docx/txt/pdf 的既有导出链)。
/// 每页:标题占位符 → 一级标题,其余文本框按段落提取。
/// 已知限制:表格/图表等非纯文本形状暂不提取;旧版 .ppt 二进制格式不支持(由 FormatRegistry 拒收)。
/// </summary>
public static class PptxReader
{
    public static DocumentModel Read(string path)
    {
        var blocks = new List<Block>();
        using var doc = PresentationDocument.Open(path, false);
        var presentationPart = doc.PresentationPart
            ?? throw new InvalidDataException("不是有效的 PPTX 文件:缺少演示文稿部件。");
        var slideIds = presentationPart.Presentation?.SlideIdList?
            .Elements<SlideId>().ToList() ?? new List<SlideId>();

        foreach (var slideId in slideIds)
        {
            if (slideId.RelationshipId?.Value is not { } relId) continue;
            if (presentationPart.GetPartById(relId) is not SlidePart slidePart) continue;
            if (slidePart.Slide?.CommonSlideData?.ShapeTree is not { } tree) continue;

            var hasHeading = false;
            foreach (var shape in tree.Elements<Shape>())
            {
                var placeholderType = shape.NonVisualShapeProperties?
                    .ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type;
                var isTitle = placeholderType is not null &&
                              (placeholderType.Value == PlaceholderValues.Title ||
                               placeholderType.Value == PlaceholderValues.CenteredTitle);

                if (isTitle)
                {
                    if (!hasHeading)
                    {
                        var titleText = ExtractText(shape.TextBody);
                        if (!string.IsNullOrWhiteSpace(titleText))
                        {
                            blocks.Add(new HeadingBlock(1, titleText));
                            hasHeading = true;
                        }
                    }
                    continue; // 标题文本不再重复进正文
                }

                foreach (var paragraph in ExtractParagraphs(shape.TextBody))
                    blocks.Add(paragraph);
            }
        }

        if (blocks.Count == 0)
            blocks.Add(new ParagraphBlock(new[] { new Run("(演示文稿中未找到文本内容)", false, false, false) }));
        return new DocumentModel(blocks);
    }

    /// <summary>把文本框全部段落合并为一段文本(标题用)。</summary>
    private static string? ExtractText(TextBody? body)
    {
        if (body is null) return null;
        var lines = body.Elements<Drawing.Paragraph>()
            .Select(ParagraphText)
            .Where(s => s.Length > 0);
        return string.Join(" ", lines);
    }

    /// <summary>逐段提取文本框内容(正文用)。</summary>
    private static IEnumerable<ParagraphBlock> ExtractParagraphs(TextBody? body)
    {
        if (body is null) yield break;
        foreach (var p in body.Elements<Drawing.Paragraph>())
        {
            var text = ParagraphText(p);
            if (text.Length > 0)
                yield return new ParagraphBlock(new[] { new Run(text, false, false, false) });
        }
    }

    /// <summary>
    /// 取一个 a:p 段落的文本:拼接其中所有 a:t 文本节点。
    /// 按本地名匹配(而非类型),同时容忍标准 a:r 运行包装与某些生成器写出的裸 a:t。
    /// </summary>
    private static string ParagraphText(Drawing.Paragraph paragraph)
        => string.Concat(paragraph.Descendants().Where(e => e.LocalName == "t").Select(e => e.InnerText)).Trim();
}
