using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class FfmpegArgsBuilderTests
{
    private static readonly ConversionOptions Opts = new();

    private static void AssertOrdered(IReadOnlyList<string> args, params string[] expected)
    {
        var idx = -1;
        foreach (var e in expected)
        {
            var next = -1;
            for (var i = idx + 1; i < args.Count; i++)
            {
                if (args[i] == e) { next = i; break; }
            }
            Assert.True(next > idx, $"参数 '{e}' 未按顺序出现。完整参数: {string.Join(" ", args)}");
            idx = next;
        }
    }

    private static int IndexOf(IReadOnlyList<string> args, string value)
    {
        for (var i = 0; i < args.Count; i++)
            if (args[i] == value) return i;
        return -1;
    }

    [Fact]
    public void Mp4_To_Mp3_Uses_LibMp3Lame_With_Bitrate()
    {
        var args = FfmpegArgsBuilder.Build("in.mp4", "mp3", "out.part", Opts, null);
        AssertOrdered(args, "-i", "in.mp4", "-vn", "-c:a", "libmp3lame", "-b:a", "192k", "-f", "mp3", "out.part");
    }

    [Fact]
    public void Wav_Target_Uses_Pcm_And_No_Bitrate()
    {
        var args = FfmpegArgsBuilder.Build("in.mp3", "wav", "out.part", Opts, null);
        Assert.Contains("pcm_s16le", args);
        Assert.DoesNotContain("-b:a", args);
        AssertOrdered(args, "-f", "wav", "out.part");
    }

    [Fact]
    public void M4a_Target_Uses_Ipod_Muxer()
    {
        var args = FfmpegArgsBuilder.Build("in.mp3", "m4a", "out.part", Opts, null);
        AssertOrdered(args, "-f", "ipod", "out.part");
        AssertOrdered(args, "-c:a", "aac");
    }

    [Fact]
    public void Webm_Target_Transcodes_Vp9_And_Opus()
    {
        var probe = new ProbeResult(10, new[] { new ProbeStream("video", "h264"), new ProbeStream("audio", "aac") });
        var args = FfmpegArgsBuilder.Build("in.mp4", "webm", "out.part", Opts, probe);
        AssertOrdered(args, "-c:v", "libvpx-vp9", "-c:a", "libopus", "-f", "webm");
    }

    [Fact]
    public void CopyMode_For_Compatible_H264_Aac_Into_Mkv()
    {
        var probe = new ProbeResult(10, new[] { new ProbeStream("video", "h264"), new ProbeStream("audio", "aac") });
        var args = FfmpegArgsBuilder.Build("in.mp4", "mkv", "out.part", Opts, probe);
        AssertOrdered(args, "-c", "copy");
        Assert.DoesNotContain("libx264", args);
        Assert.DoesNotContain("libvpx-vp9", args);
    }

    [Fact]
    public void Avi_Target_Forces_Mp3_Audio_Even_When_Copying()
    {
        var probe = new ProbeResult(10, new[] { new ProbeStream("video", "h264"), new ProbeStream("audio", "aac") });
        var args = FfmpegArgsBuilder.Build("in.mp4", "avi", "out.part", Opts, probe);
        AssertOrdered(args, "-c:v", "copy", "-c:a", "libmp3lame", "-f", "avi");
    }

    [Fact]
    public void Gif_Source_Is_Transcoded_Not_Copied()
    {
        // gif 源(视频流 codec=gif)无法 copy 进 mp4,必须走转码路径
        var probe = new ProbeResult(5, new[] { new ProbeStream("video", "gif") });
        var args = FfmpegArgsBuilder.Build("in.gif", "mp4", "out.part", Opts, probe);
        Assert.Contains("libx264", args);
        Assert.DoesNotContain("-c", args);
    }

    [Fact]
    public void Gif_Target_Uses_Palette_Filter()
    {
        var args = FfmpegArgsBuilder.Build("in.mp4", "gif", "out.part", Opts, null);
        var filter = args[IndexOf(args, "-filter_complex") + 1];
        Assert.Contains("palettegen", filter);
        Assert.Contains("paletteuse", filter);
        Assert.Contains("fps=12", filter);
        Assert.Contains("scale=480", filter);
        AssertOrdered(args, "-f", "gif", "out.part");
    }

    [Fact]
    public void Muxer_Mapping()
    {
        Assert.Equal("mp4", FfmpegArgsBuilder.GetMuxer("mp4"));
        Assert.Equal("mp4", FfmpegArgsBuilder.GetMuxer("mov"));
        Assert.Equal("matroska", FfmpegArgsBuilder.GetMuxer("mkv"));
        Assert.Equal("ipod", FfmpegArgsBuilder.GetMuxer("m4a"));
        Assert.Equal("adts", FfmpegArgsBuilder.GetMuxer("aac"));
    }
}
