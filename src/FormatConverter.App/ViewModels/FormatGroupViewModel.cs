using CommunityToolkit.Mvvm.ComponentModel;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;

namespace FormatConverter.App.ViewModels;

/// <summary>格式选择区的一行:类别标签 + 该类全部格式磁贴。</summary>
public sealed class FormatGroupViewModel
{
    public string Label { get; }
    public IReadOnlyList<FormatTileViewModel> Tiles { get; }

    public FormatGroupViewModel(FileCategory category, IEnumerable<FormatInfo> formats, Action<FormatInfo> select)
    {
        Label = category switch
        {
            FileCategory.Video => "视频",
            FileCategory.Audio => "音频",
            FileCategory.Document => "文档",
            FileCategory.Image => "图片",
            _ => "",
        };
        Tiles = formats.Select(f => new FormatTileViewModel(f, select)).ToList();
    }
}

/// <summary>
/// 单个格式磁贴:IsSelected 由外部(全局选中格式)驱动;
/// 点击时向上推送选择,取消点击会弹回(单选,不可取消最后一个)。
/// 每个磁贴同时是该格式的投放区:拖入 → 以该格式为目标入队。
/// </summary>
public sealed partial class FormatTileViewModel : ObservableObject
{
    private readonly Action<FormatInfo> _select;
    private bool _isSelected;

    public FormatInfo Format { get; }
    public string ExtensionText { get; }
    public string Description { get; }

    /// <summary>该格式下 等待/转换中 的文件数(红色角标,0 隐藏)。</summary>
    [ObservableProperty]
    private int pendingCount;

    /// <summary>拖动悬停中:蓝边浅蓝底提示。</summary>
    [ObservableProperty]
    private bool isDragOver;

    /// <summary>刚拒绝了不兼容的拖入:红边浅红底闪烁。</summary>
    [ObservableProperty]
    private bool isRejected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value) _select(Format);   // 点击磁贴 → 通知主 VM 切换全局目标格式
            else OnPropertyChanged();     // 取消选中 → 通知属性变化,绑定弹回 true(保持单选)
        }
    }

    /// <summary>由主 VM 在全局选中格式变化后调用,刷新选中态。
    /// 注意:必须显式传 nameof(IsSelected)——[CallerMemberName] 会取成 "SetSelected",绑定收不到通知。</summary>
    public void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));

    public FormatTileViewModel(FormatInfo format, Action<FormatInfo> select)
    {
        Format = format;
        ExtensionText = format.Extension.ToUpper();
        // 说明小字:去掉展示名前缀的扩展名("MP4 视频"→"视频");无前缀则原样
        Description = format.DisplayName.StartsWith(ExtensionText + " ", StringComparison.OrdinalIgnoreCase)
            ? format.DisplayName[(ExtensionText.Length + 1)..]
            : format.DisplayName;
        _select = select;
    }
}
