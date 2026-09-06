using System.Windows.Forms;

namespace FormatConverter.App.Services;

/// <summary>
/// 托盘气泡通知(不打扰式):转换完成时在通知区域显示 3 秒气泡。
/// 通知失败(如无托盘环境)静默忽略。
/// </summary>
public static class NotifyService
{
    private static NotifyIcon? _icon;

    public static void Show(string title, string message)
    {
        try
        {
            if (_icon is null)
            {
                _icon = new NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Information,
                    Visible = false,
                };
            }

            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(3000);
        }
        catch
        {
            // 通知失败不影响主流程
        }
    }
}
