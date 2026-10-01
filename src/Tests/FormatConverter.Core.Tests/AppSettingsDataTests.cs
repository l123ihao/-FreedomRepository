using System.Text.Json;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class AppSettingsDataTests
{
    [Fact]
    public void Defaults_Have_Expected_Values()
    {
        var d = AppSettingsData.Defaults();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "格式转换输出"),
            d.OutputDirectory);
        Assert.True(d.AutoRename);
        Assert.Equal(192, d.AudioBitrateKbps);
        Assert.True(d.VideoCopyFirst);
        Assert.True(d.VideoHardwareAcceleration);
        Assert.Equal(5, d.ExitDelaySeconds);
        Assert.Empty(d.UserPresets);
    }

    [Fact]
    public void Deserialize_Legacy_Two_Field_Json_Migrates_With_Defaults()
    {
        var legacy = """{"DontAskBeforeConvert":true,"Theme":"Dark"}""";
        var d = JsonSerializer.Deserialize<AppSettingsData>(legacy, AppSettingsData.JsonOptions)!;
        Assert.True(d.DontAskBeforeConvert);
        Assert.Equal("Dark", d.Theme);
        Assert.True(d.AutoRename);
        Assert.Equal(192, d.AudioBitrateKbps);
        Assert.Empty(d.UserPresets);
    }

    [Fact]
    public void RoundTrip_Preserves_Presets_And_Enums()
    {
        var d = AppSettingsData.Defaults();
        d.UserPresets.Add(new ConversionPreset
        {
            Id = "abc123",
            Name = "微信小视频",
            TargetExtension = "mp4",
            OutputFileNameTemplate = "(p)(f)_小",
            PostConversionAction = PostConversionAction.MoveToArchiveFolder,
            Options = new ConversionOptionsOverride { AudioBitrateKbps = 128, VideoCrf = 26 },
        });
        d.PresetOrder.Add("builtin.hq-mp4");
        d.PresetOrder.Add("abc123");

        var json = JsonSerializer.Serialize(d, AppSettingsData.JsonOptions);
        var back = JsonSerializer.Deserialize<AppSettingsData>(json, AppSettingsData.JsonOptions)!;

        var p = Assert.Single(back.UserPresets);
        Assert.Equal("微信小视频", p.Name);
        Assert.Equal("(p)(f)_小", p.OutputFileNameTemplate);
        Assert.Equal(PostConversionAction.MoveToArchiveFolder, p.PostConversionAction);
        Assert.Equal(128, p.Options!.AudioBitrateKbps);
        Assert.Equal(26, p.Options.VideoCrf);
        Assert.Equal(["builtin.hq-mp4", "abc123"], back.PresetOrder);
        Assert.Contains("MoveToArchiveFolder", json); // 枚举序列化为字符串而非数字
    }

    [Fact]
    public void Sanitize_Normalizes_Theme()
    {
        var d = AppSettingsData.Defaults();
        d.Theme = "dark";
        d.Sanitize();
        Assert.Equal("Dark", d.Theme);

        d.Theme = "奇怪的值";
        d.Sanitize();
        Assert.Null(d.Theme);
    }

    [Fact]
    public void Sanitize_Clamps_Exit_Delay_And_Bitrate()
    {
        var d = AppSettingsData.Defaults();
        d.ExitDelaySeconds = 25;
        d.AudioBitrateKbps = 500;
        d.Sanitize();
        Assert.Equal(10, d.ExitDelaySeconds);
        Assert.Equal(320, d.AudioBitrateKbps);

        d.ExitDelaySeconds = -3;
        d.AudioBitrateKbps = 8;
        d.Sanitize();
        Assert.Equal(0, d.ExitDelaySeconds);
        Assert.Equal(32, d.AudioBitrateKbps);
    }

    [Fact]
    public void Sanitize_Drops_Invalid_Presets_And_Duplicates()
    {
        var d = AppSettingsData.Defaults();
        d.UserPresets.AddRange(
        [
            new ConversionPreset { Id = "ok", Name = "合法", TargetExtension = "mp4" },
            new ConversionPreset { Id = "", Name = "无 Id", TargetExtension = "mp4" },
            new ConversionPreset { Id = "x1", Name = "", TargetExtension = "mp4" },
            new ConversionPreset { Id = "x2", Name = "坏目标", TargetExtension = "xyz" },
            new ConversionPreset { Id = "ok", Name = "重复 Id", TargetExtension = "mp3" },
        ]);
        d.Sanitize();
        var p = Assert.Single(d.UserPresets);
        Assert.Equal("合法", p.Name);
    }

    [Fact]
    public void Sanitize_Keeps_Only_Valid_Order_Ids_And_Dedupes()
    {
        var d = AppSettingsData.Defaults();
        d.UserPresets.Add(new ConversionPreset { Id = "u1", Name = "用户", TargetExtension = "mp3" });
        d.PresetOrder.AddRange(["builtin.hq-mp4", "u1", "bogus", "builtin.hq-mp4"]);
        d.Sanitize();
        Assert.Equal(["builtin.hq-mp4", "u1"], d.PresetOrder);
    }

    [Fact]
    public void ToConversionOptions_Maps_Global_Settings()
    {
        var d = AppSettingsData.Defaults();
        d.AutoRename = false;
        d.VideoCopyFirst = false;
        d.VideoHardwareAcceleration = false;
        d.AudioBitrateKbps = 320;
        var o = d.ToConversionOptions();
        Assert.Equal(OverwritePolicy.Overwrite, o.OverwritePolicy);
        Assert.Equal(VideoMode.AlwaysTranscode, o.VideoMode);
        Assert.False(o.HardwareAcceleration);
        Assert.Equal(320, o.AudioBitrateKbps);
    }
}
