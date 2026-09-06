using FormatConverter.Core.Tools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FormatConverter.Core.Tests;

public class ImageToolsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-img-" + Guid.NewGuid().ToString("N"));

    public ImageToolsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string MakeImage(string name, int width = 200, int height = 160)
    {
        var path = Path.Combine(_dir, name);
        using var img = new Image<Rgba32>(width, height, Color.Red);
        img.Save(path);
        return path;
    }

    private static (int Width, int Height) Size(string path)
    {
        using var img = Image.Load(path);
        return (img.Width, img.Height);
    }

    [Fact]
    public async Task Compress_Limits_MaxWidth_Keeping_Aspect()
    {
        var input = MakeImage("in.png", 200, 160);
        var output = Path.Combine(_dir, "out.jpg");

        await ImageTools.CompressAsync(input, output, new CompressOptions(Quality: 80, MaxWidth: 100));

        Assert.True(File.Exists(output));
        var size = Size(output);
        Assert.Equal(100, size.Width);
        Assert.Equal(80, size.Height);
    }

    [Fact]
    public async Task Resize_By_Percent()
    {
        var input = MakeImage("in.png", 200, 160);
        var output = Path.Combine(_dir, "out.png");

        await ImageTools.ResizeAsync(input, output, new ResizeOptions(Percent: 50));

        var size = Size(output);
        Assert.Equal(100, size.Width);
        Assert.Equal(80, size.Height);
    }

    [Fact]
    public async Task Resize_By_Width_Keeps_Aspect()
    {
        var input = MakeImage("in.png", 200, 160);
        var output = Path.Combine(_dir, "out.png");

        await ImageTools.ResizeAsync(input, output, new ResizeOptions(Width: 50));

        var size = Size(output);
        Assert.Equal(50, size.Width);
        Assert.Equal(40, size.Height);
    }

    [Fact]
    public async Task Crop_Covers_To_Exact_Size()
    {
        var input = MakeImage("in.png", 200, 160);
        var output = Path.Combine(_dir, "out.png");

        await ImageTools.CropAsync(input, output, new CropOptions(100, 100));

        var size = Size(output);
        Assert.Equal(100, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Fact]
    public async Task Watermark_Image_Produces_Output()
    {
        var input = MakeImage("in.png", 200, 160);
        var wm = MakeImage("wm.png", 40, 20);
        var output = Path.Combine(_dir, "out.png");

        await ImageTools.WatermarkAsync(input, output,
            new WatermarkOptions(ImagePath: wm, Position: WatermarkPosition.BottomRight));

        Assert.True(File.Exists(output));
        var size = Size(output);
        Assert.Equal(200, size.Width);
        Assert.Equal(160, size.Height);
    }

    [Fact]
    public async Task Watermark_Text_Produces_Output()
    {
        var input = MakeImage("in.png", 200, 160);
        var output = Path.Combine(_dir, "out.png");

        await ImageTools.WatermarkAsync(input, output,
            new WatermarkOptions(Text: "测试水印", Position: WatermarkPosition.Center, FontSize: 16));

        Assert.True(File.Exists(output));
        Assert.Equal(200, Size(output).Width);
    }
}
