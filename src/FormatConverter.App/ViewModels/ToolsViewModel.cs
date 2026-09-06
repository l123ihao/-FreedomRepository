using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Tools;
using Microsoft.Win32;

namespace FormatConverter.App.ViewModels;

/// <summary>工具页 VM:媒体信息 / 格式检测 / 图片压缩 / 缩放 / 裁剪 / 水印 / 批量重命名。</summary>
public partial class ToolsViewModel : ObservableObject
{
    public sealed record ToolItem(string Key, string Name, string Icon);

    public sealed record WatermarkPositionOption(string Label, WatermarkPosition Value);

    public IReadOnlyList<ToolItem> Tools { get; } = new[]
    {
        new ToolItem("media", "媒体信息", "🎬"),
        new ToolItem("detect", "格式检测", "🔍"),
        new ToolItem("compress", "图片压缩", "🗜"),
        new ToolItem("resize", "图片缩放", "↔"),
        new ToolItem("crop", "图片裁剪", "✂"),
        new ToolItem("watermark", "图片水印", "🏷"),
        new ToolItem("rename", "批量重命名", "✏"),
        new ToolItem("pdfmerge", "PDF 合并", "📑"),
        new ToolItem("pdfsplit", "PDF 拆分", "✂"),
        new ToolItem("trim", "视频剪辑", "🎞"),
        new ToolItem("frames", "视频抽帧", "🖼"),
        new ToolItem("gif", "视频转 GIF", "🎬"),
        new ToolItem("audio", "音频增强", "🔊"),
    };

    [ObservableProperty]
    private ToolItem selectedTool = null!;

    // ---- 媒体信息 / 格式检测 ----
    [ObservableProperty]
    private string mediaInfoResult = "选择视频或音频文件,查看时长 / 分辨率 / 编码 / 码率。";

    [ObservableProperty]
    private string detectResult = "选择文件,按文件头魔数识别真实格式(不受扩展名误导)。";

    // ---- 图片工具共享状态 ----
    [ObservableProperty]
    private string imageInputPath = "";

    [ObservableProperty]
    private string imageStatus = "先选择图片,再执行工具。";

    // ---- 压缩 ----
    [ObservableProperty]
    private int compressQuality = 80;

    [ObservableProperty]
    private int compressMaxWidth;

    [ObservableProperty]
    private int compressMaxHeight;

    // ---- 缩放 ----
    [ObservableProperty]
    private int resizePercent;

    [ObservableProperty]
    private int resizeWidth;

    [ObservableProperty]
    private int resizeHeight;

    // ---- 裁剪 ----
    [ObservableProperty]
    private int cropWidth = 800;

    [ObservableProperty]
    private int cropHeight = 600;

    // ---- 水印 ----
    [ObservableProperty]
    private string watermarkText = "";

    [ObservableProperty]
    private string watermarkImagePath = "";

    [ObservableProperty]
    private WatermarkPositionOption watermarkPosition;

    [ObservableProperty]
    private int watermarkFontSize = 24;

    [ObservableProperty]
    private double watermarkOpacity = 0.7;

    public IReadOnlyList<WatermarkPositionOption> WatermarkPositions { get; } = new[]
    {
        new WatermarkPositionOption("左上", Core.Tools.WatermarkPosition.TopLeft),
        new WatermarkPositionOption("右上", Core.Tools.WatermarkPosition.TopRight),
        new WatermarkPositionOption("左下", Core.Tools.WatermarkPosition.BottomLeft),
        new WatermarkPositionOption("右下", Core.Tools.WatermarkPosition.BottomRight),
        new WatermarkPositionOption("居中", Core.Tools.WatermarkPosition.Center),
    };

    // ---- 批量重命名 ----
    public ObservableCollection<string> RenameFiles { get; } = new();
    public ObservableCollection<RenamePreviewItem> RenamePreview { get; } = new();

    [ObservableProperty]
    private string renameTemplate = "{name}-{n}";

    [ObservableProperty]
    private int renameStartNumber = 1;

    [ObservableProperty]
    private string renameStatus = "选择文件,填写模板后先预览,再执行。";

    public ToolsViewModel()
    {
        selectedTool = Tools[0];
        watermarkPosition = WatermarkPositions[3];
    }

    // ---------- 媒体信息 ----------

    [RelayCommand]
    private async Task SelectMediaFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "媒体文件|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg",
        };
        if (dlg.ShowDialog() != true) return;
        var path = dlg.FileName;

        var lines = new List<string>
        {
            $"文件: {path}",
            $"大小: {FormatBytes(new FileInfo(path).Length)}",
        };

        var probe = await FfmpegProbe.ProbeAsync(path);
        if (probe is null)
        {
            lines.Add("未检测到 ffmpeg,或无法解析该媒体文件。");
        }
        else
        {
            lines.Add($"时长: {FormatDuration(probe.DurationSeconds)}");
            var video = probe.Streams.FirstOrDefault(s => s.CodecType == "video");
            if (video is not null)
            {
                lines.Add($"视频编码: {video.CodecName}" + (video.Width > 0 ? $",分辨率: {video.Width}×{video.Height}" : ""));
                if (video.BitRate > 0) lines.Add($"视频码率: {FormatBitRate(video.BitRate)}");
            }
            var audio = probe.Streams.FirstOrDefault(s => s.CodecType == "audio");
            if (audio is not null)
            {
                lines.Add($"音频编码: {audio.CodecName}" + (audio.BitRate > 0 ? $",码率: {FormatBitRate(audio.BitRate)}" : ""));
            }
            if (probe.BitRate > 0) lines.Add($"整体码率: {FormatBitRate(probe.BitRate)}");
        }

        MediaInfoResult = string.Join(Environment.NewLine, lines);
    }

    // ---------- 格式检测 ----------

    [RelayCommand]
    private void SelectDetectFile()
    {
        var dlg = new OpenFileDialog { Filter = "所有文件|*.*" };
        if (dlg.ShowDialog() != true) return;
        var path = dlg.FileName;

        var ext = Path.GetExtension(path).TrimStart('.');
        var detected = FormatDetector.DetectFile(path);

        if (detected is null)
        {
            DetectResult = $"文件: {path}\n扩展名: .{ext}\n未识别的文件头(可能是不支持或未知的格式)。";
        }
        else
        {
            var match = detected.Equals(ext, StringComparison.OrdinalIgnoreCase)
                        || (detected == "jpg" && ext == "jpeg")
                        || (detected == "zip" && ext is "docx" or "pptx" or "xlsx");
            DetectResult = $"文件: {path}\n扩展名: .{ext}\n真实格式: {detected}\n{(match ? "✓ 扩展名与内容一致" : "⚠ 扩展名与真实格式不符,请留意")}";
        }
    }

    // ---------- 图片工具 ----------

    [RelayCommand]
    private void SelectImage()
    {
        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif" };
        if (dlg.ShowDialog() != true) return;
        ImageInputPath = dlg.FileName;
        ImageStatus = "已选择图片,可执行工具。";
    }

    [RelayCommand]
    private void SelectWatermarkImage()
    {
        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
        if (dlg.ShowDialog() == true) WatermarkImagePath = dlg.FileName;
    }

    [RelayCommand]
    private async Task CompressImageAsync()
    {
        if (!EnsureImage(out var input)) return;
        var output = OutputPath(input, "_compressed", ".jpg");
        try
        {
            await ImageTools.CompressAsync(input, output,
                new CompressOptions(CompressQuality, CompressMaxWidth, CompressMaxHeight));
            ImageStatus = $"✓ 压缩完成: {output}";
        }
        catch (Exception ex)
        {
            ImageStatus = $"压缩失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResizeImageAsync()
    {
        if (!EnsureImage(out var input)) return;
        var output = OutputPath(input, "_resized", null);
        var options = ResizePercent > 0
            ? new ResizeOptions(Percent: ResizePercent)
            : new ResizeOptions(ResizeWidth > 0 ? ResizeWidth : null, ResizeHeight > 0 ? ResizeHeight : null);
        try
        {
            await ImageTools.ResizeAsync(input, output, options);
            ImageStatus = $"✓ 缩放完成: {output}";
        }
        catch (Exception ex)
        {
            ImageStatus = $"缩放失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CropImageAsync()
    {
        if (!EnsureImage(out var input)) return;
        var output = OutputPath(input, "_cropped", null);
        try
        {
            await ImageTools.CropAsync(input, output, new CropOptions(CropWidth, CropHeight));
            ImageStatus = $"✓ 裁剪完成: {output}";
        }
        catch (Exception ex)
        {
            ImageStatus = $"裁剪失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task WatermarkImageAsync()
    {
        if (!EnsureImage(out var input)) return;
        var output = OutputPath(input, "_watermarked", null);
        try
        {
            await ImageTools.WatermarkAsync(input, output, new WatermarkOptions(
                Text: string.IsNullOrWhiteSpace(WatermarkText) ? null : WatermarkText.Trim(),
                ImagePath: string.IsNullOrWhiteSpace(WatermarkImagePath) ? null : WatermarkImagePath,
                Position: WatermarkPosition.Value,
                FontSize: WatermarkFontSize,
                Opacity: (float)WatermarkOpacity));
            ImageStatus = $"✓ 水印完成: {output}";
        }
        catch (Exception ex)
        {
            ImageStatus = $"水印失败: {ex.Message}";
        }
    }

    private bool EnsureImage(out string input)
    {
        input = ImageInputPath;
        if (string.IsNullOrEmpty(input) || !File.Exists(input))
        {
            ImageStatus = "请先选择图片。";
            return false;
        }
        return true;
    }

    private static string OutputPath(string input, string suffix, string? overrideExtension)
    {
        var dir = Path.GetDirectoryName(input)!;
        var name = Path.GetFileNameWithoutExtension(input);
        var ext = overrideExtension is null ? Path.GetExtension(input) : overrideExtension;
        return Path.Combine(dir, $"{name}{suffix}{ext}");
    }

    // ---------- 批量重命名 ----------

    [RelayCommand]
    private void SelectRenameFiles()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "所有文件|*.*" };
        if (dlg.ShowDialog() != true) return;
        RenameFiles.Clear();
        RenamePreview.Clear();
        foreach (var f in dlg.FileNames) RenameFiles.Add(f);
        RenameStatus = $"已选择 {RenameFiles.Count} 个文件。";
    }

    [RelayCommand]
    private void PreviewRename()
    {
        RenamePreview.Clear();
        if (RenameFiles.Count == 0)
        {
            RenameStatus = "请先选择文件。";
            return;
        }
        var preview = RenameService.Preview(RenameFiles, RenameTemplate, RenameStartNumber);
        foreach (var item in preview) RenamePreview.Add(item);
        var conflicts = preview.Count(p => p.HasConflict);
        RenameStatus = conflicts > 0
            ? $"预览完成,{conflicts} 个存在冲突(执行时自动跳过)。"
            : "预览完成,无冲突。";
    }

    [RelayCommand]
    private async Task ApplyRenameAsync()
    {
        if (RenameFiles.Count == 0)
        {
            RenameStatus = "请先选择文件。";
            return;
        }
        await Task.Yield(); // 保持 UI 响应
        var results = RenameService.Apply(RenameFiles, RenameTemplate, RenameStartNumber);
        var ok = results.Count(r => r.Success);
        var fail = results.Count - ok;
        RenameStatus = $"重命名完成: {ok} 个成功" + (fail > 0 ? $",{fail} 个失败" : "") + "。";

        RenameFiles.Clear();
        RenamePreview.Clear();
        foreach (var r in results.Where(r => r.Success && r.NewPath is not null))
            RenameFiles.Add(r.NewPath!);
    }

    // ---------- 格式化 ----------

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / 1073741824.0:0.00} GB",
        >= 1L << 20 => $"{bytes / 1048576.0:0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };

    private static string FormatDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }

    private static string FormatBitRate(double bps) => bps switch
    {
        >= 1_000_000 => $"{bps / 1_000_000:0.#} Mbps",
        >= 1_000 => $"{bps / 1_000:0} kbps",
        _ => $"{bps:0} bps",
    };

    // ---------- PDF 合并 / 拆分 ----------

    public ObservableCollection<string> PdfInputFiles { get; } = new();

    [ObservableProperty]
    private string pdfStatus = "添加 PDF 文件后合并,或选单个 PDF 拆分。";

    [ObservableProperty]
    private string pdfRangeText = "1-3,5";

    [ObservableProperty]
    private int pdfSplitEveryN = 1;

    [RelayCommand]
    private void SelectPdfFiles()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "PDF|*.pdf" };
        if (dlg.ShowDialog() != true) return;
        PdfInputFiles.Clear();
        foreach (var f in dlg.FileNames) PdfInputFiles.Add(f);
        PdfStatus = $"已选择 {PdfInputFiles.Count} 个 PDF。";
    }

    [RelayCommand]
    private async Task MergePdfAsync()
    {
        if (PdfInputFiles.Count == 0)
        {
            PdfStatus = "请先添加 PDF 文件。";
            return;
        }
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "merged.pdf" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var files = PdfInputFiles.ToList();
            await Task.Run(() => PdfTools.Merge(files, dlg.FileName));
            PdfStatus = $"✓ 合并完成: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            PdfStatus = $"合并失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectPdfSplitFile()
    {
        var dlg = new OpenFileDialog { Filter = "PDF|*.pdf" };
        if (dlg.ShowDialog() != true) return;
        PdfInputFiles.Clear();
        PdfInputFiles.Add(dlg.FileName);
        PdfStatus = $"已选择: {dlg.FileName}";
    }

    [RelayCommand]
    private async Task SplitPdfByRangesAsync()
    {
        if (PdfInputFiles.Count == 0)
        {
            PdfStatus = "请先选择 PDF 文件。";
            return;
        }
        try
        {
            var ranges = ParseRanges(PdfRangeText);
            var input = PdfInputFiles[0];
            var outDir = Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "_split");
            var outputs = await Task.Run(() => PdfTools.SplitByRanges(input, outDir, ranges));
            PdfStatus = $"✓ 已拆分为 {outputs.Count} 个文件,输出目录: {outDir}";
        }
        catch (Exception ex)
        {
            PdfStatus = $"拆分失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SplitPdfEveryNAsync()
    {
        if (PdfInputFiles.Count == 0)
        {
            PdfStatus = "请先选择 PDF 文件。";
            return;
        }
        try
        {
            var input = PdfInputFiles[0];
            var outDir = Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "_split");
            var outputs = await Task.Run(() => PdfTools.SplitEveryN(input, outDir, Math.Max(1, PdfSplitEveryN)));
            PdfStatus = $"✓ 已拆分为 {outputs.Count} 个文件,输出目录: {outDir}";
        }
        catch (Exception ex)
        {
            PdfStatus = $"拆分失败: {ex.Message}";
        }
    }

    private static List<PageRange> ParseRanges(string text)
    {
        var ranges = new List<PageRange>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var seg = part.Split('-');
            if (seg.Length == 1 && int.TryParse(seg[0], out var single))
                ranges.Add(new PageRange(single, single));
            else if (seg.Length == 2 && int.TryParse(seg[0], out var s) && int.TryParse(seg[1], out var e))
                ranges.Add(new PageRange(s, e));
            else
                throw new FormatException($"无法解析范围「{part}」");
        }
        if (ranges.Count == 0) throw new FormatException("请输入有效页码范围,如 1-3,5");
        return ranges;
    }

    // ---------- 视频 / 音频工具 ----------

    [ObservableProperty]
    private string videoInputPath = "";

    [ObservableProperty]
    private string videoStatus = "选择视频/音频文件后执行工具(需 ffmpeg)。";

    [ObservableProperty]
    private string trimStart = "";

    [ObservableProperty]
    private string trimEnd = "";

    [ObservableProperty]
    private string extractFps = "1";

    [ObservableProperty]
    private string thumbCols = "3";

    [ObservableProperty]
    private string thumbRows = "3";

    [ObservableProperty]
    private string thumbInterval = "10";

    [ObservableProperty]
    private string gifWidth = "320";

    [ObservableProperty]
    private string gifFps = "10";

    [ObservableProperty]
    private string gifStart = "";

    [ObservableProperty]
    private string gifDuration = "";

    [ObservableProperty]
    private string volumeValue = "1.0";

    [ObservableProperty]
    private string fadeInSeconds = "";

    [ObservableProperty]
    private string fadeOutSeconds = "";

    [ObservableProperty]
    private string audioOutputFormat = "mp3";

    public string[] AudioOutputFormats { get; } = { "mp3", "m4a", "wav", "flac", "ogg" };

    [RelayCommand]
    private void SelectVideo()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "媒体文件|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg",
        };
        if (dlg.ShowDialog() != true) return;
        VideoInputPath = dlg.FileName;
        VideoStatus = "已选择文件,可执行工具。";
    }

    private bool EnsureVideo(out string input)
    {
        input = VideoInputPath;
        if (string.IsNullOrEmpty(input) || !File.Exists(input))
        {
            VideoStatus = "请先选择视频/音频文件。";
            return false;
        }
        return true;
    }

    private static string VideoOutput(string input, string suffix, string extension)
    {
        var dir = Path.GetDirectoryName(input)!;
        var name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{name}{suffix}.{extension}");
    }

    private static double? ParseSeconds(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;

    private static double ParseDouble(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, out var v) && v > 0 ? v : fallback;

    [RelayCommand]
    private async Task TrimVideoAsync()
    {
        if (!EnsureVideo(out var input)) return;
        var output = VideoOutput(input, "_trim", Path.GetExtension(input).TrimStart('.'));
        try
        {
            await VideoTools.TrimAsync(input, output, ParseSeconds(TrimStart), ParseSeconds(TrimEnd));
            VideoStatus = $"✓ 剪辑完成: {output}";
        }
        catch (Exception ex)
        {
            VideoStatus = $"剪辑失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExtractFramesAsync()
    {
        if (!EnsureVideo(out var input)) return;
        var outDir = Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "_frames");
        try
        {
            var frames = await VideoTools.ExtractFramesAsync(input, outDir, ParseDouble(ExtractFps, 1));
            VideoStatus = $"✓ 已抽取 {frames.Count} 帧到: {outDir}";
        }
        catch (Exception ex)
        {
            VideoStatus = $"抽帧失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ThumbnailAsync()
    {
        if (!EnsureVideo(out var input)) return;
        var output = VideoOutput(input, "_thumbs", "jpg");
        try
        {
            await VideoTools.MakeThumbnailAsync(input, output,
                ParseInt(ThumbCols, 3), ParseInt(ThumbRows, 3), ParseDouble(ThumbInterval, 10));
            VideoStatus = $"✓ 缩略图完成: {output}";
        }
        catch (Exception ex)
        {
            VideoStatus = $"缩略图失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GifAsync()
    {
        if (!EnsureVideo(out var input)) return;
        var output = VideoOutput(input, "_clip", "gif");
        try
        {
            await VideoTools.GifAsync(input, output,
                ParseInt(GifWidth, 320), ParseInt(GifFps, 10), ParseSeconds(GifStart), ParseSeconds(GifDuration));
            VideoStatus = $"✓ GIF 完成: {output}";
        }
        catch (Exception ex)
        {
            VideoStatus = $"GIF 失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AdjustAudioAsync()
    {
        if (!EnsureVideo(out var input)) return;
        var output = VideoOutput(input, "_enhanced", AudioOutputFormat);
        try
        {
            await VideoTools.AdjustVolumeAsync(input, output,
                ParseDouble(VolumeValue, 1), ParseSeconds(FadeInSeconds), ParseSeconds(FadeOutSeconds));
            VideoStatus = $"✓ 音频处理完成: {output}";
        }
        catch (Exception ex)
        {
            VideoStatus = $"音频处理失败: {ex.Message}";
        }
    }
}
