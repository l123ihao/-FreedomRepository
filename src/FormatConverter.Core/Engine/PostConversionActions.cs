using FormatConverter.Core.Models;

namespace FormatConverter.Core.Engine;

/// <summary>转换后动作执行结果(Detail 为中文摘要,可直接展示)。</summary>
public sealed record PostActionOutcome(
    PostConversionAction Action,
    bool Success,
    string? Error,
    string? NewOutputPath,
    string Detail);

/// <summary>
/// 转换成功后的原文件动作:删除原文件 / 移入归档文件夹。
/// 动作失败不影响转换结果(仍计成功,错误记录在 PostAction 里);
/// DeleteSource 在源与输出同路径时绝不删除。
/// </summary>
public static class PostConversionActions
{
    /// <summary>仅对成功的转换结果执行其声明的后置动作。</summary>
    public static PostActionOutcome Apply(ConversionResult result, string? archiveFolder)
    {
        var action = result.Job.PostConversionAction;
        if (!result.Success)
            return new(action, false, "转换未成功,跳过动作。", null, "");
        if (action == PostConversionAction.None)
            return new(action, true, null, null, "");

        return action switch
        {
            PostConversionAction.DeleteSource => DeleteSource(result),
            PostConversionAction.MoveToArchiveFolder => MoveToArchive(result, archiveFolder),
            _ => new(action, true, null, null, ""),
        };
    }

    /// <summary>两个路径是否位于不同卷(跨卷 File.Move 会失败,需 Copy+Delete 回退)。</summary>
    public static bool IsCrossVolume(string a, string b)
    {
        try
        {
            return !string.Equals(
                Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static PostActionOutcome DeleteSource(ConversionResult result)
    {
        var source = result.Job.SourcePath;
        var output = result.OutputPath;
        try
        {
            // 源与输出同路径(如模板意外指向源)时绝不删除
            if (output is not null && SamePath(source, output))
                return new(PostConversionAction.DeleteSource, false,
                    "源文件与输出文件相同,不删除。", output, "未删除原文件(与输出相同)");

            if (!File.Exists(source))
                return new(PostConversionAction.DeleteSource, true, null, output, "原文件已不存在");

            File.Delete(source);
            return new(PostConversionAction.DeleteSource, true, null, output, "原文件已删除");
        }
        catch (Exception ex)
        {
            return new(PostConversionAction.DeleteSource, false, ex.Message, output, "删除原文件失败");
        }
    }

    private static PostActionOutcome MoveToArchive(ConversionResult result, string? archiveFolder)
    {
        var output = result.OutputPath;
        if (output is null)
            return new(PostConversionAction.MoveToArchiveFolder, false,
                "输出文件路径缺失。", null, "归档失败,输出文件保留");

        try
        {
            var dir = string.IsNullOrWhiteSpace(archiveFolder)
                ? Path.Combine(Path.GetDirectoryName(output)!, "归档")
                : archiveFolder;
            Directory.CreateDirectory(dir);
            var target = UniquePath(Path.Combine(dir, Path.GetFileName(output)));

            if (IsCrossVolume(output, target))
            {
                File.Copy(output, target);
                File.Delete(output);
            }
            else
            {
                File.Move(output, target);
            }
            return new(PostConversionAction.MoveToArchiveFolder, true, null, target, "已移入归档");
        }
        catch (Exception ex)
        {
            // 归档失败不删除输出文件,转换仍计成功
            return new(PostConversionAction.MoveToArchiveFolder, false, ex.Message, output, "归档失败,输出文件保留");
        }
    }

    /// <summary>归档目录内重名时自动 " (2)" 递增。</summary>
    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; i < 10_000; i++)
        {
            var next = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(next) && !Directory.Exists(next)) return next;
        }
        return path;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
