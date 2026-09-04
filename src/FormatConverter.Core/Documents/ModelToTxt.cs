using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>中间模型 → 纯文本。</summary>
public static class ModelToTxt
{
    public static string Convert(DocumentModel model)
    {
        var sb = new StringBuilder();
        foreach (var block in model.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    sb.Append(h.Text).AppendLine().AppendLine();
                    break;
                case ParagraphBlock p:
                    sb.AppendLine(string.Concat(p.Runs.Select(r => r.Text)));
                    break;
                case ListBlock l:
                    foreach (var item in l.Items) sb.Append("- ").AppendLine(item);
                    break;
                case TableBlock t:
                    foreach (var row in t.Rows) sb.AppendLine(string.Join('\t', row));
                    break;
                case CodeBlock c:
                    sb.AppendLine(c.Code);
                    break;
                case ImageBlock:
                    sb.AppendLine("【图片】");
                    break;
            }
        }
        return sb.ToString();
    }
}
