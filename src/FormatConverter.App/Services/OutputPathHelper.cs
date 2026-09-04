using System.IO;

namespace FormatConverter.App.Services;

/// <summary>解析输出路径:目录选择 + 重名策略(覆盖或自动加序号)。</summary>
public static class OutputPathHelper
{
    public static string Resolve(
        string sourcePath, string targetExt,
        string outputDirectory, bool outputToSourceFolder, bool autoRename)
    {
        var dir = outputToSourceFolder || string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(sourcePath)!
            : outputDirectory;

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(dir, name + "." + targetExt);

        if (!Exists(candidate) && !SamePath(candidate, sourcePath))
            return candidate;

        if (autoRename)
        {
            for (var i = 1; i < 10_000; i++)
            {
                candidate = Path.Combine(dir, $"{name} ({i}).{targetExt}");
                if (!Exists(candidate) && !SamePath(candidate, sourcePath))
                    return candidate;
            }
        }
        return candidate;
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
