using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Pure vector barcode preview component for retail labels, shelves, and blister pack packaging.
    /// Draws EAN-13, EAN-8, UPC-A, and Code 128 barcodes with optical scanning precision.
    /// </summary>
    public class BarcodeView : FrameworkElement
    {
        public const double UnitsPerMm = 96.0 / 25.4;

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(BarcodeView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WidthMmProperty = DependencyProperty.Register(
            nameof(WidthMm), typeof(double), typeof(BarcodeView),
            new FrameworkPropertyMetadata(38.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HeightMmProperty = DependencyProperty.Register(
            nameof(HeightMm), typeof(double), typeof(BarcodeView),
            new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public string? Value
        {
            get => (string?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double WidthMm
        {
            get => (double)GetValue(WidthMmProperty);
            set => SetValue(WidthMmProperty, value);
        }

        public double HeightMm
        {
            get => (double)GetValue(HeightMmProperty);
            set => SetValue(HeightMmProperty, value);
        }

        public BarcodeGeometry? Geometry() =>
            HeightMm <= 0 ? Barcode.Geometry(Value) : Barcode.Geometry(Value, HeightMm * (Barcode.Geometry(Value)?.Width ?? 100) / Math.Max(1.0, WidthMm));

        protected override Size MeasureOverride(Size availableSize)
        {
            var geometry = Geometry();
            if (geometry == null || WidthMm <= 0 || geometry.Width <= 0) return new Size(0, 0);
            var width = WidthMm * UnitsPerMm;
            return new Size(width, width / geometry.Width * geometry.Height);
        }

        protected override void OnRender(DrawingContext context)
        {
            var geometry = Geometry();
            if (geometry == null || WidthMm <= 0 || geometry.Width <= 0) return;
            var scale = WidthMm * UnitsPerMm / geometry.Width;

            // Optical scanning requirements require strict high-contrast black on white
            var paper = Brushes.White;
            var ink = Brushes.Black;
            context.DrawRectangle(paper, null, new Rect(0, 0, geometry.Width * scale, geometry.Height * scale));

            foreach (var bar in geometry.Bars)
            {
                context.DrawRectangle(ink, null, new Rect(bar.X * scale, 0, bar.Width * scale, bar.Height * scale));
            }

            if (geometry.FontSize <= 0) return;
            var typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            foreach (var digit in geometry.Texts)
            {
                var text = new FormattedText(digit.Value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, geometry.FontSize * scale, ink, 1.0);
                context.DrawText(text, new Point(digit.X * scale - text.Width / 2, geometry.TextBaseline * scale - text.Baseline));
            }
        }
    }
}
