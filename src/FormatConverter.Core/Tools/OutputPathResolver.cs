using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tools;

/// <summary>
/// 输出路径统一入口:
/// template 为空 → ResolveLegacy(原 App 侧 OutputPathHelper 逻辑,行为不变);
/// 带模板 → OutputTemplateEngine(冲突按 autoRename 映射 OverwritePolicy)。
/// blacklist = 本批次已分配的输出路径集合(OrdinalIgnoreCase),防批次内同名互撞。
/// </summary>
public static class OutputPathResolver
{
    /// <summary>旧逻辑:目录选择(源目录或指定目录)+ 重名策略(覆盖或自动 " (1)" 起加序号)。</summary>
    public static string ResolveLegacy(
        string sourcePath, string targetExt,
        string outputDirectory, bool outputToSourceFolder, bool autoRename,
        HashSet<string>? blacklist = null)
    {
        var dir = outputToSourceFolder || string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(sourcePath)!
            : outputDirectory;

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(dir, name + "." + targetExt);

        if (!IsConflict(candidate, sourcePath, blacklist)) return candidate;

        if (autoRename)
        {
            for (var i = 1; i < 10_000; i++)
            {
                candidate = Path.Combine(dir, $"{name} ({i}).{targetExt}");
                if (!IsConflict(candidate, sourcePath, blacklist)) return candidate;
            }
        }
        return candidate;
    }

    /// <summary>template 非空走模板引擎,否则退化为 ResolveLegacy。</summary>
    public static string Resolve(
        string sourcePath, string targetExt, string? template,
        string outputDirectory, bool outputToSourceFolder, bool autoRename,
        TemplateContext ctx, HashSet<string>? blacklist = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            return ResolveLegacy(sourcePath, targetExt, outputDirectory, outputToSourceFolder, autoRename, blacklist);

        var policy = autoRename ? OverwritePolicy.Rename : OverwritePolicy.Overwrite;
        return OutputTemplateEngine.Resolve(
            sourcePath, targetExt, template, outputDirectory, outputToSourceFolder, policy, ctx, blacklist);
    }

    private static bool IsConflict(string path, string sourcePath, HashSet<string>? blacklist)
        => File.Exists(path) || Directory.Exists(path)
           || SamePath(path, sourcePath)
           || (blacklist?.Contains(path) ?? false);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
