using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Models;

namespace FormatConverter.Core.Converters;

/// <summary>视频→视频(含视频→gif)与 gif→视频(保留动画)。</summary>
public sealed class FfmpegVideoConverter : FfmpegConverterBase
{
    public override bool CanConvert(ConversionJob job)
    {
        if (!FfmpegArgsBuilder.IsVideoTarget(job.TargetExtension)) return false;
        if (job.Category == FileCategory.Video) return true;
        // gif 注册在图片分类,但转视频走 ffmpeg 以保留动画(图片互转走 ImageConverter 取首帧)
        return job.Category == FileCategory.Image
               && string.Equals(Path.GetExtension(job.SourcePath), ".gif", StringComparison.OrdinalIgnoreCase);
    }
}
