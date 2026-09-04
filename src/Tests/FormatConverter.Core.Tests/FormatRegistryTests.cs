using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class FormatRegistryTests
{
    [Fact]
    public void Mp4_Can_Convert_To_All_Video_And_Audio_Targets()
    {
        var targets = FormatRegistry.GetTargets("mp4").Select(f => f.Extension).ToHashSet();
        foreach (var ext in new[] { "mkv", "avi", "mov", "webm", "gif", "mp3", "wav", "flac", "m4a", "aac", "ogg" })
            Assert.Contains(ext, targets);
        Assert.DoesNotContain("mp4", targets); // 同格式互转禁用
    }

    [Fact]
    public void Docx_Targets_Include_Pdf_Txt_Md_Html()
    {
        var targets = FormatRegistry.GetTargets("docx").Select(f => f.Extension).ToHashSet();
        foreach (var ext in new[] { "pdf", "txt", "md", "html" })
            Assert.Contains(ext, targets);
    }

    [Fact]
    public void Gif_Targets_Include_Images_And_Mp4()
    {
        var targets = FormatRegistry.GetTargets("gif").Select(f => f.Extension).ToHashSet();
        foreach (var ext in new[] { "png", "jpg", "webp", "bmp", "ico", "mp4" })
            Assert.Contains(ext, targets);
    }

    [Fact]
    public void Pdf_Only_Converts_To_Txt()
    {
        var targets = FormatRegistry.GetTargets("pdf").Select(f => f.Extension).ToArray();
        Assert.Equal(new[] { "txt" }, targets);
    }

    [Fact]
    public void Same_Format_Disabled_Everywhere()
    {
        foreach (var format in FormatRegistry.AllFormats)
        {
            var targets = FormatRegistry.GetTargets(format.Extension);
            Assert.DoesNotContain(targets, t => t.Extension == format.Extension);
        }
    }

    [Fact]
    public void Default_Targets_Follow_Category_Default()
    {
        Assert.Equal("mp4", FormatRegistry.GetDefaultTarget("mkv")!.Extension);
        Assert.Equal("mp3", FormatRegistry.GetDefaultTarget("wav")!.Extension);
        Assert.Equal("pdf", FormatRegistry.GetDefaultTarget("docx")!.Extension);
        Assert.Equal("png", FormatRegistry.GetDefaultTarget("jpg")!.Extension);
    }

    [Fact]
    public void Category_Mapping()
    {
        Assert.Equal(FileCategory.Video, FormatRegistry.GetCategory("mp4"));
        Assert.Equal(FileCategory.Audio, FormatRegistry.GetCategory("mp3"));
        Assert.Equal(FileCategory.Document, FormatRegistry.GetCategory("docx"));
        Assert.Equal(FileCategory.Image, FormatRegistry.GetCategory("gif"));
    }

    [Fact]
    public void IsSupported_Known_And_Unknown()
    {
        Assert.True(FormatRegistry.IsSupported("mkv"));
        Assert.True(FormatRegistry.IsSupported("flac"));
        Assert.True(FormatRegistry.IsSupported("ico"));
        Assert.False(FormatRegistry.IsSupported("xyz"));
    }
}
