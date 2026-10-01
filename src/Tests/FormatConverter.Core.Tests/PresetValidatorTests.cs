using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;

namespace FormatConverter.Core.Tests;

public class PresetValidatorTests
{
    private static ConversionPreset Valid() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "我的预设",
        TargetExtension = "mp4",
    };

    private static readonly ConversionPreset[] Existing =
    [
        new ConversionPreset { Id = "other", Name = "已存在", TargetExtension = "mp3" },
    ];

    [Fact]
    public void Valid_Preset_Passes()
    {
        Assert.Null(PresetValidator.Validate(Valid(), Existing));
    }

    [Fact]
    public void Archive_Action_Without_Folder_Is_Allowed()
    {
        var p = Valid() with { PostConversionAction = PostConversionAction.MoveToArchiveFolder };
        Assert.Null(PresetValidator.Validate(p, Existing));
    }

    [Fact]
    public void Blank_Name_Rejected()
    {
        var p = Valid() with { Name = "  " };
        Assert.Contains("不能为空", PresetValidator.Validate(p, Existing));
    }

    [Fact]
    public void Duplicate_Name_Rejected_But_Self_Id_Ignored()
    {
        var p = Valid() with { Name = "已存在" };
        Assert.Contains("已存在", PresetValidator.Validate(p, Existing));

        var self = new ConversionPreset { Id = "other", Name = "已存在", TargetExtension = "mp3" };
        Assert.Null(PresetValidator.Validate(self, Existing, selfId: "other"));
    }

    [Fact]
    public void Name_Equal_To_Format_Extension_Rejected()
    {
        var p = Valid() with { Name = "mp3" };
        Assert.Contains("扩展名", PresetValidator.Validate(p, Existing));
    }

    [Fact]
    public void Unknown_Target_Rejected()
    {
        var p = Valid() with { TargetExtension = "xyz" };
        Assert.Contains("不受支持", PresetValidator.Validate(p, Existing));
    }

    [Fact]
    public void Bad_Template_Rejected()
    {
        var p = Valid() with { OutputFileNameTemplate = "(unknown)" };
        Assert.Contains("占位符", PresetValidator.Validate(p, Existing));
    }
}
