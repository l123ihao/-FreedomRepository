using FormatConverter.Core.Converters;
using FormatConverter.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FormatConverter.Core.Tests;

public class ImageConversionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));

    public ImageConversionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private string PathIn(string name) => Path.Combine(_dir, name);

    private static ConversionJob Job(string src, string targetExt, string output) =>
        new(Guid.NewGuid(), src, output, targetExt, new ConversionOptions());

    [Fact]
    public async Task Png_To_Jpg_Succeeds()
    {
        var src = PathIn("src.png");
        using (var image = new Image<Rgba32>(16, 12))
        {
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                        row[x] = new Rgba32(200, 100, 50);
                }
            });
            image.Save(src);
        }

        var output = PathIn("out.jpg");
        var converter = new ImageConverter();
        Assert.True(converter.CanConvert(Job(src, "jpg", output)));

        var result = await converter.ConvertAsync(Job(src, "jpg", output), null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));
        using var loaded = await Image.LoadAsync(output);
        Assert.Equal(16, loaded.Width);
        Assert.Equal(12, loaded.Height);
    }

    [Fact]
    public async Task Failed_Conversion_Returns_Error_Not_Throw()
    {
        var src = PathIn("broken.png");
        await File.WriteAllTextAsync(src, "这不是图片");

        var output = PathIn("out.jpg");
        var result = await new ImageConverter().ConvertAsync(Job(src, "jpg", output), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(File.Exists(output)); // 失败不留半成品
    }

    [Fact]
    public void Routing_Gif_To_Mp4_Goes_To_Ffmpeg_Not_Image()
    {
        var imageConverter = new ImageConverter();
        var videoConverter = new FfmpegVideoConverter();

        Assert.False(imageConverter.CanConvert(Job("a.gif", "mp4", "o.mp4")));
        Assert.True(videoConverter.CanConvert(Job("a.gif", "mp4", "o.mp4")));

        Assert.True(imageConverter.CanConvert(Job("a.gif", "png", "o.png")));
        Assert.False(videoConverter.CanConvert(Job("a.gif", "png", "o.png")));
    }

    [Fact]
    public void Factory_Routes_All_Categories()
    {
        var factory = ConverterFactory.CreateDefault();
        Assert.IsType<FfmpegVideoConverter>(factory.GetConverter(Job("a.mp4", "mkv", "o.mkv")));
        Assert.IsType<FfmpegAudioConverter>(factory.GetConverter(Job("a.mp4", "mp3", "o.mp3")));
        Assert.IsType<ImageConverter>(factory.GetConverter(Job("a.png", "jpg", "o.jpg")));
        Assert.IsType<DocumentConverter>(factory.GetConverter(Job("a.txt", "pdf", "o.pdf")));
    }
}
