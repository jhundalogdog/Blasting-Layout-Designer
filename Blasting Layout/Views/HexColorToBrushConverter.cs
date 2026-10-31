using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Blasting_Layout.Views;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string color || new BrushConverter().ConvertFromString(color) is not Brush brush)
        {
            throw new FormatException($"Invalid connector color: {value}");
        }

        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
