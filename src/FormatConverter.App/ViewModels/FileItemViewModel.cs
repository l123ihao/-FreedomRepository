using CommunityToolkit.Mvvm.ComponentModel;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;

namespace FormatConverter.App.ViewModels;

/// <summary>文件列表中一行的视图模型。</summary>
public partial class FileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private FormatInfo targetFormat;

    [ObservableProperty]
    private string status = "等待";

    [ObservableProperty]
    private double? progress;

    [ObservableProperty]
    private string? speed;

    [ObservableProperty]
    private string? error;

    /// <summary>异步探测到的媒体信息(时长/分辨率/编码),展示在行内副标题。</summary>
    [ObservableProperty]
    private string? mediaInfoText;

    public string SourcePath { get; }
    public string Name { get; }
    public string SizeText { get; }
    public string CategoryText { get; }
    public string Emoji { get; }
    public FileCategory Category { get; }

    /// <summary>目标格式简称(大写扩展名),行内小标签展示。</summary>
    public string TargetText => TargetFormat.Extension.ToUpper();

    public FileItemViewModel(
        string sourcePath, string name, long sizeBytes, FileCategory category,
        FormatInfo target)
    {
        SourcePath = sourcePath;
        Name = name;
        SizeText = FormatSize(sizeBytes);
        Category = category;
        CategoryText = category switch
        {
            FileCategory.Video => "视频",
            FileCategory.Audio => "音频",
            FileCategory.Document => "文档",
            FileCategory.Image => "图片",
            _ => "",
        };
        Emoji = category switch
        {
            FileCategory.Video => "🎬",
            FileCategory.Audio => "🎵",
            FileCategory.Document => "📄",
            FileCategory.Image => "🖼",
            _ => "📁",
        };
        targetFormat = target;
    }

    public string ProgressText => Progress is null ? "" : $"{Progress:0}%";

    partial void OnProgressChanged(double? value) => OnPropertyChanged(nameof(ProgressText));

    partial void OnTargetFormatChanged(FormatInfo value) => OnPropertyChanged(nameof(TargetText));

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / 1073741824.0:0.00} GB",
        >= 1L << 20 => $"{bytes / 1048576.0:0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };
}
