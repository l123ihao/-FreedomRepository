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

    /// <summary>转换后动作结果摘要(如「已移入归档」),成功回写后展示在行内。</summary>
    [ObservableProperty]
    private string? postActionNote;

    /// <summary>当前全局选中格式下该文件不可转换(仅「等待」态有意义)。</summary>
    [ObservableProperty]
    private bool isIncompatible;

    /// <summary>不可转换提示文案;空串 = 无提示(Run 绑定空串不渲染,免 Visibility 转换)。</summary>
    [ObservableProperty]
    private string incompatibleText = "";

    public string SourcePath { get; }
    public string Name { get; }
    public string SizeText { get; }
    public string CategoryText { get; }
    public string Emoji { get; }
    public FileCategory Category { get; }

    /// <summary>目标格式简称(大写扩展名),行内小标签展示。</summary>
    public string TargetText => TargetFormat.Extension.ToUpper();

    /// <summary>入队时快照的输出文件名模板(null = 跟随全局输出逻辑);后续预设修改不影响已入队条目。</summary>
    public string? OutputTemplate { get; }

    /// <summary>入队时快照的转换后动作(属性名 PostAction 避免与枚举类型 PostConversionAction 同名遮蔽)。</summary>
    public PostConversionAction PostAction { get; }

    /// <summary>入队时快照的归档文件夹。</summary>
    public string? ArchiveFolder { get; }

    /// <summary>行内预设小标签(如 " · 预设:高清 MP4"),无预设时为空。</summary>
    public string PresetLabelText { get; }

    /// <summary>入队时快照的预设参数覆盖(CRF/GIF/自定义参数等无全局 UI 的项经此生效);null = 纯全局设置。</summary>
    public ConversionOptionsOverride? OptionsOverride { get; }

    public FileItemViewModel(
        string sourcePath, string name, long sizeBytes, FileCategory category,
        FormatInfo target, ConversionPreset? preset = null)
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
        OutputTemplate = preset?.OutputFileNameTemplate;
        PostAction = preset?.PostConversionAction ?? PostConversionAction.None;
        ArchiveFolder = preset?.ArchiveFolder;
        OptionsOverride = preset?.Options;
        PresetLabelText = preset is null ? "" : $" · 预设:{preset.Name}";
    }

    public string ProgressText => Progress is null ? "" : $"{Progress:0}%";

    /// <summary>标记:不能转换为当前选中的格式(保持自身目标)。</summary>
    public void SetIncompatible(FormatInfo selected)
    {
        IsIncompatible = true;
        IncompatibleText = $"⚠ 不能转为 {selected.Extension.ToUpper()},将保持转为 {TargetText}";
    }

    public void ClearIncompatible()
    {
        IsIncompatible = false;
        IncompatibleText = "";
    }

    partial void OnProgressChanged(double? value) => OnPropertyChanged(nameof(ProgressText));

    partial void OnTargetFormatChanged(FormatInfo value)
    {
        OnPropertyChanged(nameof(TargetText));
        // 目标被切换(跟随选中格式)时,不兼容提示若存在则按新目标重写或清除
        if (IsIncompatible && IncompatibleText.Length > 0)
            IncompatibleText = IncompatibleText[..IncompatibleText.IndexOf("将保持", StringComparison.Ordinal)] + $"将保持转为 {TargetText}";
    }

    /// <summary>离开「等待」态(转换中/成功/失败/取消)即清除不兼容提示。</summary>
    partial void OnStatusChanged(string value)
    {
        if (value != "等待") ClearIncompatible();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / 1073741824.0:0.00} GB",
        >= 1L << 20 => $"{bytes / 1048576.0:0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };
}
