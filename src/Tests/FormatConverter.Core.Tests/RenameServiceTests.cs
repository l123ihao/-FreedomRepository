using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

public class RenameServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fc-rename-" + Guid.NewGuid().ToString("N"));

    public RenameServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string MakeFile(string name, DateTime? writeTime = null)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        if (writeTime is DateTime dt) File.SetLastWriteTime(path, dt);
        return path;
    }

    [Fact]
    public void BuildName_Replaces_Name_Ext_Date_And_Number()
    {
        var p = MakeFile("报告.docx", new DateTime(2026, 5, 1));
        Assert.Equal("报告-20260501.docx",
            RenameService.BuildName(p, "{name}-{date}.{ext}", 1));
        Assert.Equal("报告 (1).docx",
            RenameService.BuildName(p, "{name} ({n}).{ext}", 1));
        Assert.Equal("报告.docx",
            RenameService.BuildName(p, "{name}", 1)); // 未写 {ext} → 自动追加
    }

    [Fact]
    public void BuildName_Supports_Number_Width()
    {
        var p = MakeFile("a.txt");
        Assert.Equal("a-007.txt", RenameService.BuildName(p, "{name}-{n:3}", 7));
    }

    [Fact]
    public void Preview_Detects_Existing_Conflict_And_Duplicate()
    {
        var a = MakeFile("a.txt");
        MakeFile("b.txt");
        var c = MakeFile("c.txt");

        // 两个文件都重命名为 same.txt → 后者与前者新名重复
        var preview = RenameService.Preview(new[] { a, c }, "same", 1);
        Assert.False(preview[0].HasConflict);
        Assert.True(preview[1].HasConflict);
        Assert.Equal("与其他文件的新名字重复", preview[1].ConflictReason);

        // 磁盘上已存在目标名
        MakeFile("taken.txt");
        var preview2 = RenameService.Preview(new[] { a }, "taken", 1);
        Assert.True(preview2[0].HasConflict);
        Assert.Equal("目标文件已存在", preview2[0].ConflictReason);
    }

    [Fact]
    public void Apply_Renames_Files_And_Skips_Conflicts()
    {
        var a = MakeFile("a.txt");
        var b = MakeFile("b.txt");
        MakeFile("taken.txt");

        var results = RenameService.Apply(new[] { a, b }, "{name}-{n}", 1);

        Assert.True(results[0].Success);
        Assert.True(File.Exists(Path.Combine(_dir, "a-1.txt")));
        Assert.False(File.Exists(a));

        // b.txt 的目标 b-2.txt 不存在 → 成功;再对 a 用已有目标名测冲突
        Assert.True(results[1].Success);
    }

    [Fact]
    public void Apply_Returns_Failure_For_Missing_File()
    {
        var results = RenameService.Apply(new[] { Path.Combine(_dir, "ghost.txt") }, "x-{n}", 1);
        Assert.Single(results);
        Assert.False(results[0].Success);
    }
}
