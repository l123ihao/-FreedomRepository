using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using FormatConverter.Core.Images;
using FormatConverter.Core.Models;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Converters;

/// <summary>图片互转(ImageSharp):png/jpg/webp/bmp/gif/ico 全组合。</summary>
public sealed class ImageConverter : IConverter
{
    private static readonly HashSet<string> ImageTargets =
        new(StringComparer.OrdinalIgnoreCase) { "png", "jpg", "jpeg", "webp", "bmp", "gif", "ico", "tiff" };

    public bool CanConvert(ConversionJob job) =>
        job.Category == FileCategory.Image && ImageTargets.Contains(job.TargetExtension);

    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // ico 源:ImageSharp 3.1 不支持,先解包(取最大条目)再按普通图像处理
            Image image;
            if (Path.GetExtension(job.SourcePath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await File.ReadAllBytesAsync(job.SourcePath, ct);
                image = IcoCodec.Decode(bytes);
            }
            else
            {
                await using var input = File.OpenRead(job.SourcePath);
                image = await Image.LoadAsync(input, ct); // gif 动画默认只解码首帧
            }
            using (image)
            {
                progress?.Report(new ProgressInfo(0, null, null, null, 0, 0, null));

                // jpg 无透明通道,透明区域铺白底,否则输出偏黑
                if (IsJpeg(job.TargetExtension))
                    FlattenOnWhite(image);

                var dir = Path.GetDirectoryName(job.OutputPath)!;
                Directory.CreateDirectory(dir);

                if (job.TargetExtension.Equals("ico", StringComparison.OrdinalIgnoreCase))
                {
                    var icoBytes = IcoCodec.Encode(image);
                    await File.WriteAllBytesAsync(job.OutputPath, icoBytes, ct);
                }
                else
                {
                    var encoder = GetEncoder(job.TargetExtension);
                    await using var output = File.Create(job.OutputPath);
                    await image.SaveAsync(output, encoder, ct);
                }

                progress?.Report(new ProgressInfo(100, null, null, null, 0, 0, null));
            }

            OutputValidator.EnsureNonEmpty(job.OutputPath);
            return new ConversionResult(job, true, job.OutputPath, null, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            TryDelete(job.OutputPath);
            return new ConversionResult(job, false, null, "已取消", sw.Elapsed);
        }
        catch (Exception ex)
        {
            TryDelete(job.OutputPath);
            return new ConversionResult(job, false, null, ErrorClassifier.WithCategory(ex.Message), sw.Elapsed);
        }
    }

    private static bool IsJpeg(string ext) =>
        ext.Equals("jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals("jpeg", StringComparison.OrdinalIgnoreCase);

    /// <summary>ImageSharp 3.x 移除了 Flatten:先合成到白底,再整幅画回原图。</summary>
    private static void FlattenOnWhite(Image image)
    {
        using var background = new Image<Rgba32>(image.Width, image.Height, Color.White);
        background.Mutate(x => x.DrawImage(image, new Point(0, 0), 1f));
        image.Mutate(x => x.DrawImage(background, new Point(0, 0), 1f));
    }

    private static IImageEncoder GetEncoder(string ext) => ext.ToLowerInvariant() switch
    {
        "png" => new PngEncoder(),
        "jpg" or "jpeg" => new JpegEncoder { Quality = 90 },
        "webp" => new WebpEncoder { Quality = 90 },
        "bmp" => new BmpEncoder(),
        "gif" => new GifEncoder(),
        "tiff" => new TiffEncoder(),
        _ => throw new ArgumentException($"不支持的目标图片格式: {ext}"),
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* 尽力而为 */ }
    }
}
