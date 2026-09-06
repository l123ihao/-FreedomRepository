using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FormatConverter.Core.Tools;

public enum WatermarkPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}

/// <summary>压缩:Quality(1-100,jpg/webp),MaxWidth/MaxHeight(0 = 不限,保持比例)。</summary>
public sealed record CompressOptions(int Quality = 85, int MaxWidth = 0, int MaxHeight = 0);

/// <summary>缩放:Percent 优先;否则 Width/Height(只给一个则保持比例,两个都给则拉伸)。</summary>
public sealed record ResizeOptions(int? Width = null, int? Height = null, int? Percent = null);

/// <summary>裁剪:cover 语义(先缩放覆盖目标,再居中裁出 Width×Height)。</summary>
public sealed record CropOptions(int Width, int Height);

/// <summary>水印:Text 与 ImagePath 二选一(Text 优先)。</summary>
public sealed record WatermarkOptions(
    string? Text = null,
    string? ImagePath = null,
    WatermarkPosition Position = WatermarkPosition.BottomRight,
    int FontSize = 24,
    string FontColorHex = "#FFFFFF",
    float Opacity = 0.7f);

/// <summary>图片工具(ImageSharp):压缩 / 缩放 / 裁剪 / 文字与图片水印。</summary>
public static class ImageTools
{
    public static async Task<string> CompressAsync(
        string input, string output, CompressOptions options, CancellationToken ct = default)
    {
        await using var inStream = File.OpenRead(input);
        using var image = await Image.LoadAsync(inStream, ct);

        if (options.MaxWidth > 0 || options.MaxHeight > 0)
        {
            image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(
                    options.MaxWidth > 0 ? options.MaxWidth : int.MaxValue,
                    options.MaxHeight > 0 ? options.MaxHeight : int.MaxValue),
            }));
        }

        await SaveAsync(image, output, options.Quality, ct);
        return output;
    }

    public static async Task<string> ResizeAsync(
        string input, string output, ResizeOptions options, CancellationToken ct = default)
    {
        await using var inStream = File.OpenRead(input);
        using var image = await Image.LoadAsync(inStream, ct);

        if (options.Percent is int percent)
        {
            var w = Math.Max(1, (int)Math.Round(image.Width * percent / 100.0));
            var h = Math.Max(1, (int)Math.Round(image.Height * percent / 100.0));
            image.Mutate(x => x.Resize(w, h));
        }
        else if (options.Width is int w && options.Height is int h)
        {
            image.Mutate(x => x.Resize(w, h));
        }
        else if (options.Width is int widthOnly)
        {
            image.Mutate(x => x.Resize(widthOnly, 0)); // 0 = 按比例
        }
        else if (options.Height is int heightOnly)
        {
            image.Mutate(x => x.Resize(0, heightOnly));
        }

        await SaveAsync(image, output, 90, ct);
        return output;
    }

    public static async Task<string> CropAsync(
        string input, string output, CropOptions options, CancellationToken ct = default)
    {
        await using var inStream = File.OpenRead(input);
        using var image = await Image.LoadAsync(inStream, ct);

        image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
        {
            Mode = ResizeMode.Crop,
            Size = new Size(options.Width, options.Height),
        }));

        await SaveAsync(image, output, 90, ct);
        return output;
    }

    public static async Task<string> WatermarkAsync(
        string input, string output, WatermarkOptions options, CancellationToken ct = default)
    {
        await using var inStream = File.OpenRead(input);
        using var image = await Image.LoadAsync(inStream, ct);

        if (options.Text is not null)
        {
            DrawTextWatermark(image, options);
        }
        else if (options.ImagePath is not null)
        {
            await DrawImageWatermarkAsync(image, options.ImagePath, options, ct);
        }

        await SaveAsync(image, output, 95, ct);
        return output;
    }

    // ---------- 内部 ----------

    private static void DrawTextWatermark(Image image, WatermarkOptions options)
    {
        var font = CreateFont(options.FontSize);
        var color = Color.ParseHex(options.FontColorHex);
        var size = TextMeasurer.MeasureSize(options.Text!, new TextOptions(font));
        var pos = GetPosition(image.Width, image.Height, (int)size.Width, (int)size.Height, options.Position);
        image.Mutate(x => x.DrawText(options.Text!, font, color, new PointF(pos.X, pos.Y)));
    }

    private static async Task DrawImageWatermarkAsync(
        Image image, string watermarkPath, WatermarkOptions options, CancellationToken ct)
    {
        await using var ws = File.OpenRead(watermarkPath);
        using var watermark = await Image.LoadAsync(ws, ct);

        // 水印宽度为主图 1/4,高度按比例
        var wmWidth = Math.Max(1, image.Width / 4);
        var wmHeight = Math.Max(1, (int)Math.Round(watermark.Height * (wmWidth / (double)watermark.Width)));
        watermark.Mutate(x => x.Resize(wmWidth, wmHeight));

        var pos = GetPosition(image.Width, image.Height, wmWidth, wmHeight, options.Position);
        image.Mutate(x => x.DrawImage(watermark, new Point(pos.X, pos.Y), options.Opacity));
    }

    private static Point GetPosition(int bgW, int bgH, int w, int h, WatermarkPosition position)
    {
        const int margin = 12;
        return position switch
        {
            WatermarkPosition.TopLeft => new Point(margin, margin),
            WatermarkPosition.TopRight => new Point(bgW - w - margin, margin),
            WatermarkPosition.BottomLeft => new Point(margin, bgH - h - margin),
            WatermarkPosition.BottomRight => new Point(bgW - w - margin, bgH - h - margin),
            _ => new Point((bgW - w) / 2, (bgH - h) / 2),
        };
    }

    private static Font CreateFont(float size)
    {
        foreach (var name in new[] { "Microsoft YaHei", "SimHei", "Segoe UI", "Arial" })
        {
            try
            {
                return SystemFonts.CreateFont(name, size);
            }
            catch
            {
                // 尝试下一个字体
            }
        }
        return SystemFonts.Families.First().CreateFont(size);
    }

    private static async Task SaveAsync(
        Image image, string output, int quality, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var outStream = File.Create(output);
        await image.SaveAsync(outStream, GetEncoder(output, quality), ct);
    }

    private static IImageEncoder GetEncoder(string path, int quality)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) },
            "webp" => new WebpEncoder { Quality = Math.Clamp(quality, 1, 100) },
            "bmp" => new BmpEncoder(),
            "gif" => new GifEncoder(),
            _ => new PngEncoder(),
        };
    }
}
