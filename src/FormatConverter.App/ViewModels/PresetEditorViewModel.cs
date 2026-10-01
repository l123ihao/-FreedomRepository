using CommunityToolkit.Mvvm.ComponentModel;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;
using FormatConverter.Core.Tools;

namespace FormatConverter.App.ViewModels;

public sealed record BitrateChoice(int? Value, string Label);
public sealed record CrfChoice(int? Value, string Label);
public sealed record VideoModeChoice(VideoMode? Value, string Label);
public sealed record HardwareChoice(bool? Value, string Label);
public sealed record ActionChoice(PostConversionAction Value, string Label);

/// <summary>预设编辑对话框 VM:三态选择(继承全局 = null),TryBuild 时经 PresetValidator 校验。</summary>
public sealed partial class PresetEditorViewModel : ObservableObject
{
    private readonly string _presetId;
    private readonly string? _selfId;
    private readonly IReadOnlyList<ConversionPreset> _existing;

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private FormatInfo? selectedTarget;

    [ObservableProperty]
    private string outputTemplate = "";

    [ObservableProperty]
    private string? templateError;

    [ObservableProperty]
    private string? statusError;

    [ObservableProperty]
    private ActionChoice selectedAction = null!;

    [ObservableProperty]
    private string archiveFolder = "";

    [ObservableProperty]
    private BitrateChoice selectedBitrate = null!;

    [ObservableProperty]
    private CrfChoice selectedCrf = null!;

    [ObservableProperty]
    private VideoModeChoice selectedVideoMode = null!;

    [ObservableProperty]
    private HardwareChoice selectedHardware = null!;

    [ObservableProperty]
    private string customFfmpegArgs = "";

    public IReadOnlyList<FormatInfo> TargetFormats { get; }

    public IReadOnlyList<BitrateChoice> BitrateChoices { get; } =
    [
        new(null, "继承全局设置"),
        new(128, "128 kbps"),
        new(192, "192 kbps"),
        new(320, "320 kbps"),
    ];

    public IReadOnlyList<CrfChoice> CrfChoices { get; } =
    [
        new(null, "继承全局设置"),
        new(18, "CRF 18(高清)"),
        new(20, "CRF 20"),
        new(23, "CRF 23(默认)"),
        new(26, "CRF 26"),
        new(28, "CRF 28"),
        new(32, "CRF 32(小体积)"),
    ];

    public IReadOnlyList<VideoModeChoice> VideoModeChoices { get; } =
    [
        new(null, "继承全局设置"),
        new(VideoMode.CopyFirst, "无损优先(兼容时秒转)"),
        new(VideoMode.AlwaysTranscode, "始终转码"),
    ];

    public IReadOnlyList<HardwareChoice> HardwareChoices { get; } =
    [
        new(null, "继承全局设置"),
        new(true, "启用硬件加速"),
        new(false, "禁用硬件加速"),
    ];

    public IReadOnlyList<ActionChoice> ActionChoices { get; } =
    [
        new(PostConversionAction.None, "不执行"),
        new(PostConversionAction.MoveToArchiveFolder, "移入归档文件夹"),
        new(PostConversionAction.DeleteSource, "删除原文件(仅成功后)"),
    ];

    /// <summary>归档文件夹输入仅在「移入归档文件夹」时可见。</summary>
    public bool ShowArchive => SelectedAction.Value == PostConversionAction.MoveToArchiveFolder;

    public string Title => _selfId is null ? "新建预设" : "编辑预设";

    public PresetEditorViewModel(ConversionPreset? existing, AppSettingsData settings)
    {
        TargetFormats = FormatRegistry.AllFormats
            .Where(f => FormatRegistry.IsTargetFormat(f.Extension)).ToList();
        var merged = PresetMerger.Merge(
            BuiltInPresets.Defaults, settings.UserPresets,
            settings.PresetOrder, settings.DeletedBuiltInPresetIds);
        _existing = existing is null
            ? merged
            : merged.Where(p => p.Id != existing.Id).ToList();
        _selfId = existing?.Id;
        _presetId = existing?.Id ?? Guid.NewGuid().ToString("N");

        selectedBitrate = BitrateChoices[0];
        selectedCrf = CrfChoices[0];
        selectedVideoMode = VideoModeChoices[0];
        selectedHardware = HardwareChoices[0];
        selectedAction = ActionChoices[0];
        selectedTarget = TargetFormats.FirstOrDefault(f => f.Extension == "mp4");

        if (existing is null) return;
        name = existing.Name;
        outputTemplate = existing.OutputFileNameTemplate ?? "";
        archiveFolder = existing.ArchiveFolder ?? "";
        selectedAction = ActionChoices.FirstOrDefault(c => c.Value == existing.PostConversionAction) ?? ActionChoices[0];
        selectedTarget = TargetFormats.FirstOrDefault(f =>
            string.Equals(f.Extension, existing.TargetExtension, StringComparison.OrdinalIgnoreCase))
            ?? selectedTarget;
        var o = existing.Options;
        if (o is null) return;
        selectedBitrate = BitrateChoices.FirstOrDefault(c => c.Value == o.AudioBitrateKbps) ?? BitrateChoices[0];
        selectedCrf = CrfChoices.FirstOrDefault(c => c.Value == o.VideoCrf) ?? CrfChoices[0];
        selectedVideoMode = VideoModeChoices.FirstOrDefault(c => c.Value == o.VideoMode) ?? VideoModeChoices[0];
        selectedHardware = HardwareChoices.FirstOrDefault(c => c.Value == o.HardwareAcceleration) ?? HardwareChoices[0];
        customFfmpegArgs = o.CustomFfmpegArgs ?? "";
    }

    /// <summary>构建预设并经校验;失败时错误写入 StatusError 并返回 false。</summary>
    public bool TryBuild(out ConversionPreset? preset, out string? error)
    {
        preset = null;
        var p = new ConversionPreset
        {
            Id = _presetId,
            Name = Name.Trim(),
            TargetExtension = SelectedTarget?.Extension ?? "",
            OutputFileNameTemplate = string.IsNullOrWhiteSpace(OutputTemplate) ? null : OutputTemplate.Trim(),
            PostConversionAction = SelectedAction.Value,
            ArchiveFolder = string.IsNullOrWhiteSpace(ArchiveFolder) ? null : ArchiveFolder.Trim(),
            Options = BuildOptions(),
            IsBuiltIn = false, // 内置预设不可编辑,编辑器只产出用户预设
        };
        error = PresetValidator.Validate(p, _existing, _selfId);
        if (error is not null)
        {
            StatusError = error;
            return false;
        }
        StatusError = null;
        preset = p;
        return true;
    }

    private ConversionOptionsOverride? BuildOptions()
    {
        var o = new ConversionOptionsOverride
        {
            AudioBitrateKbps = SelectedBitrate.Value,
            VideoCrf = SelectedCrf.Value,
            VideoMode = SelectedVideoMode.Value,
            HardwareAcceleration = SelectedHardware.Value,
            CustomFfmpegArgs = string.IsNullOrWhiteSpace(CustomFfmpegArgs) ? null : CustomFfmpegArgs.Trim(),
        };
        return o.IsEmpty ? null : o;
    }

    partial void OnOutputTemplateChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            TemplateError = null;
            return;
        }
        OutputTemplateEngine.TryValidate(value, out var error);
        TemplateError = error;
    }

    partial void OnSelectedActionChanged(ActionChoice value) => OnPropertyChanged(nameof(ShowArchive));
}
