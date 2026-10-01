using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;

namespace FormatConverter.Core.Tests;

public class PresetMergerTests
{
    private static ConversionPreset Builtin(string id) => new()
    {
        Id = id, Name = $"内置{id}", TargetExtension = "mp4", IsBuiltIn = true,
    };

    private static ConversionPreset User(string id) => new()
    {
        Id = id, Name = $"用户{id}", TargetExtension = "mp3",
    };

    private readonly ConversionPreset[] _builtins = [Builtin("builtin.a"), Builtin("builtin.b")];
    private readonly ConversionPreset[] _users = [User("u1"), User("u2")];

    [Fact]
    public void Merge_Respects_Order_Then_Unlisted_User_Then_Unlisted_Builtin()
    {
        var merged = PresetMerger.Merge(_builtins, _users, ["u1", "builtin.a"], null);
        Assert.Equal(["u1", "builtin.a", "u2", "builtin.b"], merged.Select(p => p.Id));
    }

    [Fact]
    public void Merge_Deleted_Builtin_Not_Revived()
    {
        var merged = PresetMerger.Merge(_builtins, _users, null, ["builtin.a"]);
        Assert.DoesNotContain(merged, p => p.Id == "builtin.a");
        Assert.Equal(["u1", "u2", "builtin.b"], merged.Select(p => p.Id));
    }

    [Fact]
    public void Merge_Deleted_Builtin_Ignored_Even_If_In_Order()
    {
        var merged = PresetMerger.Merge(_builtins, _users, ["builtin.a", "u1"], ["builtin.a"]);
        Assert.Equal(["u1", "u2", "builtin.b"], merged.Select(p => p.Id));
    }

    [Fact]
    public void Merge_Invalid_Order_Ids_Ignored_And_Duplicates_Removed()
    {
        var merged = PresetMerger.Merge(_builtins, _users, ["nope", "u1", "u1"], null);
        Assert.Equal(["u1", "u2", "builtin.a", "builtin.b"], merged.Select(p => p.Id));
    }

    [Fact]
    public void Merge_User_Preset_With_Same_Id_Overrides_Builtin()
    {
        var user = new ConversionPreset { Id = "builtin.a", Name = "我的覆盖", TargetExtension = "mp3" };
        var merged = PresetMerger.Merge(_builtins, [user], null, null);
        var p = Assert.Single(merged, x => x.Id == "builtin.a");
        Assert.Equal("我的覆盖", p.Name);
    }

    [Fact]
    public void Resolve_By_Id_Or_Name_Case_Insensitive()
    {
        var merged = PresetMerger.Merge(_builtins, _users, null, null);
        Assert.Equal("u1", PresetMerger.Resolve("u1", merged)!.Id);
        Assert.Equal("u1", PresetMerger.Resolve("U1", merged)!.Id);
        Assert.Equal("u1", PresetMerger.Resolve("用户u1", merged)!.Id);
        Assert.Equal("builtin.b", PresetMerger.Resolve("内置builtin.b", merged)!.Id);
        Assert.Null(PresetMerger.Resolve("不存在", merged));
    }
}
