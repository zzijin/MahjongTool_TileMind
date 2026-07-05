using System.Globalization;
using System.Windows.Data;

namespace TileMind.UI.Converters;

/// <summary>
/// 通用 enum ↔ int 转换器，用于 ComboBox.SelectedIndex 绑定。
/// </summary>
internal class AlignmentToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Enum e ? System.Convert.ToInt32(e) : 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && targetType.IsEnum)
            return Enum.ToObject(targetType, i);
        return 0;
    }
}
