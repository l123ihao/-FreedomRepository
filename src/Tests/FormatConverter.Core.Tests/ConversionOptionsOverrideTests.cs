using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class ConversionOptionsOverrideTests
{
    private static ConversionOptions Base() => new()
    {
        AudioBitrateKbps = 192,
        VideoCrf = 23,
        GifFps = 12,
        GifWidth = 480,
        OverwritePolicy = OverwritePolicy.Rename,
        VideoMode = VideoMode.CopyFirst,
        HardwareAcceleration = true,
    };

    [Fact]
    public void ApplyTo_Overrides_Only_Non_Null_Fields()
    {
        var o = new ConversionOptionsOverride { AudioBitrateKbps = 320, VideoMode = VideoMode.AlwaysTranscode };
        var result = o.ApplyTo(Base());
        Assert.Equal(320, result.AudioBitrateKbps);
        Assert.Equal(23, result.VideoCrf); // 未覆盖,继承
        Assert.Equal(480, result.GifWidth);
        Assert.Equal(VideoMode.AlwaysTranscode, result.VideoMode);
        Assert.True(result.HardwareAcceleration);
        Assert.Equal(OverwritePolicy.Rename, result.OverwritePolicy); // 覆盖不携带,保持基底
    }

    [Fact]
    public void ApplyTo_Empty_Override_Returns_Equivalent_Options()
    {
        var result = new ConversionOptionsOverride().ApplyTo(Base());
        Assert.Equal(Base().AudioBitrateKbps, result.AudioBitrateKbps);
        Assert.Equal(Base().VideoMode, result.VideoMode);
    }

    [Fact]
    public void IsEmpty_Reflects_Fields()
    {
        Assert.True(new ConversionOptionsOverride().IsEmpty);
        Assert.False(new ConversionOptionsOverride { VideoCrf = 18 }.IsEmpty);
        Assert.False(new ConversionOptionsOverride { CustomFfmpegArgs = "-x" }.IsEmpty);
    }

    [Fact]
    public void SummaryText_Builds_Chinese_Parts()
    {
        var o = new ConversionOptionsOverride
        {
            AudioBitrateKbps = 320,
            VideoCrf = 18,
            HardwareAcceleration = false,
            VideoMode = VideoMode.AlwaysTranscode,
        };
        var text = o.SummaryText;
        Assert.Contains("码率 320k", text);
        Assert.Contains("CRF 18", text);
        Assert.Contains("硬编关", text);
        Assert.Contains("始终转码", text);

        Assert.Equal("", new ConversionOptionsOverride().SummaryText);
    }
}
