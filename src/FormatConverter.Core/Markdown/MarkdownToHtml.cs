using Markdig;
using Markdig.Renderers;

namespace FormatConverter.Core.Markdown;

/// <summary>md → HTML:直接用 Markdig 内置渲染器,保真度最高。</summary>
public static class MarkdownToHtml
{
    public static string Convert(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.Render(Markdig.Markdown.Parse(markdown, pipeline));
        return writer.ToString();
    }
}
