using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormatConverter.App.Services;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Ffmpeg;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;
using FormatConverter.Core.Tools;

namespace FormatConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public const string PageConvert = "convert";
    public const string PageTools = "tools";
    public const string PageHistory = "history";
    public const string PageSettings = "settings";

    public sealed record ThemeOption(string Key, string Label);

    /// <summary>命令面板条目:显示名 + 快捷键提示 + 执行动作。</summary>
    public sealed record PaletteItem(string Label, string Hint, Action Run);

    private readonly ConversionEngine _engine = new(ConverterFactory.CreateDefault(), smartParallelism: true);
    private readonly HashSet<string> _knownPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProgress<ProgressInfo> _progress;
    private readonly SemaphoreSlim _probeGate = new(2);
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _probeCts = new();
    private int _overwriteWarnings;

    /// <summary>应用设置(构造时加载一次;设置类属性变更时回写)。注意:必须先于下方各设置属性声明。</summary>
    private readonly AppSettingsData _settings = SettingsService.Load();

    private System.Windows.Threading.DispatcherTimer? _exitTimer;
    private int _exitCountdown;

    public ObservableCollection<FileItemViewModel> Files { get; } = new();

    /// <summary>队列中当前选中的文件(ConvertPage 的 SelectionChanged 同步,Del 移除用)。</summary>
    public ObservableCollection<FileItemViewModel> SelectedFiles { get; } = new();

    public int[] AudioBitrateChoices { get; } = { 128, 192, 320 };

    /// <summary>设置页主题选项(Key 与 AppTheme 枚举名一致)。</summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption("System", "跟随系统"),
        new ThemeOption("Light", "浅色"),
        new ThemeOption("Dark", "深色"),
    };

    /// <summary>命令面板全部条目(构造函数填充)。</summary>
    public IReadOnlyList<PaletteItem> PaletteItems { get; }

    /// <summary>全量目标格式磁贴(按类别顺序平铺:视频→音频→文档→图片)。</summary>
    public IReadOnlyList<FormatTileViewModel> FormatTiles { get; }

    /// <summary>预设管理(转换页预设条 + 设置页预设卡片共用)。</summary>
    public PresetsViewModel PresetsVm { get; } = new();

    /// <summary>全局唯一的目标格式:先选格式,再拖文件。</summary>
    [ObservableProperty]
    private FormatInfo selectedFormat = null!;

    /// <summary>当前选中格式的简称(大写扩展名),空态拖放文案用。</summary>
    public string SelectedFormatText => SelectedFormat.Extension.ToUpper();

    /// <summary>当前页面 key(convert/tools/history/settings),驱动导航切换。</summary>
    [ObservableProperty]
    private string currentPageKey = PageConvert;

    public bool IsConvertPage => CurrentPageKey == PageConvert;
    public bool IsToolsPage => CurrentPageKey == PageTools;
    public bool IsHistoryPage => CurrentPageKey == PageHistory;
    public bool IsSettingsPage => CurrentPageKey == PageSettings;

    /// <summary>设置页主题选择;变更即时应用并持久化。</summary>
    [ObservableProperty]
    private ThemeOption selectedTheme = null!;

    [ObservableProperty]
    private string outputDirectory = ""; // 初值在构造函数内从设置读取(字段初始化器不触发变更回调)

    [ObservableProperty]
    private bool outputToSourceFolder;

    [ObservableProperty]
    private bool autoRename = true;

    [ObservableProperty]
    private int audioBitrateKbps = 192;

    [ObservableProperty]
    private bool videoCopyFirst = true;

    /// <summary>视频转码优先硬件编码(NVENC/QSV/AMF),失败自动回退软件。</summary>
    [ObservableProperty]
    private bool videoHardwareAcceleration = true;

    /// <summary>全部成功后自动退出程序(倒计时期间拖入新文件/重试会取消)。</summary>
    [ObservableProperty]
    private bool exitAfterConversions;

    /// <summary>自动退出倒计时秒数。</summary>
    [ObservableProperty]
    private int exitDelaySeconds = 5;

    public int[] ExitDelayChoices { get; } = { 3, 5, 10 };

    [ObservableProperty]
    private bool isConverting;

    [ObservableProperty]
    private double overallProgress;

    /// <summary>任务栏进度(0-1),绑定 TaskbarItemInfo.ProgressValue。</summary>
    [ObservableProperty]
    private double taskbarProgress;

    /// <summary>任务栏进度状态:转换中 Normal,否则 None(隐藏)。</summary>
    [ObservableProperty]
    private System.Windows.Shell.TaskbarItemProgressState taskbarState =
        System.Windows.Shell.TaskbarItemProgressState.None;

    [ObservableProperty]
    private string statusText = "把文件拖到左侧队列区或右侧格式磁贴;先选格式再点「选择文件」亦可。";

    [ObservableProperty]
    private string currentSpeed = "";

    /// <summary>拖入磁贴时是否先弹确认小窗(「不再提醒」持久化到 %APPDATA%)。</summary>
    [ObservableProperty]
    private bool askBeforeConvert;

    /// <summary>队列中有「等待」文件 → 显示「开始转换」按钮。</summary>
    [ObservableProperty]
    private bool hasWaitingFiles;

    /// <summary>队列中有「失败」文件 → 显示「重试失败项」按钮。</summary>
    [ObservableProperty]
    private bool hasFailedFiles;

    // ---------- 命令面板状态 ----------

    [ObservableProperty]
    private bool commandPaletteVisible;

    [ObservableProperty]
    private string commandPaletteQuery = "";

    [ObservableProperty]
    private PaletteItem? selectedPaletteItem;

    /// <summary>按输入过滤后的命令面板条目。</summary>
    public IEnumerable<PaletteItem> FilteredPaletteItems =>
        string.IsNullOrWhiteSpace(CommandPaletteQuery)
            ? PaletteItems
            : PaletteItems.Where(i =>
                i.Label.Contains(CommandPaletteQuery, StringComparison.OrdinalIgnoreCase)
                || i.Hint.Contains(CommandPaletteQuery, StringComparison.OrdinalIgnoreCase));

    public MainViewModel()
    {
        // 在 UI 线程创建 Progress<T>:回调自动切回 UI 线程
        _progress = new Progress<ProgressInfo>(OnProgress);
        FormatTiles = BuildFormatTiles();
        // 默认选中 MP4(最常用的视频目标格式)
        var mp4 = FormatTiles.First(t => t.Format.Extension == "mp4");
        selectedFormat = mp4.Format;
        mp4.SetSelected(true);
        // 从持久化设置载入(直接赋字段,不触发变更回调,避免构造期回写)
        outputDirectory = _settings.OutputDirectory;
        outputToSourceFolder = _settings.OutputToSourceFolder;
        autoRename = _settings.AutoRename;
        audioBitrateKbps = _settings.AudioBitrateKbps;
        videoCopyFirst = _settings.VideoCopyFirst;
        videoHardwareAcceleration = _settings.VideoHardwareAcceleration;
        exitAfterConversions = _settings.ExitAfterConversions;
        exitDelaySeconds = _settings.ExitDelaySeconds;
        askBeforeConvert = _settings.DontAskBeforeConvert;
        // 主题:按持久化偏好初始化(ThemeService 已在 App 启动时应用过一次,这里只同步选项;
        // 走属性 setter → 触发 OnSelectedThemeChanged → 应用主题并回写完整 schema/迁移旧设置)
        var themeKey = string.IsNullOrEmpty(_settings.Theme) ? "System" : _settings.Theme;
        SelectedTheme = ThemeOptions.First(o =>
            string.Equals(o.Key, themeKey, StringComparison.OrdinalIgnoreCase));
        PresetsVm.ActivePresetChanged += ApplyPreset;
        RefreshShellStatus();
        // 命令面板条目(直达各命令)
        PaletteItems = new[]
        {
            new PaletteItem("选择文件", "Ctrl+O", AddFiles),
            new PaletteItem("添加文件夹", "Ctrl+Shift+O", AddFolder),
            new PaletteItem("清空列表", "", Clear),
            new PaletteItem("转换页", "Ctrl+1", () => Navigate(PageConvert)),
            new PaletteItem("工具页", "Ctrl+2", () => Navigate(PageTools)),
            new PaletteItem("历史页", "Ctrl+3", () => Navigate(PageHistory)),
            new PaletteItem("设置页", "Ctrl+4", () => Navigate(PageSettings)),
            new PaletteItem("关于", "", About),
        };
    }

    /// <summary>按固定顺序(视频/音频/文档/图片)平铺全部目标格式磁贴;纯来源格式(如 pptx)不出磁贴。</summary>
    private IReadOnlyList<FormatTileViewModel> BuildFormatTiles()
    {
        var order = new[] { FileCategory.Video, FileCategory.Audio, FileCategory.Document, FileCategory.Image };
        return order.SelectMany(cat =>
                FormatRegistry.AllFormats.Where(f => f.Category == cat && FormatRegistry.IsTargetFormat(f.Extension)))
            .Select(f => new FormatTileViewModel(f, format => SelectedFormat = format))
            .ToList();
    }

    /// <summary>
    /// 选中预设 → 联动目标格式磁贴 + 把可映射的参数覆盖写入全局高级设置(随全局持久化)。
    /// CRF/GIF/自定义参数等无全局 UI 的覆盖在入队时按条目快照生效。
    /// </summary>
    private void ApplyPreset(ConversionPreset? preset)
    {
        if (preset is null) return;
        var format = FormatRegistry.Find(preset.TargetExtension);
        if (format is null) return;
        SelectedFormat = format;
        if (preset.Options is { } o)
        {
            if (o.AudioBitrateKbps is { } k) AudioBitrateKbps = k;
            if (o.VideoMode is { } vm) VideoCopyFirst = vm == VideoMode.CopyFirst;
            if (o.HardwareAcceleration is { } hw) VideoHardwareAcceleration = hw;
        }
        StatusText = $"已应用预设「{preset.Name}」:目标 {preset.TargetExtension.ToUpper()},后续入队的文件将使用该预设(可映射的参数已写入全局设置)。";
    }

    /// <summary>取对指定目标格式生效的当前预设(目标不同 → 纯显式目标,不套预设);无预设返回 null。</summary>
    private ConversionPreset? PresetFor(FormatInfo target)
    {
        var preset = PresetsVm.ActivePreset;
        if (preset is null) return null;
        return string.Equals(preset.TargetExtension, target.Extension, StringComparison.OrdinalIgnoreCase)
            ? preset
            : null;
    }

    /// <summary>跳转到设置页的预设管理卡片。</summary>
    [RelayCommand]
    private void OpenPresetManager() => Navigate(PageSettings);

    partial void OnSelectedFormatChanged(FormatInfo value)
    {
        // 单选联动:刷新所有磁贴的选中态
        foreach (var tile in FormatTiles)
            tile.SetSelected(tile.Format == value);
        // "等待"中的文件:源能转成新格式的跟随切换;不能转的保持原目标并给出红色「不兼容」提示
        foreach (var item in Files.Where(f => f.Status == "等待"))
        {
            var ext = Path.GetExtension(item.SourcePath).TrimStart('.');
            if (FormatRegistry.GetTargets(ext).Any(t =>
                    string.Equals(t.Extension, value.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                item.TargetFormat = value;
                item.ClearIncompatible();
            }
            else
            {
                item.SetIncompatible(value);
            }
        }
        OnPropertyChanged(nameof(SelectedFormatText));
        RefreshTileCounts();
    }

    /// <summary>侧边导航切页。</summary>
    [RelayCommand]
    private void Navigate(string page) => CurrentPageKey = page;

    partial void OnCurrentPageKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsConvertPage));
        OnPropertyChanged(nameof(IsToolsPage));
        OnPropertyChanged(nameof(IsHistoryPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }

    // ---------- 命令面板 ----------

    [RelayCommand]
    private void OpenCommandPalette()
    {
        CommandPaletteQuery = "";
        CommandPaletteVisible = true;
        SelectedPaletteItem = FilteredPaletteItems.FirstOrDefault();
    }

    [RelayCommand]
    private void CloseCommandPalette() => CommandPaletteVisible = false;

    [RelayCommand]
    private void ExecutePaletteItem(PaletteItem? item)
    {
        if (item is null) return;
        CommandPaletteVisible = false;
        item.Run();
    }

    public void SelectNextPaletteItem()
    {
        var items = FilteredPaletteItems.ToList();
        if (items.Count == 0) return;
        var idx = SelectedPaletteItem is null ? -1 : items.IndexOf(SelectedPaletteItem);
        SelectedPaletteItem = items[(idx + 1) % items.Count];
    }

    public void SelectPreviousPaletteItem()
    {
        var items = FilteredPaletteItems.ToList();
        if (items.Count == 0) return;
        var idx = SelectedPaletteItem is null ? 0 : items.IndexOf(SelectedPaletteItem);
        SelectedPaletteItem = items[(idx - 1 + items.Count) % items.Count];
    }

    partial void OnCommandPaletteQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredPaletteItems));
        SelectedPaletteItem = FilteredPaletteItems.FirstOrDefault();
    }

    // ---------- 移除选中文件(Del) ----------

    private bool CanRemoveSelectedFiles() => !IsConverting && SelectedFiles.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFiles))]
    private void RemoveSelectedFiles()
    {
        if (IsConverting) return;
        foreach (var item in SelectedFiles.ToList())
        {
            Files.Remove(item);
            _knownPaths.Remove(item.SourcePath);
        }
        SelectedFiles.Clear();
        RefreshTileCounts();
        RemoveSelectedFilesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>由 ConvertPage 的 SelectionChanged 调用,同步选中集合并刷新 Del 可用性。</summary>
    public void SyncSelectedFiles(IEnumerable<FileItemViewModel> selected)
    {
        SelectedFiles.Clear();
        foreach (var item in selected)
            SelectedFiles.Add(item);
        RemoveSelectedFilesCommand.NotifyCanExecuteChanged();
    }

    // ---------- 行内媒体信息探测 ----------

    /// <summary>入队后异步探测媒体信息(转换中不探测,避免与转换争用 ffprobe)。</summary>
    private void QueueMediaProbe(FileItemViewModel item)
    {
        var cts = _probeCts;
        if (cts is null) return;
        _ = ProbeMediaAsync(item, cts.Token);
    }

    private async Task ProbeMediaAsync(FileItemViewModel item, CancellationToken ct)
    {
        try
        {
            await _probeGate.WaitAsync(ct);
            try
            {
                var text = await MediaProbeService.ProbeTextAsync(item.SourcePath, item.Category, ct);
                if (text is not null)
                    item.MediaInfoText = text;
            }
            finally
            {
                _probeGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // 探测失败静默,不影响转换主流程
        }
    }

    /// <summary>主题选项变更:应用并持久化。</summary>
    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (value is null) return;
        var theme = Enum.Parse<AppTheme>(value.Key);
        ThemeService.Apply(theme);
        PersistSettings();
    }

    // ---------- 设置持久化 ----------

    /// <summary>把当前设置类属性回写到 AppSettingsData 并落盘。</summary>
    private void PersistSettings()
    {
        _settings.OutputDirectory = OutputDirectory;
        _settings.OutputToSourceFolder = OutputToSourceFolder;
        _settings.AutoRename = AutoRename;
        _settings.AudioBitrateKbps = AudioBitrateKbps;
        _settings.VideoCopyFirst = VideoCopyFirst;
        _settings.VideoHardwareAcceleration = VideoHardwareAcceleration;
        _settings.ExitAfterConversions = ExitAfterConversions;
        _settings.ExitDelaySeconds = ExitDelaySeconds;
        _settings.DontAskBeforeConvert = AskBeforeConvert;
        if (SelectedTheme is not null) _settings.Theme = SelectedTheme.Key;
        SettingsService.Save(_settings);
    }

    partial void OnOutputDirectoryChanged(string value) => PersistSettings();

    partial void OnOutputToSourceFolderChanged(bool value) => PersistSettings();

    partial void OnAutoRenameChanged(bool value) => PersistSettings();

    partial void OnAudioBitrateKbpsChanged(int value) => PersistSettings();

    partial void OnVideoCopyFirstChanged(bool value) => PersistSettings();

    partial void OnVideoHardwareAccelerationChanged(bool value) => PersistSettings();

    partial void OnExitAfterConversionsChanged(bool value) => PersistSettings();

    partial void OnExitDelaySecondsChanged(int value) => PersistSettings();

    // ---------- 完成后自动退出 ----------

    /// <summary>取消自动退出倒计时(拖入新文件/重试/重新开始转换时调用)。</summary>
    private void CancelExitCountdown()
    {
        _exitTimer?.Stop();
        _exitTimer = null;
    }

    /// <summary>全部成功后按设置启动退出倒计时;归零关闭程序。</summary>
    private void StartExitCountdown()
    {
        if (!ExitAfterConversions || ExitDelaySeconds <= 0) return;
        _exitCountdown = ExitDelaySeconds;
        CancelExitCountdown();
        _exitTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _exitTimer.Tick += (_, _) =>
        {
            _exitCountdown--;
            if (_exitCountdown <= 0)
            {
                CancelExitCountdown();
                Application.Current.Shutdown();
                return;
            }
            StatusText = $"全部完成,{_exitCountdown} 秒后自动退出(拖入新文件可取消)";
        };
        _exitTimer.Start();
        StatusText = $"全部完成,{_exitCountdown} 秒后自动退出(拖入新文件可取消)";
    }

    // ---------- 右键菜单集成 ----------

    [ObservableProperty]
    private string shellStatus = "未安装";

    [ObservableProperty]
    private bool isShellInstalled;

    [RelayCommand]
    private void InstallShell()
    {
        try
        {
            ShellIntegration.Install();
            ShellStatus = "已安装:右键任意文件 → 万能格式转换器。";
        }
        catch (Exception ex)
        {
            ShellStatus = $"安装失败: {ex.Message}";
        }
        RefreshShellStatus();
    }

    [RelayCommand]
    private void UninstallShell()
    {
        try
        {
            ShellIntegration.Uninstall();
            ShellStatus = "已卸载右键菜单。";
        }
        catch (Exception ex)
        {
            ShellStatus = $"卸载失败: {ex.Message}";
        }
        RefreshShellStatus();
    }

    private void RefreshShellStatus()
    {
        IsShellInstalled = ShellIntegration.IsInstalled;
        if (!IsShellInstalled && ShellStatus.StartsWith("已安装", StringComparison.Ordinal))
            ShellStatus = "未安装";
    }

    partial void OnAskBeforeConvertChanged(bool value) => PersistSettings();

    partial void OnIsConvertingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        RemoveSelectedFilesCommand.NotifyCanExecuteChanged();
        TaskbarState = value
            ? System.Windows.Shell.TaskbarItemProgressState.Normal
            : System.Windows.Shell.TaskbarItemProgressState.None;
    }

    partial void OnOverallProgressChanged(double value)
    {
        TaskbarProgress = value / 100.0;
    }

    // ---------- 进度 ----------

    private void OnProgress(ProgressInfo pi)
    {
        if (pi.FileIndex is int idx && idx >= 1 && idx <= Files.Count)
        {
            var item = Files[idx - 1];
            item.Status = "转换中";
            item.Progress = pi.Percent;
            item.Speed = pi.Speed;
            if (pi.FileName is not null && pi.FileCount > 0)
                StatusText = $"正在转换: {pi.FileName} ({idx}/{pi.FileCount})";
        }
        CurrentSpeed = pi.Speed is null ? "" : $"速度: {pi.Speed}";
        var completed = Files.Count(f => f.Status is "成功" or "失败" or "已取消");
        var current = pi.Percent ?? 0;
        OverallProgress = Math.Min(100, (completed + current / 100.0) / Math.Max(1, Files.Count) * 100.0);
        RefreshTileCounts();
    }

    // ---------- 转换 ----------

    private bool CanStartExecute() => !IsConverting && HasWaitingFiles;

    /// <summary>手动开始:把「等待」文件分批跑完(确认窗点了「取消」时的兜底)。</summary>
    [RelayCommand(CanExecute = nameof(CanStartExecute))]
    private async Task StartAsync() => await RunConversionLoopAsync();

    /// <summary>
    /// 转换主循环:只要有「等待」文件就继续跑,直到清空或被取消。
    /// 转换中拖入的新文件在下一批自动接上;取消后剩余文件标记为「已取消」,循环自然退出。
    /// </summary>
    private async Task RunConversionLoopAsync()
    {
        if (IsConverting) return;
        IsConverting = true;
        CurrentSpeed = "";
        CancelExitCountdown();
        // 转换期间暂停媒体探测,避免与转换争用 ffprobe
        _probeCts?.Cancel();
        _probeCts = null;
        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            while (true)
            {
                var pending = Files.Where(f => f.Status == "等待").ToList();
                if (pending.Count == 0) break;

                // 批次黑名单:同批次已分配的输出路径,防不同源同名文件互撞
                var batchBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var jobs = new List<ConversionJob>(pending.Count);
                var jobItems = new List<FileItemViewModel>(pending.Count);
                for (var i = 0; i < pending.Count; i++)
                {
                    var item = pending[i];
                    item.Status = "转换中";
                    item.Progress = 0;
                    item.Error = null;

                    string outputPath;
                    try
                    {
                        var ctx = new TemplateContext(
                            item.SourcePath, item.TargetFormat.Extension, i + 1, pending.Count, DateTime.Now);
                        outputPath = OutputPathResolver.Resolve(
                            item.SourcePath, item.TargetFormat.Extension, item.OutputTemplate,
                            OutputDirectory, OutputToSourceFolder, AutoRename, ctx, batchBlacklist);
                    }
                    catch (OutputTemplateException ex)
                    {
                        // 手改 settings.json 等异常模板:该文件直接失败,不阻塞批次
                        item.Status = "失败";
                        item.Error = ex.Message;
                        continue;
                    }
                    batchBlacklist.Add(outputPath);

                    var options = new ConversionOptions
                    {
                        AudioBitrateKbps = AudioBitrateKbps,
                        OverwritePolicy = AutoRename ? OverwritePolicy.Rename : OverwritePolicy.Overwrite,
                        VideoMode = VideoCopyFirst ? VideoMode.CopyFirst : VideoMode.AlwaysTranscode,
                        HardwareAcceleration = VideoHardwareAcceleration,
                    };
                    // 预设参数覆盖(CRF/GIF/自定义参数等无全局 UI 的项经此生效)
                    options = item.OptionsOverride?.ApplyTo(options) ?? options;
                    jobs.Add(new ConversionJob(Guid.NewGuid(), item.SourcePath, outputPath,
                        item.TargetFormat.Extension, options, item.PostAction, item.ArchiveFolder));
                    jobItems.Add(item);
                }

                var results = await _engine.ConvertAllAsync(jobs, _progress, cts.Token);

                for (var i = 0; i < jobItems.Count; i++)
                {
                    var item = jobItems[i];
                    var result = results[i];
                    if (result.Success)
                    {
                        item.Status = "成功";
                        item.Progress = 100;
                        item.Speed = null;
                        if (result.PostAction is { } pa)
                            item.PostActionNote = pa.Detail;
                        if (result.Note is { } note)
                            item.PostActionNote = string.IsNullOrEmpty(item.PostActionNote)
                                ? note
                                : item.PostActionNote + "; " + note;
                    }
                    else if (result.ErrorMessage == "已取消")
                    {
                        item.Status = "已取消";
                        item.Progress = null;
                    }
                    else
                    {
                        item.Status = "失败";
                        item.Progress = null;
                        item.Error = result.ErrorMessage;
                    }
                }
                RefreshTileCounts();
            }
        }
        catch (OperationCanceledException)
        {
            // 引擎已在内部把未完成的文件标记为"已取消"
        }
        finally
        {
            IsConverting = false;
            cts.Dispose();
            _cts = null;
            // 转换结束后恢复媒体探测
            _probeCts ??= new CancellationTokenSource();
        }

        RefreshTileCounts();
        var ok = Files.Count(f => f.Status == "成功");
        var fail = Files.Count(f => f.Status == "失败");
        var cancel = Files.Count(f => f.Status == "已取消");
        var wait = Files.Count(f => f.Status == "等待");
        StatusText = (ok > 0 ? $"{ok} 个成功" : "完成")
                     + (fail > 0 ? $",{fail} 个失败" : "")
                     + (cancel > 0 ? $",{cancel} 个已取消" : "")
                     + (wait > 0 ? $",{wait} 个待转换" : "");
        CurrentSpeed = "";
        OverallProgress = Files.Count > 0
            ? (double)(ok + fail + cancel) / Files.Count * 100
            : 0;

        // 完成通知(全取消时不打扰)
        if (ok + fail > 0)
            NotifyService.Show("转换完成", StatusText);

        // 全部成功且无剩余/失败/取消 → 按设置启动自动退出倒计时(拖入新文件会取消)
        if (ExitAfterConversions && ok > 0 && fail == 0 && cancel == 0 && wait == 0)
            StartExitCountdown();
    }

    private bool CanRetryFailed() => !IsConverting && HasFailedFiles;

    /// <summary>把「失败」文件重置为「等待」并重新跑。</summary>
    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private async Task RetryFailedAsync()
    {
        CancelExitCountdown();
        foreach (var item in Files.Where(f => f.Status == "失败").ToList())
        {
            item.Status = "等待";
            item.Error = null;
            item.Progress = null;
        }
        RefreshTileCounts();
        await RunConversionLoopAsync();
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    /// <summary>
    /// 刷新各磁贴角标计数(等待/转换中)与 HasWaitingFiles/HasFailedFiles。
    /// 在添加、移除、清空、进度回调、批次结束时调用。
    /// </summary>
    private void RefreshTileCounts()
    {
        foreach (var tile in FormatTiles)
        {
            tile.PendingCount = Files.Count(f =>
                f.Status is "等待" or "转换中" &&
                string.Equals(f.TargetFormat.Extension, tile.Format.Extension, StringComparison.OrdinalIgnoreCase));
        }
        HasWaitingFiles = Files.Any(f => f.Status == "等待");
        HasFailedFiles = Files.Any(f => f.Status == "失败");
        StartCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
    }

    // ---------- 文件列表 ----------

    [RelayCommand]
    private void AddFiles()
    {
        if (IsConverting) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = BuildFilter(),
        };
        if (dlg.ShowDialog() == true)
            AddPaths(dlg.FileNames);
    }

    [RelayCommand]
    private void AddFolder()
    {
        if (IsConverting) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (dlg.ShowDialog() == true)
            AddPaths(new[] { dlg.FolderName });
    }

    [RelayCommand]
    private void ChooseOutputDirectory()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = OutputDirectory };
        if (dlg.ShowDialog() == true)
            OutputDirectory = dlg.FolderName;
    }

    /// <summary>移除单个文件(列表行内 ✕ 按钮)。</summary>
    [RelayCommand]
    private void RemoveFile(FileItemViewModel? item)
    {
        if (IsConverting || item is null || !Files.Contains(item)) return;
        Files.Remove(item);
        _knownPaths.Remove(item.SourcePath);
        RefreshTileCounts();
    }

    [RelayCommand]
    private void Clear()
    {
        if (IsConverting) return;
        Files.Clear();
        _knownPaths.Clear();
        _overwriteWarnings = 0;
        OverallProgress = 0;
        StatusText = "把文件拖到左侧队列区或右侧格式磁贴;先选格式再点「选择文件」亦可。";
        CurrentSpeed = "";
        RefreshTileCounts();
    }

    [RelayCommand]
    private void About()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var ffmpeg = "";
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(FfmpegLocator.FfmpegPath);
            ffmpeg = "\n\nffmpeg 版本: " + fvi.ProductVersion;
        }
        catch
        {
            ffmpeg = "\n\n未检测到 ffmpeg(音视频转换不可用)";
        }
        MessageBox.Show(
            $"万能格式转换器 v{version}\n\n" +
            "支持视频 / 音频 / 文档 / 图片格式互转。\n" +
            "ffmpeg 采用 GPLv3 许可,详见随附的 THIRD-PARTY-LICENSES.txt。" + ffmpeg,
            "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------- 拖拽/添加 ----------

    public void AddPaths(IEnumerable<string> paths)
    {
        _overwriteWarnings = 0;
        foreach (var path in paths)
            AddPath(path);
        if (Files.Count > 0)
            StatusText = $"已添加 {Files.Count} 个文件。" + OverwriteWarning();
        RefreshTileCounts();
    }

    /// <summary>
    /// 拖到磁贴投放区:以指定格式为目标入队;不兼容的文件收集进 rejected。
    /// autoStart=true 时入队后立即开始转换(确认窗通过或勾过「不再提醒」)。
    /// 返回 (实际入队数, 被拒数);被拒数>0 时调用方让磁贴红闪提示。
    /// </summary>
    public async Task<(int Added, int Rejected)> AddPathsToFormatAsync(
        IEnumerable<string> paths, FormatInfo target, bool autoStart)
    {
        CancelExitCountdown();
        _overwriteWarnings = 0;
        var rejected = new List<string>();
        var before = Files.Count;
        foreach (var path in paths)
            AddPathToFormat(path, target, rejected);
        var added = Files.Count - before;
        RefreshTileCounts();

        if (added > 0)
        {
            var rejText = rejected.Count > 0
                ? $",忽略 {rejected.Count} 个: {string.Join("、", rejected.Take(3))}{(rejected.Count > 3 ? "…" : "")}"
                : "";
            StatusText = $"已添加 {added} 个文件(→ {target.Extension.ToUpper()}){rejText}" + OverwriteWarning();
            if (autoStart) await RunConversionLoopAsync();
        }
        else
        {
            StatusText = rejected.Count > 0
                ? $"无法添加:{string.Join("、", rejected.Take(3))}"
                : "没有可添加的文件。";
        }
        return (added, rejected.Count);
    }

    private void AddPath(string path)
    {
        CancelExitCountdown();
        if (Directory.Exists(path))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    TryAddFile(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"遍历文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (File.Exists(path))
        {
            TryAddFile(path);
        }
    }

    private void TryAddFile(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.');
        if (!FormatRegistry.IsSupported(ext)) return;
        if (!_knownPaths.Add(path)) return; // 去重
        try
        {
            var info = new FileInfo(path);
            var category = FormatRegistry.GetCategory(ext);
            var targets = FormatRegistry.GetTargets(ext);
            var defaultTarget = FormatRegistry.GetDefaultTarget(ext) ?? targets[0];
            // 能转成当前选中格式 → 用选中格式;否则用该文件类别的默认目标
            var target = targets.Any(t =>
                string.Equals(t.Extension, SelectedFormat.Extension, StringComparison.OrdinalIgnoreCase))
                ? SelectedFormat : defaultTarget;
            // 目标格式与当前预设一致 → 快照预设(模板/后置动作/参数覆盖)
            var item = new FileItemViewModel(
                path, info.Name, info.Length, category, target, PresetFor(target));
            if (!string.Equals(target.Extension, SelectedFormat.Extension, StringComparison.OrdinalIgnoreCase))
                item.SetIncompatible(SelectedFormat); // 不能转选中格式 → 落了默认目标,红色提示
            Files.Add(item);
            QueueMediaProbe(item);

            // 关闭自动重命名时预检:目标已存在将被覆盖,统计并在状态栏提示
            if (!AutoRename)
            {
                var output = OutputPathResolver.ResolveLegacy(
                    path, target.Extension, OutputDirectory, OutputToSourceFolder, AutoRename);
                if (File.Exists(output) &&
                    !string.Equals(Path.GetFullPath(output), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    _overwriteWarnings++;
            }
        }
        catch
        {
            _knownPaths.Remove(path);
        }
    }

    /// <summary>投放版入口:目录递归展开。</summary>
    private void AddPathToFormat(string path, FormatInfo target, List<string> rejected)
    {
        if (Directory.Exists(path))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    TryAddFileToFormat(file, target, rejected);
            }
            catch (Exception ex)
            {
                rejected.Add($"{Path.GetFileName(path)}(遍历失败: {ex.Message})");
            }
        }
        else if (File.Exists(path))
        {
            TryAddFileToFormat(path, target, rejected);
        }
    }

    /// <summary>强制目标格式入队;源不能转成该格式或不受支持 → 记录到 rejected。</summary>
    private void TryAddFileToFormat(string path, FormatInfo target, List<string> rejected)
    {
        var ext = Path.GetExtension(path).TrimStart('.');
        var name = Path.GetFileName(path);
        if (!FormatRegistry.IsSupported(ext))
        {
            rejected.Add($"{name}(不支持的格式)");
            return;
        }
        // 老版二进制 Office:doc 的全部目标与 ppt→txt/pdf 走内置路径(无 LibreOffice 也可);
        // 仅 ppt→pptx 依赖 LibreOffice
        if (ext.Equals("ppt", StringComparison.OrdinalIgnoreCase)
            && target.Extension.Equals("pptx", StringComparison.OrdinalIgnoreCase)
            && !LibreOfficeLocator.IsAvailable)
        {
            rejected.Add($"{name}(转换 PPT→PPTX 需要安装 LibreOffice;转为 TXT/PDF 无需安装)");
            return;
        }
        if (!_knownPaths.Add(path)) return; // 去重:静默跳过,不算拒绝
        try
        {
            var targets = FormatRegistry.GetTargets(ext);
            if (!targets.Any(t =>
                    string.Equals(t.Extension, target.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                _knownPaths.Remove(path);
                rejected.Add($"{name}(不能转换为 {target.Extension.ToUpper()})");
                return;
            }
            var info = new FileInfo(path);
            var item = new FileItemViewModel(
                path, info.Name, info.Length, FormatRegistry.GetCategory(ext), target, PresetFor(target));
            // 显式目标 ≠ 选中格式且不能转选中 → 红色提示(保持显式目标)
            if (!string.Equals(target.Extension, SelectedFormat.Extension, StringComparison.OrdinalIgnoreCase)
                && !targets.Any(t => string.Equals(t.Extension, SelectedFormat.Extension, StringComparison.OrdinalIgnoreCase)))
                item.SetIncompatible(SelectedFormat);
            Files.Add(item);
            QueueMediaProbe(item);

            if (!AutoRename)
            {
                var output = OutputPathResolver.ResolveLegacy(
                    path, target.Extension, OutputDirectory, OutputToSourceFolder, AutoRename);
                if (File.Exists(output) &&
                    !string.Equals(Path.GetFullPath(output), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    _overwriteWarnings++;
            }
        }
        catch
        {
            _knownPaths.Remove(path);
            rejected.Add($"{name}(读取失败)");
        }
    }

    private string OverwriteWarning() =>
        _overwriteWarnings > 0
            ? $" 注意:{_overwriteWarnings} 个文件的目标已存在,将直接覆盖(可在「高级设置」开启「重名自动加序号」避免覆盖)。"
            : "";

    private static string BuildFilter()
    {
        var all = string.Join(";", FormatRegistry.AllFormats.Select(f => "*." + f.Extension));
        return $"所有支持的文件|{all}|所有文件|*.*";
    }
}
