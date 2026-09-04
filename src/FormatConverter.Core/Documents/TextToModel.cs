namespace FormatConverter.Core.Documents;

/// <summary>纯文本 → 中间模型:每个非空行一个段落,保留换行结构。</summary>
public static class TextToModel
{
    public static DocumentModel Convert(string text)
    {
        var blocks = new List<Block>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Length == 0) continue;
            blocks.Add(new ParagraphBlock(new[] { new Run(raw, false, false, false) }));
        }
        return new DocumentModel(blocks);
    }
}
