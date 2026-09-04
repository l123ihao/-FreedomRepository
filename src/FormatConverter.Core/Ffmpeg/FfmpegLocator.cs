namespace FormatConverter.Core.Ffmpeg;

/// <summary>
/// 定位打包的 ffmpeg/ffprobe。
/// 发布文件夹与单文件自解压两种部署下,AppContext.BaseDirectory 都指向可执行文件所在(解压)目录,
/// ffmpeg 子目录随 Content 拷贝到同一位置。
/// </summary>
public static class FfmpegLocator
{
    public static string FfmpegPath => Resolve("ffmpeg.exe", "ffmpeg");
    public static string FfprobePath => Resolve("ffprobe.exe", "ffprobe");

    public static bool IsAvailable => File.Exists(FfmpegPath) && File.Exists(FfprobePath);

    private static string Resolve(string fileName, string pathFallback)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg", fileName);
        return File.Exists(bundled) ? bundled : pathFallback;
    }
}
