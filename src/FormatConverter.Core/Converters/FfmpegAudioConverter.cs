using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Converters;

/// <summary>视频/音频 → 音频(提取或转码音轨)。</summary>
public sealed class FfmpegAudioConverter : FfmpegConverterBase
{
    public override bool CanConvert(ConversionJob job) =>
        FfmpegArgsBuilder.IsAudioTarget(job.TargetExtension)
        && job.Category is FileCategory.Video or FileCategory.Audio;
}
