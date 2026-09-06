using System.Windows;
using Microsoft.Win32;

namespace FormatConverter.App.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// 主题切换:管理 Application 资源中的 Light/Dark 字典,支持跟随系统。
/// 所有颜色 brush 均通过 DynamicResource 引用,替换 MergedDictionaries 即可即时生效。
/// </summary>
public static class ThemeService
{
    private const string LightSource = "Themes/Light.xaml";
    private const string DarkSource = "Themes/Dark.xaml";

    public static AppTheme Current { get; private set; } = AppTheme.System;

    public static bool IsDark => SystemUsesDarkTheme();

    /// <summary>应用主题:替换主题字典(按 Source 识别),保留 App.xaml 内其余资源。</summary>
    public static void Apply(AppTheme theme)
    {
        Current = theme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var old = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString == LightSource || d.Source?.OriginalString == DarkSource);
        if (old is not null) dictionaries.Remove(old);

        var useDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemUsesDarkTheme(),
        };
        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri(useDark ? DarkSource : LightSource, UriKind.Relative),
        });
    }

    /// <summary>读取系统「应用模式」注册表项判断是否深色;读取失败按浅色处理。</summary>
    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }
}
