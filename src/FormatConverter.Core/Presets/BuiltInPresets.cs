using FormatConverter.Core.Models;

namespace FormatConverter.Core.Presets;

/// <summary>
/// 出厂内置预设:固定 Id(builtin.*),不进 settings.json;可删除(记入 DeletedBuiltInPresetIds)、不可编辑。
/// 新增/删除内置预设只需改本表,用户数据不动。
/// </summary>
public static class BuiltInPresets
{
    public static readonly IReadOnlyList<ConversionPreset> Defaults = new[]
    {
        new ConversionPreset
        {
            Id = "builtin.hq-mp4",
            Name = "高清 MP4",
            TargetExtension = "mp4",
            Options = new ConversionOptionsOverride { VideoCrf = 18, VideoMode = VideoMode.AlwaysTranscode },
            IsBuiltIn = true,
        },
        new ConversionPreset
        {
            Id = "builtin.mp3-320",
            Name = "高品质 MP3",
            TargetExtension = "mp3",
            OutputFileNameTemplate = "(p)(f)",
            Options = new ConversionOptionsOverride { AudioBitrateKbps = 320 },
            IsBuiltIn = true,
        },
        new ConversionPreset
        {
            Id = "builtin.webp-photo",
            Name = "WebP 压缩",
            TargetExtension = "webp",
            OutputFileNameTemplate = "(p)(f)_webp",
            IsBuiltIn = true,
        },
        new ConversionPreset
        {
            Id = "builtin.pdf-archive",
            Name = "转 PDF 归档",
            TargetExtension = "pdf",
            OutputFileNameTemplate = "(f)",
            PostConversionAction = PostConversionAction.MoveToArchiveFolder,
            IsBuiltIn = true,
        },
        new ConversionPreset
        {
            Id = "builtin.mkv-copy",
            Name = "无损 MKV",
            TargetExtension = "mkv",
            Options = new ConversionOptionsOverride
            {
                VideoMode = VideoMode.CopyFirst,
                HardwareAcceleration = false,
            },
            IsBuiltIn = true,
        },
    };
}
