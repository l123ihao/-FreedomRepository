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
        new FormatInfo("flv",  FileCategory.Video,    "FLV 视频"),
        new FormatInfo("ts",   FileCategory.Video,    "TS 视频流"),
        new FormatInfo("m4v",  FileCategory.Video,    "M4V 视频"),
        new FormatInfo("3gp",  FileCategory.Video,    "3GP 手机视频"),
        new FormatInfo("wmv",  FileCategory.Video,    "WMV 视频"),
        new FormatInfo("mp3",  FileCategory.Audio,    "MP3 音频"),
        new FormatInfo("wav",  FileCategory.Audio,    "WAV 音频"),
        new FormatInfo("flac", FileCategory.Audio,    "FLAC 无损音频"),
        new FormatInfo("m4a",  FileCategory.Audio,    "M4A 音频"),
        new FormatInfo("aac",  FileCategory.Audio,    "AAC 音频"),
        new FormatInfo("ogg",  FileCategory.Audio,    "OGG 音频"),
        new FormatInfo("opus", FileCategory.Audio,    "Opus 音频"),
        new FormatInfo("aiff", FileCategory.Audio,    "AIFF 音频"),
        new FormatInfo("wma",  FileCategory.Audio,    "WMA 音频"),
        new FormatInfo("m4b",  FileCategory.Audio,    "M4B 有声书"),
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
        new FormatInfo("tiff", FileCategory.Image,    "TIFF 图片"),
    };

    private static readonly Dictionary<string, FormatInfo> ByExtension =
        AllFormats.ToDictionary(f => f.Extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 转换矩阵。gif 特殊:既是图片(静态互转)也可作为视频路由目标/来源(经 ffmpeg)。
    /// 视频源可直接提取为任意音频格式。
    /// </summary>
    private static readonly Dictionary<string, string[]> Matrix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp4"]  = ["mkv", "avi", "mov", "webm", "gif", "flv", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["mkv"]  = ["mp4", "flv", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["avi"]  = ["mp4", "flv", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["mov"]  = ["mp4", "flv", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["webm"] = ["mp4", "flv", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["flv"]  = ["mp4", "mkv", "avi", "mov", "webm", "ts", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["ts"]   = ["mp4", "mkv", "avi", "mov", "webm", "flv", "m4v", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["m4v"]  = ["mp4", "mkv", "avi", "mov", "webm", "flv", "ts", "3gp", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["3gp"]  = ["mp4", "mkv", "avi", "mov", "webm", "flv", "ts", "m4v", "wmv", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["wmv"]  = ["mp4", "mkv", "avi", "mov", "webm", "flv", "ts", "m4v", "3gp", "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["gif"]  = ["png", "jpg", "webp", "bmp", "ico", "tiff", "mp4"],

        ["mp3"]  = ["wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["wav"]  = ["mp3", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["flac"] = ["mp3", "wav", "m4a", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["m4a"]  = ["mp3", "wav", "flac", "aac", "ogg", "opus", "aiff", "wma", "m4b"],
        ["aac"]  = ["mp3", "wav", "flac", "m4a", "ogg", "opus", "aiff", "wma", "m4b"],
        ["ogg"]  = ["mp3", "wav", "flac", "m4a", "aac", "opus", "aiff", "wma", "m4b"],
        ["opus"] = ["mp3", "wav", "flac", "m4a", "aac", "ogg", "aiff", "wma", "m4b"],
        ["aiff"] = ["mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "wma", "m4b"],
        ["wma"]  = ["mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "m4b"],
        ["m4b"]  = ["mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "aiff", "wma"],

        ["docx"] = ["pdf", "txt", "md", "html"],
        ["txt"]  = ["docx", "pdf"],
        ["md"]   = ["docx", "html", "pdf"],
        ["pdf"]  = ["txt"],
        // 仅作来源:标题→Word 标题、正文逐段提取。旧版 .ppt(97-2003 二进制)不支持。
        ["pptx"] = ["docx", "txt", "pdf"],

        ["png"]  = ["jpg", "webp", "bmp", "gif", "ico", "tiff"],
        ["jpg"]  = ["png", "webp", "bmp", "gif", "ico", "tiff"],
        ["webp"] = ["png", "jpg", "bmp", "gif", "ico", "tiff"],
        ["bmp"]  = ["png", "jpg", "webp", "gif", "ico", "tiff"],
        ["ico"]  = ["png", "jpg", "webp", "bmp", "gif", "tiff"],
        ["tiff"] = ["png", "jpg", "webp", "bmp", "gif", "ico"],
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
