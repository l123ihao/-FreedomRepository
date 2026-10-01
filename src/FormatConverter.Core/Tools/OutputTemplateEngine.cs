using System.Text;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Tools;

/// <summary>模板展开上下文(批次序号/总数与当前时间由调用方注入,便于测试)。</summary>
public sealed record TemplateContext(
    string SourcePath, string OutputExtension, int FileIndex, int FileCount, DateTime Now);

/// <summary>模板语法错误(中文消息,可直接展示给用户)。</summary>
public sealed class OutputTemplateException : Exception
{
    public OutputTemplateException(string message) : base(message) { }
}

/// <summary>
/// 输出文件名模板引擎(语法对标 File Converter):
/// (p) 所在目录 (f)/(F) 文件名 (i)/(I) 输入扩展名 (o)/(O) 输出扩展名
/// (p:documents|music|videos|pictures|desktop)(及 (p:d)(p:m)(p:v)(p:p) 短名)
/// (d0)(d1)… 目录层级(往上,越界空串) (n:i) 序号 (n:c) 总数 (d:格式串) 日期。
/// 含 (p) 系令牌的模板展开为完整路径,否则视为相对文件名(目录由调用方决定)。
/// </summary>
public static class OutputTemplateEngine
{
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '|', '?', '*'];

    /// <summary>展开模板;语法错误抛 OutputTemplateException。</summary>
    public static string Expand(string template, TemplateContext ctx)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new OutputTemplateException("文件名模板不能为空。");

        var sb = new StringBuilder(template.Length + 16);
        for (var i = 0; i < template.Length;)
        {
            var c = template[i];
            if (c == '(')
            {
                var end = template.IndexOf(')', i + 1);
                if (end < 0) throw new OutputTemplateException("模板括号不配对:缺少 ')'。");
                var token = template.Substring(i + 1, end - i - 1);
                if (token.Length == 0) throw new OutputTemplateException("模板包含空括号 \"()\"。");
                sb.Append(ExpandToken(token, ctx));
                i = end + 1;
            }
            else if (c == ')')
            {
                throw new OutputTemplateException("模板括号不配对:多余的 ')'。");
            }
            else
            {
                if (InvalidChars.Contains(c))
                    throw new OutputTemplateException($"模板包含非法字符 '{c}'(不允许 < > : \" | ? *)。");
                sb.Append(c);
                i++;
            }
        }
        // '/' 允许作为子目录分隔符书写,统一规范化为平台分隔符
        return sb.ToString().Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>编辑器实时校验:合法返回 true,否则输出中文错误。</summary>
    public static bool TryValidate(string template, out string? error)
    {
        try
        {
            Expand(template, new TemplateContext(@"C:\示例\文件.mp4", "mp4", 1, 1, DateTime.Now));
            error = null;
            return true;
        }
        catch (OutputTemplateException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = $"模板无效: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 展开模板并解析为最终输出路径:目录定基、扩展名兜底、按 OverwritePolicy 处理冲突
    /// (Rename 时从 " (2)" 起递增,跳过磁盘已有与批次黑名单,上限 9999)。
    /// </summary>
    public static string Resolve(
        string sourcePath, string targetExtension, string template,
        string outputDirectory, bool outputToSourceFolder, OverwritePolicy policy,
        TemplateContext ctx, HashSet<string>? blacklist)
    {
        var expanded = Expand(template, ctx);
        string candidate;
        if (ContainsPathToken(template))
        {
            candidate = expanded;
        }
        else
        {
            var dir = outputToSourceFolder || string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.GetDirectoryName(sourcePath)!
                : outputDirectory;
            candidate = Path.Combine(dir, expanded);
        }

        if (string.IsNullOrEmpty(Path.GetExtension(candidate)))
            candidate += "." + targetExtension;

        candidate = candidate.Replace('/', Path.DirectorySeparatorChar);
        return EnsureUnique(candidate, sourcePath, policy, blacklist);
    }

    /// <summary>模板是否含 (p) 或 (p:xxx) 系绝对路径令牌。</summary>
    private static bool ContainsPathToken(string template)
    {
        for (var i = 0; i < template.Length - 2; i++)
        {
            if (template[i] != '(') continue;
            var j = template.IndexOf(')', i + 1);
            if (j < 0) return false; // 语法错误交给 Expand 抛
            var token = template.Substring(i + 1, j - i - 1);
            if (token == "p" || token.StartsWith("p:", StringComparison.Ordinal)) return true;
            i = j;
        }
        return false;
    }

    private static string ExpandToken(string token, TemplateContext ctx)
    {
        var inputExt = Path.GetExtension(ctx.SourcePath).TrimStart('.');
        var inputName = Path.GetFileNameWithoutExtension(ctx.SourcePath);
        var dir = Path.GetDirectoryName(ctx.SourcePath) ?? "";

        switch (token)
        {
            // (p)/(p:xxx) 展开为带尾部分隔符的绝对目录(与 File Converter 行为一致,便于 "(p)(f)" 直接拼接)
            case "p": return AppendSeparator(dir);
            case "f": return inputName;
            case "F": return inputName.ToUpperInvariant();
            case "i": return inputExt;
            case "I": return inputExt.ToUpperInvariant();
            case "o": return ctx.OutputExtension;
            case "O": return ctx.OutputExtension.ToUpperInvariant();
            case "p:d" or "p:documents": return AppendSeparator(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            case "p:m" or "p:music": return AppendSeparator(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            case "p:v" or "p:videos": return AppendSeparator(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            case "p:p" or "p:pictures": return AppendSeparator(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            case "p:desktop": return AppendSeparator(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            case "n:i": return ctx.FileIndex.ToString();
            case "n:c": return ctx.FileCount.ToString();
        }

        if (token.StartsWith("d:", StringComparison.Ordinal))
            return ExpandDate(token, ctx.Now);

        if (token.Length >= 2 && (token[0] == 'd' || token[0] == 'D')
            && int.TryParse(token.AsSpan(1), out var level) && level >= 0)
            return ExpandDirectoryLevel(ctx.SourcePath, level, token[0] == 'D');

        throw new OutputTemplateException($"未知占位符 \"({token})\"。");
    }

    private static string AppendSeparator(string path) =>
        path.Length == 0 || path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string ExpandDate(string token, DateTime now)
    {
        var format = token[2..];
        try
        {
            // '/'、':' 在文件名中非法,按 File Converter 惯例替换
            return now.ToString(format).Replace('/', '-').Replace(':', '\'');
        }
        catch (FormatException)
        {
            throw new OutputTemplateException($"日期格式 \"{format}\" 无效。");
        }
    }

    /// <summary>(d0)=文件所在目录名,(d1)=父目录名…;越界返回空串。</summary>
    private static string ExpandDirectoryLevel(string sourcePath, int level, bool upper)
    {
        var current = Path.GetDirectoryName(sourcePath);
        for (var n = 0; n <= level; n++)
        {
            if (string.IsNullOrEmpty(current)) return "";
            var name = Path.GetFileName(current);
            if (n == level) return upper ? name.ToUpperInvariant() : name;
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                return ""; // 已到根,越界
            current = parent;
        }
        return "";
    }

    private static string EnsureUnique(
        string candidate, string sourcePath, OverwritePolicy policy, HashSet<string>? blacklist)
    {
        if (!IsConflict(candidate, sourcePath, blacklist)) return candidate;
        if (policy == OverwritePolicy.Overwrite) return candidate;

        var dir = Path.GetDirectoryName(candidate)!;
        var name = Path.GetFileNameWithoutExtension(candidate);
        var ext = Path.GetExtension(candidate);
        for (var i = 2; i < 10_000; i++)
        {
            var next = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!IsConflict(next, sourcePath, blacklist)) return next;
        }
        throw new OutputTemplateException($"无法为 \"{candidate}\" 生成不冲突的文件名。");
    }

    private static bool IsConflict(string path, string sourcePath, HashSet<string>? blacklist)
        => File.Exists(path) || Directory.Exists(path)
           || SamePath(path, sourcePath)
           || (blacklist?.Contains(path) ?? false);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
