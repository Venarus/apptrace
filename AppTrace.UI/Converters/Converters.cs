using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AppTrace.Core.Models;

namespace AppTrace.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true
        };

        bool invert = Invert || (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase));
        if (invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? parameter?.ToString() ?? "" : Binding.DoNothing;
    }
}

public class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConfidenceLevel confidence)
        {
            return confidence switch
            {
                ConfidenceLevel.High => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                ConfidenceLevel.Possible => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                ConfidenceLevel.Uncertain => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }
        return new SolidColorBrush(Color.FromRgb(148, 163, 184));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class SizeConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SizeConfidence sizeConf)
        {
            return sizeConf switch
            {
                SizeConfidence.Verified => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                SizeConfidence.Estimated => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                SizeConfidence.Unknown => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Slate
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
        }
        return new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StartupStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isEnabled = value is bool b && b;
        return isEnabled
            ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) // Emerald
            : new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Slate
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
