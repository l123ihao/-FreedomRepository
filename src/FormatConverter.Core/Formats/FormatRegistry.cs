using FormatConverter.Core.Models;

namespace FormatConverter.Core.Formats;

/// <summary>
/// 格式注册表:声明全部受支持格式与"源格式 → 合法目标格式"转换矩阵。
/// UI 的每文件目标下拉框与 ConverterFactory 的路由都从本表取数。
/// </summary>
public static class FormatRegistry
{
    public static readonly IReadOnlyList<FormatInfo> AllFormats = new[]
    {
        new FormatInfo("mp4",  FileCategory.Video,    "MP4 视频"),
        new FormatInfo("mkv",  FileCategory.Video,    "MKV 视频"),
        new FormatInfo("avi",  FileCategory.Video,    "AVI 视频"),
        new FormatInfo("mov",  FileCategory.Video,    "MOV 视频"),
        new FormatInfo("webm", FileCategory.Video,    "WebM 视频"),
        new FormatInfo("mp3",  FileCategory.Audio,    "MP3 音频"),
        new FormatInfo("wav",  FileCategory.Audio,    "WAV 音频"),
        new FormatInfo("flac", FileCategory.Audio,    "FLAC 无损音频"),
        new FormatInfo("m4a",  FileCategory.Audio,    "M4A 音频"),
        new FormatInfo("aac",  FileCategory.Audio,    "AAC 音频"),
        new FormatInfo("ogg",  FileCategory.Audio,    "OGG 音频"),
        new FormatInfo("docx", FileCategory.Document, "Word 文档"),
        new FormatInfo("txt",  FileCategory.Document, "纯文本"),
        new FormatInfo("pdf",  FileCategory.Document, "PDF 文档"),
        new FormatInfo("md",   FileCategory.Document, "Markdown"),
        new FormatInfo("html", FileCategory.Document, "HTML 网页"),
        new FormatInfo("pptx", FileCategory.Document, "PPT 演示文稿"),
        new FormatInfo("png",  FileCategory.Image,    "PNG 图片"),
        new FormatInfo("jpg",  FileCategory.Image,    "JPG 图片"),
        new FormatInfo("webp", FileCategory.Image,    "WebP 图片"),
        new FormatInfo("bmp",  FileCategory.Image,    "BMP 图片"),
        new FormatInfo("gif",  FileCategory.Image,    "GIF 图片"),
        new FormatInfo("ico",  FileCategory.Image,    "ICO 图标"),
    };

    private static readonly Dictionary<string, FormatInfo> ByExtension =
        AllFormats.ToDictionary(f => f.Extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 转换矩阵。gif 特殊:既是图片(静态互转)也可作为视频路由目标/来源(经 ffmpeg)。
    /// 视频源可直接提取为任意音频格式。
    /// </summary>
    private static readonly Dictionary<string, string[]> Matrix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp4"]  = ["mkv", "avi", "mov", "webm", "gif", "mp3", "wav", "flac", "m4a", "aac", "ogg"],
        ["mkv"]  = ["mp4", "mp3", "wav", "flac", "m4a", "aac", "ogg"],
        ["avi"]  = ["mp4", "mp3", "wav", "flac", "m4a", "aac", "ogg"],
        ["mov"]  = ["mp4", "mp3", "wav", "flac", "m4a", "aac", "ogg"],
        ["webm"] = ["mp4", "mp3", "wav", "flac", "m4a", "aac", "ogg"],
        ["gif"]  = ["png", "jpg", "webp", "bmp", "ico", "mp4"],

        ["mp3"]  = ["wav", "flac", "m4a", "aac", "ogg"],
        ["wav"]  = ["mp3", "flac", "m4a", "aac", "ogg"],
        ["flac"] = ["mp3", "wav", "m4a", "aac", "ogg"],
        ["m4a"]  = ["mp3", "wav", "flac", "aac", "ogg"],
        ["aac"]  = ["mp3", "wav", "flac", "m4a", "ogg"],
        ["ogg"]  = ["mp3", "wav", "flac", "m4a", "aac"],

        ["docx"] = ["pdf", "txt", "md", "html"],
        ["txt"]  = ["docx", "pdf"],
        ["md"]   = ["docx", "html", "pdf"],
        ["pdf"]  = ["txt"],
        // 仅作来源:标题→Word 标题、正文逐段提取。旧版 .ppt(97-2003 二进制)不支持。
        ["pptx"] = ["docx", "txt", "pdf"],

        ["png"]  = ["jpg", "webp", "bmp", "gif", "ico"],
        ["jpg"]  = ["png", "webp", "bmp", "gif", "ico"],
        ["webp"] = ["png", "jpg", "bmp", "gif", "ico"],
        ["bmp"]  = ["png", "jpg", "webp", "gif", "ico"],
        ["ico"]  = ["png", "jpg", "webp", "bmp", "gif"],
    };

    /// <summary>按扩展名查格式(忽略大小写,不含点)。</summary>
    public static FormatInfo? Find(string extension)
    {
        var ext = extension.TrimStart('.');
        return ByExtension.GetValueOrDefault(ext);
    }

    /// <summary>判断扩展名是否受支持(可作为转换来源)。</summary>
    public static bool IsSupported(string extension) => Find(extension) is not null;

    /// <summary>该扩展名是否可作为转换目标(出现在任意转换矩阵的右侧)。pptx 等纯来源格式返回 false。</summary>
    public static bool IsTargetFormat(string extension)
    {
        var ext = extension.TrimStart('.');
        return Matrix.Values.Any(v => v.Contains(ext, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>取文件的格式类别;未知扩展名返回 null。参数可以是完整路径或裸扩展名。</summary>
    public static FileCategory? GetCategoryOrNull(string pathOrExtension)
    {
        var ext = Path.GetExtension(pathOrExtension).TrimStart('.');
        if (ext.Length == 0) ext = pathOrExtension.TrimStart('.'); // 裸扩展名(如 "mp4")
        return ByExtension.TryGetValue(ext, out var info) ? info.Category : null;
    }

    public static FileCategory GetCategory(string pathOrExtension)
        => GetCategoryOrNull(pathOrExtension)
           ?? throw new ArgumentException($"不支持的扩展名: {pathOrExtension}");

    /// <summary>该源格式可转换到的全部目标格式;空表示不支持转换。</summary>
    public static IReadOnlyList<FormatInfo> GetTargets(string sourceExtension)
    {
        var ext = sourceExtension.TrimStart('.');
        if (!Matrix.TryGetValue(ext, out var targets)) return Array.Empty<FormatInfo>();
        return targets.Select(t => ByExtension[t]).ToList();
    }

    /// <summary>默认目标格式:类别默认(视频→mp4、音频→mp3、文档→pdf、图片→png),不在列表时取第一项。</summary>
    public static FormatInfo? GetDefaultTarget(string sourceExtension)
    {
        var targets = GetTargets(sourceExtension);
        if (targets.Count == 0) return null;

        var categoryDefault = GetCategory(sourceExtension) switch
        {
            FileCategory.Video => "mp4",
            FileCategory.Audio => "mp3",
            FileCategory.Document => "pdf",
            FileCategory.Image => "png",
            _ => null,
        };
        return targets.FirstOrDefault(t => t.Extension == categoryDefault) ?? targets[0];
    }
}
