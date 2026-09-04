using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>中间模型 → Markdown。图片不落地,以注释占位。</summary>
public static class ModelToMarkdown
{
    public static string Convert(DocumentModel model)
    {
        var sb = new StringBuilder();
        foreach (var block in model.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    sb.Append(new string('#', Math.Clamp(h.Level, 1, 6)))
                      .Append(' ').Append(h.Text).AppendLine().AppendLine();
                    break;
                case ParagraphBlock p:
                    sb.AppendLine(string.Concat(p.Runs.Select(FormatRun))).AppendLine();
                    break;
                case ListBlock l:
                    foreach (var item in l.Items) sb.Append("- ").AppendLine(item);
                    sb.AppendLine();
                    break;
                case TableBlock t when t.Rows.Count > 0:
                    var cols = t.Rows.Max(r => r.Count);
                    sb.Append('|')
                      .Append(string.Join(" | ", Enumerable.Repeat("---", cols)))
                      .AppendLine(" |");
                    foreach (var row in t.Rows)
                        sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
                    sb.AppendLine();
                    break;
                case CodeBlock c:
                    sb.Append("```").AppendLine(c.Language ?? "");
                    sb.AppendLine(c.Code);
                    sb.AppendLine("```").AppendLine();
                    break;
                case ImageBlock:
                    sb.AppendLine("<!-- 图片省略 -->");
                    break;
            }
        }
        return sb.ToString();
    }

    private static string FormatRun(Run r)
    {
        var t = r.Text;
        if (r.Bold) t = "**" + t + "**";
        if (r.Italic) t = "*" + t + "*";
        return t;
    }
}
