using FormatConverter.Core.Models;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

public class OutputTemplateEngineTests : IDisposable
{
    private readonly string _dir;
    private const string Source = @"C:\Users\张三\Videos\meeting.mp4";

    public OutputTemplateEngineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private TemplateContext Ctx(string source = Source, string ext = "mp3", int index = 1, int count = 1)
        => new(source, ext, index, count, new DateTime(2026, 10, 1, 8, 5, 3));

    private string File(string name)
    {
        var path = Path.Combine(_dir, name);
        System.IO.File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Expand_All_Basic_Tokens()
    {
        var result = OutputTemplateEngine.Expand("(p)-(f)-(F)-(i)-(I)-(o)-(O)", Ctx());
        Assert.Equal(@"C:\Users\张三\Videos\-meeting-MEETING-mp4-MP4-mp3-MP3", result);
    }

    [Fact]
    public void Expand_Special_Folders()
    {
        var result = OutputTemplateEngine.Expand(
            "(p:d)-(p:documents)-(p:m)-(p:music)-(p:v)-(p:videos)-(p:p)-(p:pictures)-(p:desktop)", Ctx());
        // (p:xxx) 展开为带尾部分隔符的绝对目录(与 File Converter 行为一致)
        var expected = string.Join("-",
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            AppendSep(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
        Assert.Equal(expected, result);
    }

    private static string AppendSep(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    [Fact]
    public void Expand_Directory_Levels_Walk_Up_And_Upper_Variant()
    {
        Assert.Equal("Videos", OutputTemplateEngine.Expand("(d0)", Ctx()));
        Assert.Equal("张三", OutputTemplateEngine.Expand("(d1)", Ctx()));
        Assert.Equal("Users", OutputTemplateEngine.Expand("(d2)", Ctx()));
        Assert.Equal("", OutputTemplateEngine.Expand("(d3)", Ctx())); // 已到根,越界空串
        Assert.Equal("VIDEOS", OutputTemplateEngine.Expand("(D0)", Ctx()));
    }

    [Fact]
    public void Expand_Index_And_Count()
    {
        Assert.Equal("3_12", OutputTemplateEngine.Expand("(n:i)_(n:c)", Ctx(index: 3, count: 12)));
    }

    [Fact]
    public void Expand_Date_Replaces_Path_Illegal_Separators()
    {
        Assert.Equal("2026-10-01", OutputTemplateEngine.Expand("(d:yyyy-MM-dd)", Ctx()));
        Assert.Equal("08'05'03", OutputTemplateEngine.Expand("(d:HH:mm:ss)", Ctx()));
    }

    [Fact]
    public void Expand_Invalid_Date_Format_Throws()
    {
        var ex = Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("(d:Q)", Ctx()));
        Assert.Contains("日期格式", ex.Message);
    }

    [Fact]
    public void Expand_Unknown_Token_Throws()
    {
        var ex = Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("(xyz)", Ctx()));
        Assert.Contains("未知占位符", ex.Message);
    }

    [Fact]
    public void Expand_Empty_Parens_Throws()
    {
        Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("a()b", Ctx()));
    }

    [Fact]
    public void Expand_Unmatched_Parens_Throws()
    {
        Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("(f", Ctx()));
        Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("f)", Ctx()));
    }

    [Fact]
    public void Expand_Illegal_Char_Throws()
    {
        Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("a<b", Ctx()));
    }

    [Fact]
    public void Expand_Empty_Template_Throws()
    {
        Assert.Throws<OutputTemplateException>(() => OutputTemplateEngine.Expand("  ", Ctx()));
    }

    [Fact]
    public void Expand_Slash_Allowed_As_Subdirectory_Separator()
    {
        Assert.Equal(@"sub\meeting", OutputTemplateEngine.Expand("sub/(f)", Ctx()));
    }

    [Fact]
    public void TryValidate_Good_And_Bad()
    {
        Assert.True(OutputTemplateEngine.TryValidate("(p)(f)", out var error));
        Assert.Null(error);
        Assert.False(OutputTemplateEngine.TryValidate("(bad)", out error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Resolve_Relative_Template_Uses_OutputDirectory()
    {
        var result = OutputTemplateEngine.Resolve(
            Source, "mp3", "(f)_new", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(), null);
        Assert.Equal(Path.Combine(_dir, "meeting_new.mp3"), result);
    }

    [Fact]
    public void Resolve_Relative_Template_Uses_SourceDir_When_OutputToSourceFolder()
    {
        var result = OutputTemplateEngine.Resolve(
            Source, "mp3", "(f)_new", _dir, outputToSourceFolder: true,
            OverwritePolicy.Rename, Ctx(), null);
        Assert.Equal(@"C:\Users\张三\Videos\meeting_new.mp3", result);
    }

    [Fact]
    public void Resolve_Absolute_Template_Ignores_OutputDirectory()
    {
        var result = OutputTemplateEngine.Resolve(
            Source, "mp3", "(p)(f)_new", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(), null);
        Assert.Equal(@"C:\Users\张三\Videos\meeting_new.mp3", result);
    }

    [Fact]
    public void Resolve_Appends_Extension_When_Missing()
    {
        var result = OutputTemplateEngine.Resolve(
            Source, "mp3", "(f)", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(), null);
        Assert.Equal(Path.Combine(_dir, "meeting.mp3"), result);
    }

    [Fact]
    public void Resolve_Rename_Conflict_Increments_From_2()
    {
        var source = File("a.mp4");
        File("a.mp3");
        File("a (2).mp3");
        var result = OutputTemplateEngine.Resolve(
            source, "mp3", "(p)(f)", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(source), null);
        Assert.Equal(Path.Combine(_dir, "a (3).mp3"), result);
    }

    [Fact]
    public void Resolve_Overwrite_Returns_Conflicted_Path()
    {
        var source = File("a.mp4");
        File("a.mp3");
        var result = OutputTemplateEngine.Resolve(
            source, "mp3", "(p)(f)", _dir, outputToSourceFolder: false,
            OverwritePolicy.Overwrite, Ctx(source), null);
        Assert.Equal(Path.Combine(_dir, "a.mp3"), result);
    }

    [Fact]
    public void Resolve_Skips_Blacklisted_Paths()
    {
        var source = File("a.mp4");
        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(_dir, "a.mp3"),
        };
        var result = OutputTemplateEngine.Resolve(
            source, "mp3", "(p)(f)", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(source), blacklist);
        Assert.Equal(Path.Combine(_dir, "a (2).mp3"), result);
    }

    [Fact]
    public void Resolve_SamePath_With_Source_Treated_As_Conflict()
    {
        var source = File("a.mp4");
        var result = OutputTemplateEngine.Resolve(
            source, "mp4", "(p)(f)", _dir, outputToSourceFolder: false,
            OverwritePolicy.Rename, Ctx(source, ext: "mp4"), null);
        Assert.Equal(Path.Combine(_dir, "a (2).mp4"), result);
    }
}
