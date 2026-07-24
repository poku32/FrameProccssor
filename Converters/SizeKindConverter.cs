using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FrameProccssor.Models;

namespace FrameProccssor.Converters;

/// <summary>
/// SizeKind 枚举值转换为中文显示
/// </summary>
public class SizeKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SizeKind kind)
        {
            return kind switch
            {
                SizeKind.Fixed => "固定",
                SizeKind.Dependent => "依赖",
                SizeKind.Remainder => "剩余",
                SizeKind.TailFixed => "尾部固定",
                _ => "?"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return s switch
            {
                "固定" => SizeKind.Fixed,
                "依赖" => SizeKind.Dependent,
                "剩余" => SizeKind.Remainder,
                "尾部固定" => SizeKind.TailFixed,
                _ => SizeKind.Fixed
            };
        }
        return SizeKind.Fixed;
    }
}

/// <summary>
/// 布尔值取反
/// </summary>
public class BoolInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// null/空字符串 → Collapsed，非空 → Visible
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 选中索引的字节格子高亮转换器
/// </summary>
public class IndexHighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;

        int byteIndex = values[0] is int bi ? bi : -1;
        int? highlightIndex = values[1] as int?;
        return byteIndex == highlightIndex;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
