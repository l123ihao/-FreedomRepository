using FormatConverter.Core.Engine;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tests;

public class PostConversionActionsTests : IDisposable
{
    private readonly string _dir;

    public PostConversionActionsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        System.IO.File.WriteAllText(path, "");
        return path;
    }

    private static ConversionResult Result(
        string source, string output, bool success = true,
        PostConversionAction action = PostConversionAction.None)
    {
        var job = new ConversionJob(
            Guid.NewGuid(), source, output,
            Path.GetExtension(output).TrimStart('.'), new ConversionOptions(), action);
        return new ConversionResult(job, success, success ? output : null,
            success ? null : "失败", TimeSpan.Zero);
    }

    [Fact]
    public void Apply_None_Returns_Empty_Outcome()
    {
        var source = Touch("a.mp4");
        var outcome = PostConversionActions.Apply(Result(source, Path.Combine(_dir, "a.mp3")), null);
        Assert.Equal(PostConversionAction.None, outcome.Action);
        Assert.True(outcome.Success);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Apply_Failure_Skips_Action()
    {
        var source = Touch("a.mp4");
        var outcome = PostConversionActions.Apply(
            Result(source, Path.Combine(_dir, "a.mp3"), success: false, action: PostConversionAction.DeleteSource), null);
        Assert.False(outcome.Success);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void DeleteSource_Deletes_Original_And_Keeps_Output()
    {
        var source = Touch("a.mp4");
        var output = Touch("a.mp3");
        var outcome = PostConversionActions.Apply(
            Result(source, output, action: PostConversionAction.DeleteSource), null);
        Assert.True(outcome.Success);
        Assert.Equal("原文件已删除", outcome.Detail);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void DeleteSource_SamePath_With_Output_Never_Deletes()
    {
        var source = Touch("a.mp4");
        var outcome = PostConversionActions.Apply(
            Result(source, source, action: PostConversionAction.DeleteSource), null);
        Assert.False(outcome.Success);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void DeleteSource_Missing_Source_Is_Fine()
    {
        var output = Touch("a.mp3");
        var missing = Path.Combine(_dir, "不存在.mp4");
        var outcome = PostConversionActions.Apply(
            Result(missing, output, action: PostConversionAction.DeleteSource), null);
        Assert.True(outcome.Success);
        Assert.Equal("原文件已不存在", outcome.Detail);
    }

    [Fact]
    public void MoveToArchive_Uses_Default_Archive_Folder_Next_To_Output()
    {
        var source = Touch("a.mp4");
        var output = Touch("a.mp3");
        var outcome = PostConversionActions.Apply(
            Result(source, output, action: PostConversionAction.MoveToArchiveFolder), null);
        Assert.True(outcome.Success);
        var expected = Path.Combine(_dir, "归档", "a.mp3");
        Assert.Equal(expected, outcome.NewOutputPath);
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void MoveToArchive_Uses_Custom_Folder()
    {
        var source = Touch("a.mp4");
        var output = Touch("a.mp3");
        var archive = Path.Combine(_dir, "我的归档");
        var outcome = PostConversionActions.Apply(
            Result(source, output, action: PostConversionAction.MoveToArchiveFolder), archive);
        Assert.True(outcome.Success);
        Assert.True(File.Exists(Path.Combine(archive, "a.mp3")));
    }

    [Fact]
    public void MoveToArchive_Conflict_Renames_From_2()
    {
        var source = Touch("a.mp4");
        var output = Touch("a.mp3");
        var archive = Path.Combine(_dir, "归档");
        Directory.CreateDirectory(archive);
        System.IO.File.WriteAllText(Path.Combine(archive, "a.mp3"), "");
        System.IO.File.WriteAllText(Path.Combine(archive, "a (2).mp3"), "");

        var outcome = PostConversionActions.Apply(
            Result(source, output, action: PostConversionAction.MoveToArchiveFolder), null);
        Assert.True(outcome.Success);
        Assert.Equal(Path.Combine(archive, "a (3).mp3"), outcome.NewOutputPath);
    }

    [Fact]
    public void MoveToArchive_Failure_Keeps_Output_And_Does_Not_Mark_Failed()
    {
        var source = Touch("a.mp4");
        var output = Touch("a.mp3");
        var blocked = Touch("不是目录.txt"); // 归档目标是个文件路径 → CreateDirectory 抛异常

        var outcome = PostConversionActions.Apply(
            Result(source, output, action: PostConversionAction.MoveToArchiveFolder), blocked);
        Assert.False(outcome.Success);
        Assert.Contains("归档失败", outcome.Detail);
        Assert.True(File.Exists(output)); // 输出文件保留
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void IsCrossVolume_Compares_Path_Roots()
    {
        Assert.True(PostConversionActions.IsCrossVolume(@"C:\a\x.mp3", @"D:\b\x.mp3"));
        Assert.False(PostConversionActions.IsCrossVolume(@"C:\a\x.mp3", @"c:\a\b\x.mp3"));
        Assert.False(PostConversionActions.IsCrossVolume(@"C:\a\x.mp3", @"C:\a\y.mp3"));
    }
}
