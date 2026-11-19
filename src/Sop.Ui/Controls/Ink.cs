using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Centring text by its INK rather than by its line box.
    ///
    /// Centring a TextBlock centres its LINE, and a line is taller than the
    /// letters on it: it keeps room under the baseline for descenders, and
    /// where the family is a fallback list — as the mono token is — enough for
    /// the tallest face in the list, whether or not that face draws anything.
    /// A licence code or a folder path has no descenders and comes from one
    /// face, so the text you can see still sits off centre in the box around
    /// it. Measured on the database-folder chip: fourteen above, eleven below.
    ///
    /// <see cref="Centered"/> measures what is actually drawn — the bounds of
    /// the glyph geometry, not the font's declared metrics, which do not
    /// predict this — and shifts the text by the difference between the middle
    /// of the line and the middle of the ink. The margin gives back below
    /// exactly what it takes above, so the element keeps its size and the
    /// chip around it does not change shape.
    ///
    /// Use it with <c>VerticalAlignment="Center"</c>: a centred element with
    /// margins is placed by half their difference, which is precisely the
    /// shift this asks for.
    /// </summary>
    public static class Ink
    {
        public static readonly DependencyProperty CenteredProperty = DependencyProperty.RegisterAttached(
            "Centered", typeof(bool), typeof(Ink),
            new FrameworkPropertyMetadata(false, OnCenteredChanged));

        public static bool GetCentered(DependencyObject element) => (bool)element.GetValue(CenteredProperty);

        public static void SetCentered(DependencyObject element, bool value) => element.SetValue(CenteredProperty, value);

        private static void OnCenteredChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock text) return;
            if (e.NewValue is bool on && on)
            {
                text.Loaded += OnLoaded;
                text.SizeChanged += OnResized;
                Apply(text);
                return;
            }
            text.Loaded -= OnLoaded;
            text.SizeChanged -= OnResized;
            text.Margin = new Thickness(0);
        }

        // The face and the size are DynamicResource lookups, so they are often
        // still the fallback when the property is first set and the real ones
        // by the time the element loads. The text itself arrives later again —
        // a path is written in once the store is open.
        private static void OnLoaded(object sender, RoutedEventArgs e) => Apply((TextBlock)sender);

        private static void OnResized(object sender, SizeChangedEventArgs e) => Apply((TextBlock)sender);

        /// <summary>
        /// How far down this text has to move to sit in the middle of its
        /// line, in device-independent units. Negative means the ink sits low,
        /// which is the usual case for capitals with no descenders. Zero when
        /// there is nothing to measure.
        /// </summary>
        public static double Offset(TextBlock text)
        {
            if (text == null || string.IsNullOrEmpty(text.Text) || text.FontSize <= 0) return 0;
            var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
            var formatted = new FormattedText(
                text.Text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                text.FontSize,
                // Never painted — the constructor wants a brush and this
                // measures. The text's own keeps a colour literal out of it.
                text.Foreground,
                VisualTreeHelper.GetDpi(text).PixelsPerDip);

            // The drawn shape of this exact string, placed the way the line
            // lays it out: its top is where the tallest letter starts.
            var ink = formatted.BuildGeometry(new Point(0, 0))?.Bounds ?? Rect.Empty;
            if (ink.IsEmpty || formatted.Height <= 0) return 0;
            return formatted.Height / 2 - (ink.Top + ink.Height / 2);
        }

        private static void Apply(TextBlock text)
        {
            var shift = Offset(text);
            var margin = new Thickness(0, shift, 0, -shift);
            // SizeChanged fires from the layout this very margin causes.
            if (text.Margin != margin) text.Margin = margin;
        }
    }
}
