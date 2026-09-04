using System.Windows;

namespace FormatConverter.App.Views;

/// <summary>
/// 拖入磁贴后的确认小窗:「要把 N 个文件转换为 {EXT} 吗?」+「不再提醒」。
/// DialogResult=true 表示开始转换;「取消」时文件仍留在队列,可手动「开始转换」。
/// </summary>
public partial class ConfirmDropDialog : Window
{
    public bool DontAskAgain => DontAskCheck.IsChecked == true;

    public ConfirmDropDialog(int fileCount, string targetExtension)
    {
        InitializeComponent();
        MessageText.Text = $"要把 {fileCount} 个文件转换为 {targetExtension.ToUpper()} 吗?";
    }

    private void OnConvert(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
