using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FormatConverter.App.ViewModels;
using FormatConverter.App.Views;
using FormatConverter.Core.Formats;

namespace FormatConverter.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _flashTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // ---------- 窗口级投放(空白处兜底 → 当前选中格式) ----------

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            await DropOntoFormat(_vm.SelectedFormat, paths, tile: null);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // ---------- 磁贴投放区 ----------

    private void OnTileDragEnter(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            (sender as FrameworkElement)?.DataContext is FormatTileViewModel tile)
            tile.IsDragOver = true;
    }

    private void OnTileDragLeave(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is FormatTileViewModel tile)
            tile.IsDragOver = false;
    }

    private void OnTileDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true; // 阻止窗口级处理,避免空白区兜底抢事件
    }

    private async void OnTileDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is not FormatTileViewModel tile) return;
        tile.IsDragOver = false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        var (_, rejected) = await DropOntoFormat(tile.Format, paths, tile);
        if (rejected > 0) FlashTile(tile);
    }

    /// <summary>共用的投放流程:确认(可勾「不再提醒」)→ 入队 → 立即转换。</summary>
    private async Task<(int Added, int Rejected)> DropOntoFormat(
        FormatInfo target, string[] paths, FormatTileViewModel? tile)
    {
        var autoStart = true;
        if (_vm.AskBeforeConvert)
        {
            var dlg = new ConfirmDropDialog(CountFiles(paths), target.Extension) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                if (dlg.DontAskAgain) _vm.AskBeforeConvert = false; // setter 自动持久化
            }
            else
            {
                autoStart = false; // 「取消」:文件留在队列,等「开始转换」
            }
        }
        return await _vm.AddPathsToFormatAsync(paths, target, autoStart);
    }

    /// <summary>确认窗文案用的文件数(目录递归展开计数,读不了按 1 个算)。</summary>
    private static int CountFiles(IEnumerable<string> paths)
    {
        var count = 0;
        foreach (var p in paths)
        {
            try
            {
                count += Directory.Exists(p)
                    ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Take(10000).Count()
                    : File.Exists(p) ? 1 : 0;
            }
            catch
            {
                count++;
            }
        }
        return Math.Max(count, 1);
    }

    // ---------- 拒绝红闪(共享定时器,500ms 后恢复) ----------

    private void FlashTile(FormatTileViewModel tile)
    {
        tile.IsRejected = true;
        _flashTimer.Stop();
        _flashTimer.Tick -= OnFlashTick;
        _flashTimer.Tick += OnFlashTick;
        _flashTimer.Start();
    }

    private void OnFlashTick(object? sender, EventArgs e)
    {
        _flashTimer.Stop();
        foreach (var t in _vm.FormatGroups.SelectMany(g => g.Tiles))
            t.IsRejected = false;
    }
}
