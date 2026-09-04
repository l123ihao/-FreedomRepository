using FormatConverter.Core.Models;

namespace FormatConverter.Core.Converters;

/// <summary>按 CanConvert 顺序匹配转换器(媒体/图片/文档路由互不重叠)。</summary>
public sealed class ConverterFactory
{
    private readonly IReadOnlyList<IConverter> _converters;

    public ConverterFactory(params IConverter[] converters) => _converters = converters;

    public static ConverterFactory CreateDefault() => new(
        new FfmpegVideoConverter(),
        new FfmpegAudioConverter(),
        new ImageConverter(),
        new DocumentConverter());

    public IConverter? GetConverter(ConversionJob job) =>
        _converters.FirstOrDefault(c => c.CanConvert(job));
}
