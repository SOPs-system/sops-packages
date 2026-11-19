using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sop.Ui.Controls
{
    /// <summary>text-transform: uppercase — WPF has no CSS, so header text goes through this.</summary>
    public sealed class UpperCaseConverter : IValueConverter
    {
        public static readonly UpperCaseConverter Instance = new UpperCaseConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string s ? s.ToUpper(culture) : value;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public static readonly InverseBoolConverter Instance = new InverseBoolConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            !(value is bool b && b);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            !(value is bool b && b);
    }

    /// <summary>null / empty string → Collapsed; anything else → Visible.</summary>
    public sealed class PresentToVisibilityConverter : IValueConverter
    {
        public static readonly PresentToVisibilityConverter Instance = new PresentToVisibilityConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value == null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>null / empty string → Visible; anything else → Collapsed.</summary>
    public sealed class EmptyToVisibilityConverter : IValueConverter
    {
        public static readonly EmptyToVisibilityConverter Instance = new EmptyToVisibilityConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value == null || (value is string s && s.Length == 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Computes a circular pill corner radius (radius = height / 2).
    /// In WPF, setting CornerRadius to 9999 scales rx and ry separately to width/2 and height/2,
    /// which turns rectangles into ellipses. Binding CornerRadius to ActualHeight / 2 guarantees
    /// true circular caps (rx == ry == height/2).
    /// </summary>
    public sealed class HalfHeightToCornerRadiusConverter : IValueConverter
    {
        public static readonly HalfHeightToCornerRadiusConverter Instance = new HalfHeightToCornerRadiusConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double h && h > 0 && !double.IsNaN(h) && !double.IsInfinity(h))
            {
                return new CornerRadius(h / 2.0);
            }
            return new CornerRadius(11);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

