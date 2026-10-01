using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

public class OutputPathResolverTests : IDisposable
{
    private readonly string _dir;
    private readonly string _source;

    public OutputPathResolverTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _source = Path.Combine(_dir, "视频.mp4");
        File.WriteAllText(_source, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    [Fact]
    public void ResolveLegacy_Basic_No_Conflict()
    {
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", _dir, false, true);
        Assert.Equal(Path.Combine(_dir, "视频.mp3"), result);
    }

    [Fact]
    public void ResolveLegacy_AutoRename_Increments_From_1()
    {
        File.WriteAllText(Path.Combine(_dir, "视频.mp3"), "");
        File.WriteAllText(Path.Combine(_dir, "视频 (1).mp3"), "");
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", _dir, false, true);
        Assert.Equal(Path.Combine(_dir, "视频 (2).mp3"), result);
    }

    [Fact]
    public void ResolveLegacy_No_Rename_Returns_Conflicted_Path()
    {
        File.WriteAllText(Path.Combine(_dir, "视频.mp3"), "");
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", _dir, false, autoRename: false);
        Assert.Equal(Path.Combine(_dir, "视频.mp3"), result);
    }

    [Fact]
    public void ResolveLegacy_OutputToSourceFolder_Wins_Over_OutputDirectory()
    {
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", @"D:\别处", true, true);
        Assert.Equal(Path.Combine(_dir, "视频.mp3"), result);
    }

    [Fact]
    public void ResolveLegacy_Empty_OutputDirectory_Falls_Back_To_SourceDir()
    {
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", "  ", false, true);
        Assert.Equal(Path.Combine(_dir, "视频.mp3"), result);
    }

    [Fact]
    public void ResolveLegacy_Skips_Blacklisted_Paths()
    {
        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(_dir, "视频.mp3"),
        };
        var result = OutputPathResolver.ResolveLegacy(_source, "mp3", _dir, false, true, blacklist);
        Assert.Equal(Path.Combine(_dir, "视频 (1).mp3"), result);
    }

    [Fact]
    public void Resolve_Null_Template_Delegates_To_Legacy()
    {
        var legacy = OutputPathResolver.ResolveLegacy(_source, "mp3", _dir, false, true);
        var ctx = new TemplateContext(_source, "mp3", 1, 1, DateTime.Now);
        var result = OutputPathResolver.Resolve(_source, "mp3", null, _dir, false, true, ctx);
        Assert.Equal(legacy, result);
    }

    [Fact]
    public void Resolve_Template_Prevents_Same_Name_Collision_Within_Batch()
    {
        var other = Path.Combine(_dir, "视频.mp4");
        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ctx = new TemplateContext(_source, "mp3", 1, 2, DateTime.Now);

        var first = OutputPathResolver.Resolve(_source, "mp3", "(f)", _dir, false, true, ctx, blacklist);
        blacklist.Add(first);
        var second = OutputPathResolver.Resolve(other, "mp3", "(f)", _dir, false, true,
            new TemplateContext(other, "mp3", 2, 2, DateTime.Now), blacklist);

        Assert.Equal(Path.Combine(_dir, "视频.mp3"), first);
        Assert.Equal(Path.Combine(_dir, "视频 (2).mp3"), second);
    }
}
