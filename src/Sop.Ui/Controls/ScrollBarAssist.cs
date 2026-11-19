using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Attached properties to configure and dynamically resize ScrollBar and its arrow buttons.
    /// Supports inheritance on ScrollViewer so properties set on ScrollViewer apply to all nested scrollbars.
    /// </summary>
    public static class ScrollBarAssist
    {
        public static readonly DependencyProperty GutterSizeProperty =
            DependencyProperty.RegisterAttached(
                "GutterSize",
                typeof(double),
                typeof(ScrollBarAssist),
                new FrameworkPropertyMetadata(double.NaN,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                    OnGutterSizeChanged));

        public static double GetGutterSize(DependencyObject element) => (double)element.GetValue(GutterSizeProperty);
        public static void SetGutterSize(DependencyObject element, double value) => element.SetValue(GutterSizeProperty, value);

        public static readonly DependencyProperty ArrowSizeProperty =
            DependencyProperty.RegisterAttached(
                "ArrowSize",
                typeof(double),
                typeof(ScrollBarAssist),
                new FrameworkPropertyMetadata(double.NaN,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static double GetArrowSize(DependencyObject element) => (double)element.GetValue(ArrowSizeProperty);
        public static void SetArrowSize(DependencyObject element, double value) => element.SetValue(ArrowSizeProperty, value);

        public static readonly DependencyProperty ArrowGlyphSizeProperty =
            DependencyProperty.RegisterAttached(
                "ArrowGlyphSize",
                typeof(double),
                typeof(ScrollBarAssist),
                new FrameworkPropertyMetadata(double.NaN,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static double GetArrowGlyphSize(DependencyObject element) => (double)element.GetValue(ArrowGlyphSizeProperty);
        public static void SetArrowGlyphSize(DependencyObject element, double value) => element.SetValue(ArrowGlyphSizeProperty, value);

        public static readonly DependencyProperty ArrowStrokeThicknessProperty =
            DependencyProperty.RegisterAttached(
                "ArrowStrokeThickness",
                typeof(double),
                typeof(ScrollBarAssist),
                new FrameworkPropertyMetadata(double.NaN,
                    FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static double GetArrowStrokeThickness(DependencyObject element) => (double)element.GetValue(ArrowStrokeThicknessProperty);
        public static void SetArrowStrokeThickness(DependencyObject element, double value) => element.SetValue(ArrowStrokeThicknessProperty, value);

        private static void OnGutterSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is double size && !double.IsNaN(size) && size > 0)
            {
                if (d is ScrollBar scrollBar)
                {
                    ApplyGutterSize(scrollBar, size);
                }
                else if (d is ScrollViewer scrollViewer)
                {
                    if (scrollViewer.IsLoaded)
                    {
                        ApplyToScrollViewer(scrollViewer, size);
                    }
                    else
                    {
                        RoutedEventHandler? loadedHandler = null;
                        loadedHandler = (s, args) =>
                        {
                            scrollViewer.Loaded -= loadedHandler;
                            ApplyToScrollViewer(scrollViewer, (double)scrollViewer.GetValue(GutterSizeProperty));
                        };
                        scrollViewer.Loaded += loadedHandler;
                    }
                }
            }
        }

        private static void ApplyGutterSize(ScrollBar scrollBar, double size)
        {
            if (scrollBar.Orientation == Orientation.Vertical)
            {
                scrollBar.Width = size;
                scrollBar.MinWidth = size;
            }
            else
            {
                scrollBar.Height = size;
                scrollBar.MinHeight = size;
            }
        }

        private static void ApplyToScrollViewer(ScrollViewer scrollViewer, double size)
        {
            if (double.IsNaN(size) || size <= 0) return;
            if (scrollViewer.Template?.FindName("PART_VerticalScrollBar", scrollViewer) is ScrollBar vBar)
            {
                vBar.Width = size;
                vBar.MinWidth = size;
            }
            if (scrollViewer.Template?.FindName("PART_HorizontalScrollBar", scrollViewer) is ScrollBar hBar)
            {
                hBar.Height = size;
                hBar.MinHeight = size;
            }
        }
    }

    /// <summary>
    /// Alias for <see cref="ScrollBarAssist"/> providing developer convenience.
    /// </summary>
    public static class ScrollBarProperties
    {
        public static readonly DependencyProperty GutterSizeProperty = ScrollBarAssist.GutterSizeProperty;
        public static double GetGutterSize(DependencyObject element) => ScrollBarAssist.GetGutterSize(element);
        public static void SetGutterSize(DependencyObject element, double value) => ScrollBarAssist.SetGutterSize(element, value);

        public static readonly DependencyProperty ArrowSizeProperty = ScrollBarAssist.ArrowSizeProperty;
        public static double GetArrowSize(DependencyObject element) => ScrollBarAssist.GetArrowSize(element);
        public static void SetArrowSize(DependencyObject element, double value) => ScrollBarAssist.SetArrowSize(element, value);

        public static readonly DependencyProperty ArrowGlyphSizeProperty = ScrollBarAssist.ArrowGlyphSizeProperty;
        public static double GetArrowGlyphSize(DependencyObject element) => ScrollBarAssist.GetArrowGlyphSize(element);
        public static void SetArrowGlyphSize(DependencyObject element, double value) => ScrollBarAssist.SetArrowGlyphSize(element, value);

        public static readonly DependencyProperty ArrowStrokeThicknessProperty = ScrollBarAssist.ArrowStrokeThicknessProperty;
        public static double GetArrowStrokeThickness(DependencyObject element) => ScrollBarAssist.GetArrowStrokeThickness(element);
        public static void SetArrowStrokeThickness(DependencyObject element, double value) => ScrollBarAssist.SetArrowStrokeThickness(element, value);
    }

    /// <summary>
    /// Computes the arrow button length along the scrolling direction.
    /// </summary>
    public class ScrollBarArrowLengthConverter : IMultiValueConverter
    {
        public static readonly ScrollBarArrowLengthConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is double arrowSize && !double.IsNaN(arrowSize) && arrowSize > 0)
            {
                return arrowSize;
            }
            if (values != null && values.Length > 1 && values[1] is double actualDim && !double.IsNaN(actualDim) && actualDim > 0)
            {
                return actualDim;
            }
            if (Application.Current?.TryFindResource("Size.ScrollbarArrow") is double token && token > 0)
            {
                return token;
            }
            return 12.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Computes the size of the chevron arrow glyph.
    /// </summary>
    public class ScrollBarArrowGlyphSizeConverter : IMultiValueConverter
    {
        public static readonly ScrollBarArrowGlyphSizeConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is double glyphSize && !double.IsNaN(glyphSize) && glyphSize > 0)
            {
                return glyphSize;
            }
            if (values != null && values.Length > 1 && values[1] is double btnDim && !double.IsNaN(btnDim) && btnDim > 0)
            {
                return Math.Max(5.0, Math.Round(btnDim * 0.5));
            }
            if (Application.Current?.TryFindResource("Size.ScrollbarArrowGlyph") is double token && token > 0)
            {
                return token;
            }
            return 7.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Computes the stroke thickness of the chevron arrow path based on button size or explicit setting.
    /// </summary>
    public class ScrollBarArrowStrokeConverter : IMultiValueConverter
    {
        public static readonly ScrollBarArrowStrokeConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is double stroke && !double.IsNaN(stroke) && stroke > 0)
            {
                return stroke;
            }
            if (values != null && values.Length > 1 && values[1] is double dim && !double.IsNaN(dim) && dim >= 20.0)
            {
                return 2.0;
            }
            return 1.5;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
