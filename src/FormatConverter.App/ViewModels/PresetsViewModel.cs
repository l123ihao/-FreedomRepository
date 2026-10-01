using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatConverter.App.Services;
using FormatConverter.App.Views;
using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;

namespace FormatConverter.App.ViewModels;

/// <summary>
/// 预设管理:合并内置与用户预设、选中态、增删改排序、持久化。
/// 注意:属性名 ActivePreset 避免与类型 ConversionPreset 相关命名冲突。
/// </summary>
public sealed partial class PresetsViewModel : ObservableObject
{
    public ObservableCollection<PresetItemViewModel> Items { get; } = new();

    /// <summary>当前选中的预设;null = 无预设(跟随全局设置)。</summary>
    [ObservableProperty]
    private ConversionPreset? activePreset;

    /// <summary>设置页预设列表中选中的行(编辑/删除/排序命令的目标)。</summary>
    [ObservableProperty]
    private PresetItemViewModel? selectedItem;

    /// <summary>当前预设的中文摘要(参数/模板/后置动作);空 = 跟随全局设置。</summary>
    public string ActiveSummary { get; private set; } = "";

    /// <summary>选中预设变化时通知(由 MainViewModel 订阅,联动磁贴与高级设置)。</summary>
    public event Action<ConversionPreset?>? ActivePresetChanged;

    private AppSettingsData _settings = SettingsService.Load();

    public PresetsViewModel() => Reload();

    /// <summary>从设置重新加载并合并预设;保持当前选中(按 Id 定位,找不到回「无预设」)。</summary>
    public void Reload()
    {
        _settings = SettingsService.Load();
        var effective = PresetMerger.Merge(
            BuiltInPresets.Defaults, _settings.UserPresets,
            _settings.PresetOrder, _settings.DeletedBuiltInPresetIds);

        var currentId = ActivePreset?.Id;
        Items.Clear();
        Items.Add(new PresetItemViewModel(null, Select));
        foreach (var p in effective)
            Items.Add(new PresetItemViewModel(p, Select));

        var keep = currentId is null ? null : effective.FirstOrDefault(p => p.Id == currentId);
        SetActive(keep);
        ActivePresetChanged?.Invoke(ActivePreset);
        UpdateSummary();
    }

    /// <summary>持久化当前用户预设/顺序/删除标记(并同步右键菜单预设子命令)。</summary>
    public void Save()
    {
        SettingsService.Save(_settings);
        ShellIntegration.SyncPresets(EffectivePresets());
    }

    private IReadOnlyList<ConversionPreset> EffectivePresets() => PresetMerger.Merge(
        BuiltInPresets.Defaults, _settings.UserPresets,
        _settings.PresetOrder, _settings.DeletedBuiltInPresetIds);

    private void Select(PresetItemViewModel item)
    {
        SetActive(item.Preset);
        UpdateSummary();
        ActivePresetChanged?.Invoke(ActivePreset);
    }

    private void SetActive(ConversionPreset? preset)
    {
        ActivePreset = preset; // 生成属性自带 PropertyChanged 通知
        foreach (var item in Items)
        {
            var selected = item.Preset is null
                ? preset is null
                : preset is not null && string.Equals(item.Preset.Id, preset.Id, StringComparison.OrdinalIgnoreCase);
            item.SetSelected(selected);
        }
    }

    private void UpdateSummary()
    {
        ActiveSummary = ActivePreset is null
            ? ""
            : Items.Skip(1).FirstOrDefault(i =>
                    i.Preset is not null
                    && string.Equals(i.Preset.Id, ActivePreset.Id, StringComparison.OrdinalIgnoreCase))
                ?.Summary ?? "";
        OnPropertyChanged(nameof(ActiveSummary));
    }

    private ConversionPreset? ShowEditor(ConversionPreset? existing)
    {
        var editor = new PresetEditorViewModel(existing, _settings);
        var dlg = new PresetManagerDialog
        {
            DataContext = editor,
            Owner = System.Windows.Application.Current.MainWindow,
        };
        return dlg.ShowDialog() == true && editor.TryBuild(out var preset, out _)
            ? preset
            : null;
    }

    [RelayCommand]
    private void NewPreset()
    {
        var result = ShowEditor(null);
        if (result is null) return;
        _settings.UserPresets.Add(result);
        _settings.PresetOrder.Add(result.Id);
        Save();
        SetActiveAfterReload(result.Id);
    }

    [RelayCommand]
    private void EditPreset(PresetItemViewModel? item)
    {
        if (item?.Preset is null || item.IsBuiltIn) return;
        var result = ShowEditor(item.Preset);
        if (result is null) return;
        var index = _settings.UserPresets.FindIndex(p => p.Id == item.Preset.Id);
        if (index >= 0) _settings.UserPresets[index] = result;
        Save();
        SetActiveAfterReload(result.Id);
    }

    [RelayCommand]
    private void DeletePreset(PresetItemViewModel? item)
    {
        if (item?.Preset is null || item.IsNone) return;
        var id = item.Preset.Id;
        if (item.IsBuiltIn)
        {
            if (!_settings.DeletedBuiltInPresetIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                _settings.DeletedBuiltInPresetIds.Add(id);
            _settings.PresetOrder.RemoveAll(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            _settings.UserPresets.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            _settings.PresetOrder.RemoveAll(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase));
        }
        Save();
        SetActiveAfterReload(null);
    }

    [RelayCommand]
    private void MoveUp(PresetItemViewModel? item) => Move(item, -1);

    [RelayCommand]
    private void MoveDown(PresetItemViewModel? item) => Move(item, +1);

    private void Move(PresetItemViewModel? item, int delta)
    {
        if (item?.Preset is null || item.IsNone) return;
        var order = EffectivePresets().Select(p => p.Id).ToList();
        var index = order.FindIndex(id =>
            string.Equals(id, item.Preset.Id, StringComparison.OrdinalIgnoreCase));
        var target = index + delta;
        if (index < 0 || target < 0 || target >= order.Count) return;
        (order[index], order[target]) = (order[target], order[index]);
        _settings.PresetOrder = order;
        Save();
        SetActiveAfterReload(item.Preset.Id);
    }

    [RelayCommand]
    private void ResetOrder()
    {
        _settings.PresetOrder.Clear();
        Save();
        SetActiveAfterReload(ActivePreset?.Id);
    }

    /// <summary>保存后重载列表并选中指定预设(Id 为 null → 「无预设」)。</summary>
    private void SetActiveAfterReload(string? presetId)
    {
        Reload();
        SetActive(presetId is null ? null : EffectivePresets().FirstOrDefault(p => p.Id == presetId));
        UpdateSummary();
        ActivePresetChanged?.Invoke(ActivePreset);
    }

    private sealed record ConversionPresetEditorResult(ConversionPreset Preset, string? Error);
}
