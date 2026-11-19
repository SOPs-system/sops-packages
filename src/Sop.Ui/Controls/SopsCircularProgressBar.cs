using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Circular Capsule Progress Bar (SopsCircularProgressBar).
    /// Displays circular progress using two concentric/radial capsule arcs (Completed and Remaining).
    /// Both ends of both arcs are rounded with circular end-caps.
    /// Supports configurable diameter, stroke thickness, angular gap, smooth animation, and center content.
    /// </summary>
    public class SopsCircularProgressBar : Control
    {
        private TextBlock? _percentageLabel;

        static SopsCircularProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SopsCircularProgressBar),
                new FrameworkPropertyMetadata(typeof(SopsCircularProgressBar)));
            FocusableProperty.OverrideMetadata(
                typeof(SopsCircularProgressBar),
                new FrameworkPropertyMetadata(false));
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceValue));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceMaximum));

        public static readonly DependencyProperty CompletedBrushProperty = DependencyProperty.Register(
            nameof(CompletedBrush), typeof(Brush), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RemainingBrushProperty = DependencyProperty.Register(
            nameof(RemainingBrush), typeof(Brush), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
            nameof(Diameter), typeof(double), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(48.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentGapProperty = DependencyProperty.Register(
            nameof(SegmentGap), typeof(double), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(
            nameof(ShowPercentage), typeof(bool), typeof(SopsCircularProgressBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public Brush? CompletedBrush
        {
            get => (Brush?)GetValue(CompletedBrushProperty);
            set => SetValue(CompletedBrushProperty, value);
        }

        public Brush? RemainingBrush
        {
            get => (Brush?)GetValue(RemainingBrushProperty);
            set => SetValue(RemainingBrushProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public double Diameter
        {
            get => (double)GetValue(DiameterProperty);
            set => SetValue(DiameterProperty, value);
        }

        public double SegmentGap
        {
            get => (double)GetValue(SegmentGapProperty);
            set => SetValue(SegmentGapProperty, value);
        }

        public bool ShowPercentage
        {
            get => (bool)GetValue(ShowPercentageProperty);
            set => SetValue(ShowPercentageProperty, value);
        }

        #endregion

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            if (baseValue is double val)
            {
                var bar = (SopsCircularProgressBar)d;
                if (val < 0.0) return 0.0;
                if (val > bar.Maximum) return bar.Maximum;
            }
            return baseValue;
        }

        private static object CoerceMaximum(DependencyObject d, object baseValue)
        {
            if (baseValue is double max)
            {
                if (max <= 0.0) return 1.0;
            }
            return baseValue;
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsCircularProgressBar bar)
            {
                bar.UpdatePercentageText();
                bar.InvalidateVisual();
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _percentageLabel = GetTemplateChild("PART_PercentageLabel") as TextBlock;
            UpdatePercentageText();
        }

        private void UpdatePercentageText()
        {
            if (_percentageLabel != null)
            {
                var max = Maximum <= 0.0 ? 100.0 : Maximum;
                var ratio = Math.Max(0.0, Math.Min(Value, max)) / max;
                var pct = (int)Math.Round(ratio * 100.0);
                _percentageLabel.Text = $"{pct}%";
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var d = Diameter > 0 ? Diameter : 48.0;
            return new Size(d, d);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var d = Math.Min(ActualWidth, ActualHeight);
            if (d <= 0) d = Diameter;
            var stroke = Math.Max(1.0, StrokeThickness);
            if (d <= stroke) return;

            var radius = (d - stroke) / 2.0;
            var center = new Point(d / 2.0, d / 2.0);

            var max = Maximum <= 0.0 ? 100.0 : Maximum;
            var val = Math.Max(0.0, Math.Min(Value, max));
            var ratio = val / max;

            var activeBrush = CompletedBrush ?? (TryFindResource("Brush.Primary") as Brush) ?? Foreground;
            var trackBrush = RemainingBrush ?? (TryFindResource("Brush.SurfaceSunken") as Brush) ?? Background;

            // Compute angular gap in degrees based on arc length = SegmentGap
            var circumference = 2.0 * Math.PI * radius;
            var gapDegrees = circumference > 0 ? (SegmentGap / circumference) * 360.0 : 4.0;
            gapDegrees = Math.Min(gapDegrees, 45.0); // Safety limit

            var completedPen = new Pen(activeBrush, stroke)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            var remainingPen = new Pen(trackBrush, stroke)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            // Edge State: 0% -> full remaining circle
            if (ratio <= 0.001)
            {
                // Full circle drawn as geometry or ellipse
                dc.DrawEllipse(null, remainingPen, center, radius, radius);
                return;
            }

            // Edge State: 100% -> full completed circle
            if (ratio >= 0.999)
            {
                dc.DrawEllipse(null, completedPen, center, radius, radius);
                return;
            }

            // Segmented Arcs with Gap and Rounded Ends
            // Start at top (-90 degrees)
            var totalAvailableDegrees = 360.0 - (gapDegrees * 2.0);
            var completedSweep = Math.Max(1.0, totalAvailableDegrees * ratio);
            var remainingSweep = Math.Max(1.0, totalAvailableDegrees * (1.0 - ratio));

            var startAngleComp = -90.0 + (gapDegrees / 2.0);
            var endAngleComp = startAngleComp + completedSweep;

            var startAngleRem = endAngleComp + gapDegrees;
            var endAngleRem = startAngleRem + remainingSweep;

            // Draw Completed Arc Segment
            DrawArc(dc, completedPen, center, radius, startAngleComp, completedSweep);

            // Draw Remaining Arc Segment
            DrawArc(dc, remainingPen, center, radius, startAngleRem, remainingSweep);
        }

        private static void DrawArc(DrawingContext dc, Pen pen, Point center, double radius, double startAngleDegrees, double sweepAngleDegrees)
        {
            if (sweepAngleDegrees <= 0.1) return;

            var startRad = startAngleDegrees * (Math.PI / 180.0);
            var endRad = (startAngleDegrees + sweepAngleDegrees) * (Math.PI / 180.0);

            var startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));

            var endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            var isLargeArc = sweepAngleDegrees > 180.0;

            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            var geometry = new PathGeometry(new[] { figure });
            dc.DrawGeometry(null, pen, geometry);
        }
    }
}
