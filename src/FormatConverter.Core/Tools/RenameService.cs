using System.Text.RegularExpressions;

namespace FormatConverter.Core.Tools;

public sealed record RenamePreviewItem(
    string SourcePath,
    string NewName,
    bool HasConflict,
    string? ConflictReason);

public sealed record RenameResult(
    string SourcePath,
    string? NewPath,
    bool Success,
    string? Error);

/// <summary>
/// 批量重命名:模板占位符 {n}(序号,支持 {n:3} 宽度)、{name}(原文件名)、{ext}(原扩展名)、{date}(yyyyMMdd,文件修改时间)。
/// 模板未写 {ext} 且结果没有扩展名时,自动追加原扩展名(重命名不改格式)。
/// </summary>
public static class RenameService
{
    private static readonly Regex Placeholder = new(
        @"\{(name|ext|date|n(?::\d+)?)\}", RegexOptions.Compiled);

    /// <summary>预览重命名结果(不落盘);冲突包括磁盘同名与新名字重复。</summary>
    public static IReadOnlyList<RenamePreviewItem> Preview(
        IEnumerable<string> paths, string template, int startNumber = 1)
    {
        var list = paths.Where(File.Exists).ToList();
        var results = new List<RenamePreviewItem>(list.Count);
        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < list.Count; i++)
        {
            var path = list[i];
            var newName = BuildName(path, template, startNumber + i);
            var targetPath = Path.Combine(Path.GetDirectoryName(path)!, newName);

            var existsOnDisk = !string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
                               && File.Exists(targetPath);
            var duplicate = !usedTargets.Add(targetPath);

            string? reason = null;
            if (existsOnDisk) reason = "目标文件已存在";
            else if (duplicate) reason = "与其他文件的新名字重复";

            results.Add(new RenamePreviewItem(path, newName, existsOnDisk || duplicate, reason));
        }
        return results;
    }

    /// <summary>执行重命名;冲突项跳过,单项失败不影响其他文件。</summary>
    public static IReadOnlyList<RenameResult> Apply(
        IEnumerable<string> paths, string template, int startNumber = 1)
    {
        var list = paths.ToList();
        var results = new List<RenameResult>(list.Count);
        var existing = new List<string>(list.Count);

        foreach (var path in list)
        {
            if (File.Exists(path)) existing.Add(path);
            else results.Add(new RenameResult(path, null, false, "文件不存在"));
        }

        var preview = Preview(existing, template, startNumber);

        foreach (var item in preview)
        {
            if (item.HasConflict)
            {
                results.Add(new RenameResult(item.SourcePath, null, false, item.ConflictReason));
                continue;
            }

            try
            {
                var target = Path.Combine(Path.GetDirectoryName(item.SourcePath)!, item.NewName);
                File.Move(item.SourcePath, target);
                results.Add(new RenameResult(item.SourcePath, target, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new RenameResult(item.SourcePath, null, false, ex.Message));
            }
        }
        return results;
    }

    /// <summary>把模板替换成一个新文件名(不含目录)。</summary>
    public static string BuildName(string path, string template, int number)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path).TrimStart('.');
        var date = File.GetLastWriteTime(path).ToString("yyyyMMdd");

        var result = Placeholder.Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            return key switch
            {
                "name" => name,
                "ext" => ext,
                "date" => date,
                "n" => number.ToString(),
                _ when key.StartsWith("n:", StringComparison.Ordinal) =>
                    number.ToString("D" + int.Parse(key[2..])),
                _ => m.Value,
            };
        });

        // 模板没写 {ext} 且结果没有扩展名 → 追加原扩展名
        if (!template.Contains("{ext}", StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(result).Length == 0
            && ext.Length > 0)
        {
            result += "." + ext;
        }
        return result;
    }
}
