namespace FormatConverter.Core.Models;

/// <summary>
/// 预设的参数覆盖:所有字段可空,null = 继承全局设置(ApplyTo 时逐字段回填)。
/// 覆盖不包含 OverwritePolicy——重名策略始终跟随全局设置。
/// </summary>
public sealed class ConversionOptionsOverride
{
    public int? AudioBitrateKbps { get; init; }

    public int? VideoCrf { get; init; }

    public int? GifFps { get; init; }

    public int? GifWidth { get; init; }

    public VideoMode? VideoMode { get; init; }

    public bool? HardwareAcceleration { get; init; }

    /// <summary>FFmpeg 自定义参数逃生舱(按空白/双引号切分,支持 {input}/{output} 占位符)。</summary>
    public string? CustomFfmpegArgs { get; init; }

    /// <summary>是否没有任何覆盖。</summary>
    public bool IsEmpty =>
        AudioBitrateKbps is null && VideoCrf is null && GifFps is null && GifWidth is null
        && VideoMode is null && HardwareAcceleration is null
        && string.IsNullOrWhiteSpace(CustomFfmpegArgs);

    /// <summary>以本覆盖逐字段覆写基础选项,返回新实例。</summary>
    public ConversionOptions ApplyTo(ConversionOptions @base) => new()
    {
        AudioBitrateKbps = AudioBitrateKbps ?? @base.AudioBitrateKbps,
        VideoCrf = VideoCrf ?? @base.VideoCrf,
        GifFps = GifFps ?? @base.GifFps,
        GifWidth = GifWidth ?? @base.GifWidth,
        OverwritePolicy = @base.OverwritePolicy,
        VideoMode = VideoMode ?? @base.VideoMode,
        HardwareAcceleration = HardwareAcceleration ?? @base.HardwareAcceleration,
        CustomFfmpegArgs = CustomFfmpegArgs ?? @base.CustomFfmpegArgs,
    };

    /// <summary>中文摘要(如 "CRF 18 · 码率 320k · 硬编关"),空 = 无覆盖。</summary>
    public string SummaryText
    {
        get
        {
            var parts = new List<string>();
            if (AudioBitrateKbps is { } k) parts.Add($"码率 {k}k");
            if (VideoCrf is { } crf) parts.Add($"CRF {crf}");
            if (GifFps is { } fps && GifWidth is { } w) parts.Add($"GIF {fps}fps/宽 {w}");
            else if (GifFps is { } f) parts.Add($"GIF {f}fps");
            else if (GifWidth is { } gw) parts.Add($"GIF 宽 {gw}");
            // 注意:属性 VideoMode 与类型 VideoMode 同名,这里必须用完全限定名(项目已知坑)
            if (VideoMode is { } vm) parts.Add(vm == Models.VideoMode.CopyFirst ? "无损优先" : "始终转码");
            if (HardwareAcceleration is { } hw) parts.Add(hw ? "硬编开" : "硬编关");
            if (!string.IsNullOrWhiteSpace(CustomFfmpegArgs)) parts.Add("自定义参数");
            return string.Join(" · ", parts);
        }
    }
}
