using System.Globalization;
using System.Windows.Data;

namespace FormatConverter.App.Converters;

/// <summary>bool 取反(用于转换中禁用控件)。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
