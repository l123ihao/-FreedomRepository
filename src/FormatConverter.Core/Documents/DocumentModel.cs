namespace FormatConverter.Core.Documents;

/// <summary>文档中间模型:docx/md/txt 的读取器统一输出,各导出器共享。</summary>
public abstract record Block;

public sealed record Run(string Text, bool Bold, bool Italic, bool Underline);

public sealed record ParagraphBlock(IReadOnlyList<Run> Runs) : Block;

public sealed record HeadingBlock(int Level, string Text) : Block;

public sealed record ListBlock(IReadOnlyList<string> Items) : Block;

public sealed record TableBlock(IReadOnlyList<IReadOnlyList<string>> Rows) : Block;

public sealed record ImageBlock(byte[] Data, string Extension) : Block;

public sealed record CodeBlock(string Code, string? Language) : Block;

public sealed record DocumentModel(IReadOnlyList<Block> Blocks)
{
    public static readonly DocumentModel Empty = new(Array.Empty<Block>());
}
