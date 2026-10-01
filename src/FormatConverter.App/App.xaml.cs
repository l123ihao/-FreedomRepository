using System.Windows;
using FormatConverter.App.Services;
using Microsoft.Win32;

namespace FormatConverter.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 命令行静默转换:--convert <扩展名|预设名> / --preset <预设名|Id>(右键菜单调用,不显示主窗口)
        if (e.Args.Length > 0 && (e.Args[0].Equals("--convert", StringComparison.OrdinalIgnoreCase)
            || e.Args[0].Equals("--preset", StringComparison.OrdinalIgnoreCase)))
        {
            RunCommandLineAsync(e.Args);
            return;
        }

        ThemeService.Apply(SettingsService.LoadTheme());

        // 跟随系统主题变化(仅当用户选择「跟随系统」时)
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General &&
                ThemeService.Current == AppTheme.System)
            {
                Dispatcher.Invoke(() => ThemeService.Apply(AppTheme.System));
            }
        };

        new MainWindow().Show();
    }

    private async void RunCommandLineAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Shutdown(1);
            return;
        }

        try
        {
            var presetOnly = args[0].Equals("--preset", StringComparison.OrdinalIgnoreCase);
            var exitCode = await CommandLineConverter.RunAsync(args[1], args.Skip(2).ToArray(), presetOnly);
            Shutdown(exitCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("万能格式转换器: 转换失败 — " + ex.Message);
            Shutdown(1);
        }
    }
}
