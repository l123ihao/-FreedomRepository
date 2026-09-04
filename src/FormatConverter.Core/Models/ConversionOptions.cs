namespace FormatConverter.Core.Models;

/// <summary>输出文件已存在时的处理策略。</summary>
public enum OverwritePolicy
{
    /// <summary>覆盖已有文件。</summary>
    Overwrite,

    /// <summary>自动重命名(追加 " (1)"、" (2)"…)。</summary>
    Rename,
}

/// <summary>视频容器互转策略。</summary>
public enum VideoMode
{
    /// <summary>目标容器兼容源流时优先 -c copy(秒级),否则转码。</summary>
    CopyFirst,

    /// <summary>始终重新编码。</summary>
    AlwaysTranscode,
}

/// <summary>转换参数(全局默认值,Phase 1 不做每文件细调 UI)。</summary>
public sealed class ConversionOptions
{
    /// <summary>音频码率(kbps,mp3/aac/m4a 用;wav/flac 无损忽略)。</summary>
    public int AudioBitrateKbps { get; init; } = 192;

    /// <summary>视频转码 CRF 质量参数(越小越清晰,默认 23)。</summary>
    public int VideoCrf { get; init; } = 23;

    /// <summary>转 GIF 的帧率。</summary>
    public int GifFps { get; init; } = 12;

    /// <summary>转 GIF 的最大宽度(像素,防体积爆炸)。</summary>
    public int GifWidth { get; init; } = 480;

    public OverwritePolicy OverwritePolicy { get; init; } = OverwritePolicy.Rename;

    public VideoMode VideoMode { get; init; } = VideoMode.CopyFirst;
}
