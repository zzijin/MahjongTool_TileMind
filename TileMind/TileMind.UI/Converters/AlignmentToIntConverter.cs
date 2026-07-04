using System.Globalization;
using System.Windows.Data;
using TileMind.Common.Models;

namespace TileMind.UI.Converters;

/// <summary>
/// OverlayTextAlignment ↔ int (0=Left, 1=Center, 2=Right)，用于 ComboBox.SelectedIndex 绑定。
/// </summary>
internal class AlignmentToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is OverlayTextAlignment alignment)
            return (int)alignment;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i >= 0 && i <= 2)
            return (OverlayTextAlignment)i;
        return OverlayTextAlignment.Left;
    }
}
