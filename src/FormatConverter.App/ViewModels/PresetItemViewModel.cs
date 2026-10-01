using CommunityToolkit.Mvvm.ComponentModel;
using FormatConverter.Core.Models;

namespace FormatConverter.App.ViewModels;

/// <summary>
/// 预设条上的一个 chip(或列表行)。首项为「无预设」伪条目(Preset = null)。
/// IsSelected 与 FormatTileViewModel 同款模式:点击向上推送,取消点击弹回。
/// </summary>
public sealed partial class PresetItemViewModel : ObservableObject
{
    private readonly Action<PresetItemViewModel> _select;
    private bool _isSelected;

    public ConversionPreset? Preset { get; }
    public string Name { get; }
    public string TargetText { get; }
    public string Summary { get; }
    public bool IsBuiltIn { get; }
    public bool IsNone { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value) _select(this);
            else OnPropertyChanged();
        }
    }

    /// <summary>由 PresetsViewModel 刷新选中态(必须显式传 nameof,同 FormatTileViewModel 的坑)。</summary>
    public void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));

    public PresetItemViewModel(ConversionPreset? preset, Action<PresetItemViewModel> select)
    {
        Preset = preset;
        _select = select;
        if (preset is null)
        {
            IsNone = true;
            Name = "无预设";
            TargetText = "";
            Summary = "";
            return;
        }
        Name = preset.Name;
        TargetText = preset.TargetExtension.ToUpper();
        IsBuiltIn = preset.IsBuiltIn;
        Summary = BuildSummary(preset);
    }

    private static string BuildSummary(ConversionPreset p)
    {
        var parts = new List<string>();
        if (p.Options is { IsEmpty: false } o) parts.Add(o.SummaryText);
        if (!string.IsNullOrWhiteSpace(p.OutputFileNameTemplate))
            parts.Add($"模板 {p.OutputFileNameTemplate}");
        parts.Add(p.PostConversionAction switch
        {
            PostConversionAction.MoveToArchiveFolder => "转换后归档",
            PostConversionAction.DeleteSource => "转换后删除原文件",
            _ => "无后置动作",
        });
        return string.Join(" · ", parts);
    }
}
