using FormatConverter.Core.Ffmpeg;

namespace FormatConverter.Core.Tests;

public class FfmpegProgressParserTests
{
    [Fact]
    public void Parses_OutTime_Us_And_Computes_Percent()
    {
        var state = new FfmpegProgressState();
        FfmpegProgressParser.OnLine(state, "frame=100");
        FfmpegProgressParser.OnLine(state, "out_time_us=5000000");
        FfmpegProgressParser.OnLine(state, "out_time_ms=5000000"); // 单位同为微秒,取最大值
        FfmpegProgressParser.OnLine(state, "speed=2.1x");

        Assert.Equal(5_000_000, state.MaxOutputTimeUs);
        Assert.Equal("2.1x", state.Speed);
        Assert.Equal(50.0, FfmpegProgressParser.ToPercent(state.MaxOutputTimeUs, 10));
    }

    [Fact]
    public void Percent_Is_Null_Without_Total_Duration()
    {
        var state = new FfmpegProgressState();
        FfmpegProgressParser.OnLine(state, "out_time_us=1000000");
        Assert.Null(FfmpegProgressParser.ToPercent(state.MaxOutputTimeUs, null));
    }

    [Fact]
    public void Percent_Clamped_To_100()
    {
        var state = new FfmpegProgressState();
        FfmpegProgressParser.OnLine(state, "out_time_us=999999999");
        Assert.Equal(100.0, FfmpegProgressParser.ToPercent(state.MaxOutputTimeUs, 3));
    }

    [Fact]
    public void Progress_End_Sets_Finished()
    {
        var state = new FfmpegProgressState();
        FfmpegProgressParser.OnLine(state, "progress=continue");
        Assert.False(state.Finished);
        FfmpegProgressParser.OnLine(state, "progress=end");
        Assert.True(state.Finished);
    }

    [Fact]
    public void Ignores_Garbage_Lines()
    {
        var state = new FfmpegProgressState();
        FfmpegProgressParser.OnLine(state, "no-equals-sign");
        FfmpegProgressParser.OnLine(state, "");
        FfmpegProgressParser.OnLine(state, "=x");
        Assert.Equal(0, state.MaxOutputTimeUs);
        Assert.Null(state.Speed);
    }

    [Fact]
    public void Part_Path_Is_In_Same_Directory_With_Part_Extension()
    {
        var part = FfmpegRunner.BuildPartPath(@"D:\out\我的 视频.mp4");
        Assert.StartsWith(@"D:\out", part);
        Assert.EndsWith(".part", part);
        Assert.NotEqual(@"D:\out\我的 视频.mp4", part);
    }
}
