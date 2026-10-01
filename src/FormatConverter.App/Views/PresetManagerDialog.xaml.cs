using System.Windows;
using FormatConverter.App.ViewModels;

namespace FormatConverter.App.Views;

/// <summary>预设新建/编辑对话框:确定时校验,失败则错误红字展示在窗体上、不关闭。</summary>
public partial class PresetManagerDialog : Window
{
    public PresetManagerDialog() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is PresetEditorViewModel vm && !vm.TryBuild(out _, out _))
            return; // 校验失败,错误已显示在 StatusError
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
