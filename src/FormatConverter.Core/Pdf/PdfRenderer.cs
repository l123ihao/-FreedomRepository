using FormatConverter.Core.Documents;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FormatConverter.Core.Pdf;

/// <summary>
/// 中间模型 → PDF(QuestPDF)。必须注册中文字体(微软雅黑),否则中文全部渲染为方块。
/// </summary>
public static class PdfRenderer
{
    private const string FontFamily = "Microsoft YaHei";
    private static readonly object Gate = new();
    private static bool _ready;

    public static void EnsureReady()
    {
        if (_ready) return;
        lock (Gate)
        {
            if (_ready) return;
            QuestPDF.Settings.License = LicenseType.Community;
            RegisterFonts();
            _ready = true;
        }
    }

    private static void RegisterFonts()
    {
        // 优先微软雅黑(msyh.ttc 字体集合),用自定义名注册保证按 FontFamily 常量能查到
        foreach (var path in new[] { @"C:\Windows\Fonts\msyh.ttc", @"C:\Windows\Fonts\simhei.ttf" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var fs = File.OpenRead(path);
                FontManager.RegisterFontWithCustomName(FontFamily, fs);
                return;
            }
            catch { /* 尝试下一个字体 */ }
        }
        // 都失败则 PDF 中文会显示为方块
    }

    public static void Render(DocumentModel model, string outputPath)
    {
        EnsureReady();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontFamily(FontFamily).FontSize(11));

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    foreach (var block in model.Blocks)
                        AddBlock(col.Item(), block);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static void AddBlock(IContainer c, Block block)
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                var size = h.Level switch { 1 => 18f, 2 => 15f, 3 => 13f, _ => 12f };
                c.Text(h.Text).FontSize(size).Bold();
                break;
            }
            case ParagraphBlock p:
                c.Text(t =>
                {
                    foreach (var run in p.Runs)
                    {
                        var span = t.Span(run.Text);
                        if (run.Bold) span.Bold();
                        if (run.Italic) span.Italic();
                        if (run.Underline) span.Underline();
                    }
                });
                break;
            case ListBlock l:
                c.Column(col =>
                {
                    col.Spacing(2);
                    foreach (var item in l.Items)
                        col.Item().Text("• " + item);
                });
                break;
            case TableBlock tb when tb.Rows.Count > 0:
                c.Table(table =>
                {
                    var colCount = tb.Rows.Max(r => r.Count);
                    table.ColumnsDefinition(cd =>
                    {
                        for (var i = 0; i < colCount; i++) cd.RelativeColumn();
                    });
                    foreach (var row in tb.Rows)
                    {
                        for (var i = 0; i < colCount; i++)
                        {
                            var text = i < row.Count ? row[i] : "";
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4).Text(text);
                        }
                    }
                });
                break;
            case ImageBlock img:
                if (img.Extension.ToLowerInvariant() is "png" or "jpg" or "jpeg")
                    c.Image(img.Data).FitWidth();
                else
                    c.Text($"【图片({img.Extension} 格式无法嵌入)】").FontColor(Colors.Grey.Medium);
                break;
            case CodeBlock code:
                c.Background(Colors.Grey.Lighten3).Padding(8)
                 .Text(code.Code).FontFamily("Consolas").FontSize(9);
                break;
        }
    }
}
