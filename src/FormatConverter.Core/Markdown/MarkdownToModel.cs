using System.Text;
using FormatConverter.Core.Documents;
using Markdig;
using Md = Markdig.Syntax;
using Inlines = Markdig.Syntax.Inlines;

namespace FormatConverter.Core.Markdown;

/// <summary>Markdig AST → 中间模型。inline 格式(粗体等)简化为纯文本,结构(标题/列表/表格/代码)保留。</summary>
public static class MarkdownToModel
{
    public static DocumentModel Convert(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var doc = Markdig.Markdown.Parse(markdown, pipeline);
        var blocks = new List<Block>();

        foreach (var node in doc)
        {
            switch (node)
            {
                case Md.HeadingBlock h:
                    blocks.Add(new HeadingBlock(h.Level, ExtractText(h.Inline)));
                    break;
                case Md.ListBlock l:
                    blocks.Add(new ListBlock(ExtractItems(l)));
                    break;
                case Md.FencedCodeBlock f:
                    blocks.Add(new CodeBlock(ExtractCode(f), f.Info));
                    break;
                case Md.CodeBlock c:
                    blocks.Add(new CodeBlock(ExtractCode(c), null));
                    break;
                case Md.QuoteBlock q:
                    foreach (var child in q)
                    {
                        if (child is Md.ParagraphBlock cp)
                            blocks.Add(new ParagraphBlock(new[]
                                { new Run("> " + ExtractText(cp.Inline), false, false, false) }));
                    }
                    break;
                case Md.ParagraphBlock p:
                    blocks.Add(new ParagraphBlock(new[]
                        { new Run(ExtractText(p.Inline), false, false, false) }));
                    break;
            }
        }

        return new DocumentModel(blocks);
    }

    private static string ExtractCode(Md.CodeBlock code)
    {
        var sb = new StringBuilder();
        foreach (var line in code.Lines.Lines) sb.AppendLine(line.Slice.ToString());
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static IReadOnlyList<string> ExtractItems(Md.ListBlock list)
    {
        var items = new List<string>();
        foreach (var child in list)
        {
            if (child is not Md.ListItemBlock item) continue;

            var parts = new List<string>();
            foreach (var inner in item)
            {
                switch (inner)
                {
                    case Md.ParagraphBlock p:
                        parts.Add(ExtractText(p.Inline));
                        break;
                    case Md.ListBlock nested:
                        foreach (var n in ExtractItems(nested)) parts.Add("    - " + n);
                        break;
                }
            }
            items.Add(string.Join(" ", parts.Where(x => x.Length > 0)));
        }
        return items;
    }

    private static string ExtractText(Inlines.ContainerInline? container)
    {
        if (container is null) return "";
        var sb = new StringBuilder();
        Walk(container, sb);
        return sb.ToString();
    }

    private static void Walk(Inlines.ContainerInline container, StringBuilder sb)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case Inlines.LiteralInline lit:
                    sb.Append(lit.Content.ToString());
                    break;
                case Inlines.CodeInline code:
                    sb.Append(code.Content);
                    break;
                case Inlines.LineBreakInline:
                    sb.Append('\n');
                    break;
                case Inlines.ContainerInline child:
                    Walk(child, sb);
                    break;
            }
        }
    }
}
