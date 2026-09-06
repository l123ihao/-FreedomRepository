using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

public class FormatDetectorTests
{
    private static byte[] H(params byte[] bytes) => bytes;

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, "gif")]
    [InlineData(new byte[] { 0x42, 0x4D, 0x00, 0x00 }, "bmp")]
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00 }, "ico")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "pdf")]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "zip")]
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, "mkv")]
    [InlineData(new byte[] { 0x4F, 0x67, 0x67, 0x53 }, "ogg")]
    [InlineData(new byte[] { 0x66, 0x4C, 0x61, 0x43 }, "flac")]
    [InlineData(new byte[] { 0x49, 0x44, 0x33, 0x04 }, "mp3")]
    [InlineData(new byte[] { 0xFF, 0xFB, 0x90, 0x00 }, "mp3")]
    public void Detect_Returns_Known_Format(byte[] header, string expected)
    {
        Assert.Equal(expected, FormatDetector.Detect(header));
    }

    [Fact]
    public void Detect_Riff_Containers()
    {
        var webp = H(0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50);
        var avi = H(0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x41, 0x56, 0x49, 0x20);
        var wav = H(0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45);

        Assert.Equal("webp", FormatDetector.Detect(webp));
        Assert.Equal("avi", FormatDetector.Detect(avi));
        Assert.Equal("wav", FormatDetector.Detect(wav));
    }

    [Fact]
    public void Detect_Mp4_By_Ftyp_At_Offset_4()
    {
        var mp4 = H(0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32);
        Assert.Equal("mp4", FormatDetector.Detect(mp4));
    }

    [Fact]
    public void Detect_Unknown_Returns_Null()
    {
        Assert.Null(FormatDetector.Detect(H(0x01, 0x02, 0x03, 0x04)));
        Assert.Null(FormatDetector.Detect(H()));
        Assert.Null(FormatDetector.Detect(H(0x42)));
    }

    [Fact]
    public void DetectFile_Reads_From_Disk()
    {
        var path = Path.Combine(Path.GetTempPath(), "fc-detector-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            Assert.Equal("png", FormatDetector.DetectFile(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void IsKnownExtension_Covers_Common_Formats()
    {
        Assert.True(FormatDetector.IsKnownExtension("mp4"));
        Assert.True(FormatDetector.IsKnownExtension(".JPG"));
        Assert.False(FormatDetector.IsKnownExtension("exe"));
    }
}
