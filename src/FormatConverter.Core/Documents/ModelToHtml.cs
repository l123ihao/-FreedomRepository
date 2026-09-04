using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>中间模型 → HTML。png/jpg/gif 图片以 base64 data URI 内嵌。</summary>
public static class ModelToHtml
{
    public static string Convert(DocumentModel model, string title)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
          .Append("<title>").Append(Escape(title)).Append("</title></head><body>").AppendLine();

        foreach (var block in model.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    sb.Append("<h").Append(Math.Clamp(h.Level, 1, 6)).Append('>')
                      .Append(Escape(h.Text)).Append("</h").Append(Math.Clamp(h.Level, 1, 6)).AppendLine(">");
                    break;
                case ParagraphBlock p:
                    sb.Append("<p>").Append(string.Concat(p.Runs.Select(FormatRun))).AppendLine("</p>");
                    break;
                case ListBlock l:
                    sb.AppendLine("<ul>");
                    foreach (var item in l.Items) sb.Append("<li>").Append(Escape(item)).AppendLine("</li>");
                    sb.AppendLine("</ul>");
                    break;
                case TableBlock t when t.Rows.Count > 0:
                    sb.AppendLine("<table border=\"1\">");
                    foreach (var row in t.Rows)
                    {
                        sb.Append("<tr>");
                        foreach (var cell in row) sb.Append("<td>").Append(Escape(cell)).Append("</td>");
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</table>");
                    break;
                case CodeBlock c:
                    sb.Append("<pre><code>").Append(Escape(c.Code)).AppendLine("</code></pre>");
                    break;
                case ImageBlock img:
                    if (MimeFromExtension(img.Extension) is { } mime)
                    {
                        sb.Append("<img src=\"data:").Append(mime).Append(";base64,")
                          .Append(System.Convert.ToBase64String(img.Data)).AppendLine("\">");
                    }
                    else
                    {
                        sb.AppendLine("<p>【图片(" + Escape(img.Extension) + "格式无法嵌入)】</p>");
                    }
                    break;
            }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string FormatRun(Run r)
    {
        var t = Escape(r.Text);
        if (r.Bold) t = "<b>" + t + "</b>";
        if (r.Italic) t = "<i>" + t + "</i>";
        if (r.Underline) t = "<u>" + t + "</u>";
        return t;
    }

    private static string? MimeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "gif" => "image/gif",
        _ => null,
    };

    public static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
