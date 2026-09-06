using System.Windows;
using System.Windows.Input;
using FormatConverter.App.ViewModels;

namespace FormatConverter.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    private MainViewModel Vm => _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        // 命令面板打开时聚焦输入框
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CommandPaletteVisible) && _vm.CommandPaletteVisible)
            {
                PaletteQueryBox.Focus();
                PaletteQueryBox.SelectAll();
            }
        };
    }

    // ---------- 命令面板 ----------

    private void OnPaletteKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Vm.CloseCommandPaletteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                Vm.ExecutePaletteItemCommand.Execute(Vm.SelectedPaletteItem);
                e.Handled = true;
                break;
            case Key.Down:
                Vm.SelectNextPaletteItem();
                PaletteList.ScrollIntoView(Vm.SelectedPaletteItem);
                e.Handled = true;
                break;
            case Key.Up:
                Vm.SelectPreviousPaletteItem();
                PaletteList.ScrollIntoView(Vm.SelectedPaletteItem);
                e.Handled = true;
                break;
        }
    }

    private void OnPaletteItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm.SelectedPaletteItem is not null)
            Vm.ExecutePaletteItemCommand.Execute(Vm.SelectedPaletteItem);
    }

    private void OnPaletteOverlayClick(object sender, MouseButtonEventArgs e) =>
        Vm.CloseCommandPaletteCommand.Execute(null);
}
