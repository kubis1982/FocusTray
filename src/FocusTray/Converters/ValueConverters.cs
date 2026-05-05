using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FocusTray.Converters;

/// <summary>
/// Converts bool to Visibility (inverted: true = Collapsed, false = Visible).
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts string to Visibility (empty/null = Collapsed, non-empty = Visible).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to inverted bool.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Converts TimeOnly to DateTime for WPF DatePicker time binding.
/// </summary>
public class TimeOnlyToDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeOnly timeOnly)
        {
            return DateTime.Today.Add(timeOnly.ToTimeSpan());
        }
        return DateTime.Now;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return TimeOnly.FromDateTime(dateTime);
        }
        return TimeOnly.FromDateTime(DateTime.Now);
    }
}

/// <summary>
/// Converts TimeOnly to string (HH:mm format) for TextBox binding.
/// </summary>
public class TimeOnlyStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeOnly timeOnly)
        {
            return timeOnly.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            if (TimeOnly.TryParseExact(str, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                return timeOnly;
            }
            // Try parsing with single digit hours (e.g., "9:30")
            if (TimeOnly.TryParseExact(str, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnly))
            {
                return timeOnly;
            }
        }
        return TimeOnly.FromDateTime(DateTime.Now);
    }
}
