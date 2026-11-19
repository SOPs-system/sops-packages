using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Material 3–style Circular Progress Spinner (SopsCircularProgress).
    /// Both active arc and remaining track arc are drawn as precise geometric capsule paths (PathGeometry).
    /// Features:
    /// - Two distinct visible angular gaps between the active arc and track.
    /// - Caps on all 4 arc ends are explicitly rounded (semi-circular capsule geometry).
    /// - Cap thickness does not bleed into the gap: the gap is measured between the outer tips of the rounded caps.
    /// - Track never extends underneath the active arc or fills the gaps.
    /// - Smooth continuous rotation in indeterminate mode.
    /// </summary>
    public class SopsCircularProgress : Control
    {
        private readonly RotateTransform _rotation = new RotateTransform();
        private TextBlock? _percentageLabel;

        static SopsCircularProgress()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SopsCircularProgress),
                new FrameworkPropertyMetadata(typeof(SopsCircularProgress)));
            FocusableProperty.OverrideMetadata(
                typeof(SopsCircularProgress),
                new FrameworkPropertyMetadata(false));
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceValue));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceMaximum));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackGapAngleProperty = DependencyProperty.Register(
            nameof(TrackGapAngle), typeof(double), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(
            nameof(ActiveBrush), typeof(Brush), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
            nameof(TrackBrush), typeof(Brush), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
            nameof(IsIndeterminate), typeof(bool), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIndeterminateChanged));

        public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
            nameof(IsAnimated), typeof(bool), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(
            nameof(AnimationDuration), typeof(Duration), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(1200)), OnIndeterminateChanged));

        public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(
            nameof(ShowPercentage), typeof(bool), typeof(SopsCircularProgress),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region CLR Properties

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

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public double TrackGapAngle
        {
            get => (double)GetValue(TrackGapAngleProperty);
            set => SetValue(TrackGapAngleProperty, value);
        }

        public Brush? ActiveBrush
        {
            get => (Brush?)GetValue(ActiveBrushProperty);
            set => SetValue(ActiveBrushProperty, value);
        }

        public Brush? TrackBrush
        {
            get => (Brush?)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }

        public bool IsAnimated
        {
            get => (bool)GetValue(IsAnimatedProperty);
            set => SetValue(IsAnimatedProperty, value);
        }

        public Duration AnimationDuration
        {
            get => (Duration)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        public bool ShowPercentage
        {
            get => (bool)GetValue(ShowPercentageProperty);
            set => SetValue(ShowPercentageProperty, value);
        }

        #endregion

        public SopsCircularProgress()
        {
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = _rotation;

            IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is bool visible)
                {
                    UpdateAnimation(visible && IsIndeterminate);
                }
            };
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            if (baseValue is double val && d is SopsCircularProgress p)
            {
                if (val < 0.0) return 0.0;
                if (val > p.Maximum) return p.Maximum;
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
            if (d is SopsCircularProgress bar)
            {
                bar.UpdatePercentage();
                bar.InvalidateVisual();
            }
        }

        private static void OnIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsCircularProgress bar)
            {
                bar.UpdateAnimation(bar.IsVisible && bar.IsIndeterminate);
                bar.InvalidateVisual();
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _percentageLabel = GetTemplateChild("PART_PercentageLabel") as TextBlock;
            UpdatePercentage();
            UpdateAnimation(IsVisible && IsIndeterminate);
        }

        private void UpdatePercentage()
        {
            if (_percentageLabel != null)
            {
                if (IsIndeterminate)
                {
                    _percentageLabel.Text = string.Empty;
                    return;
                }
                var max = Maximum <= 0.0 ? 100.0 : Maximum;
                var ratio = Math.Max(0.0, Math.Min(Value, max)) / max;
                var pct = (int)Math.Round(ratio * 100.0);
                _percentageLabel.Text = $"{pct}%";
            }
        }

        private void UpdateAnimation(bool active)
        {
            if (!active || !Motion.Enabled || !IsAnimated)
            {
                _rotation.BeginAnimation(RotateTransform.AngleProperty, null);
                return;
            }

            var duration = AnimationDuration.HasTimeSpan
                ? AnimationDuration.TimeSpan
                : TimeSpan.FromMilliseconds(1200);

            var turn = new DoubleAnimation(0, 360, duration)
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _rotation.BeginAnimation(RotateTransform.AngleProperty, turn);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var s = Size > 0 ? Size : 40.0;
            return new Size(s, s);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var s = Math.Min(ActualWidth, ActualHeight);
            if (s <= 0) s = Size > 0 ? Size : 40.0;
            var stroke = StrokeThickness > 0 ? StrokeThickness : 4.0;
            if (s <= stroke) return;

            var rMid = (s - stroke) / 2.0;
            var rInner = rMid - (stroke / 2.0);
            var rOuter = rMid + (stroke / 2.0);
            var center = new Point(s / 2.0, s / 2.0);

            var activeBrush = ActiveBrush ?? (TryFindResource("Brush.Primary") as Brush) ?? Foreground;
            var trackBrush = TrackBrush ?? (TryFindResource("Brush.SurfaceSunken") as Brush) ?? Background;

            // Compute cap angle offset so that the rounded semi-circle cap does not bleed into the visual gap.
            // Half stroke on a circle of radius rMid occupies capAngle degrees:
            var capAngle = rMid > 0 ? ((stroke / 2.0) / rMid) * (180.0 / Math.PI) : 4.0;
            var requestedGap = Math.Max(4.0, TrackGapAngle);

            // Total angular distance from center-line to center-line between segments
            var centerGapAngle = requestedGap + (2.0 * capAngle);

            // Indeterminate Mode: Material 3 style spinning arc (260° active arc) separated by two large, distinct visible gaps to track
            if (IsIndeterminate)
            {
                var activeSweep = 250.0;
                var remainingForTrack = 360.0 - activeSweep - (centerGapAngle * 2.0);

                // Draw active capsule geometry
                var activeGeom = CreateCapsuleArcGeometry(center, rInner, rOuter, -90.0, activeSweep);
                if (activeGeom != null)
                {
                    dc.DrawGeometry(activeBrush, null, activeGeom);
                }

                // Draw track capsule geometry if space permits
                if (remainingForTrack > 4.0)
                {
                    var trackStart = -90.0 + activeSweep + centerGapAngle;
                    var trackGeom = CreateCapsuleArcGeometry(center, rInner, rOuter, trackStart, remainingForTrack);
                    if (trackGeom != null)
                    {
                        dc.DrawGeometry(trackBrush, null, trackGeom);
                    }
                }
                return;
            }

            // Determinate Mode:
            var max = Maximum <= 0.0 ? 100.0 : Maximum;
            var val = Math.Max(0.0, Math.Min(Value, max));
            var ratio = val / max;

            // 0%: Full track donut (complete circle)
            if (ratio <= 0.001)
            {
                var fullTrack = CreateDonutGeometry(center, rInner, rOuter);
                dc.DrawGeometry(trackBrush, null, fullTrack);
                return;
            }

            // 100%: Full active donut (complete circle) without gaps
            if (ratio >= 0.999)
            {
                var fullActive = CreateDonutGeometry(center, rInner, rOuter);
                dc.DrawGeometry(activeBrush, null, fullActive);
                return;
            }

            // Between 0% and 100%:
            // Maintain TWO equal, clearly visible gaps
            var totalGapSpace = centerGapAngle * 2.0;
            var availableForSegments = 360.0 - totalGapSpace;
            if (availableForSegments <= 10.0)
            {
                centerGapAngle = (360.0 - 20.0) / 2.0;
                availableForSegments = 20.0;
            }

            var activeSweepAngle = Math.Max(2.0, availableForSegments * ratio);
            var trackSweepAngle = Math.Max(2.0, availableForSegments * (1.0 - ratio));

            var activeStartAngle = -90.0;
            var trackStartAngle = activeStartAngle + activeSweepAngle + centerGapAngle;

            // 1. Draw Active Capsule Geometry
            var activeArcGeom = CreateCapsuleArcGeometry(center, rInner, rOuter, activeStartAngle, activeSweepAngle);
            if (activeArcGeom != null)
            {
                dc.DrawGeometry(activeBrush, null, activeArcGeom);
            }

            // 2. Draw Track Capsule Geometry (never extends under active arc)
            var trackArcGeom = CreateCapsuleArcGeometry(center, rInner, rOuter, trackStartAngle, trackSweepAngle);
            if (trackArcGeom != null)
            {
                dc.DrawGeometry(trackBrush, null, trackArcGeom);
            }
        }

        /// <summary>
        /// Creates a filled 2D capsule arc geometry with exact rounded semi-circular end-caps.
        /// Inner arc + rounded end cap + outer arc + rounded start cap.
        /// </summary>
        public static PathGeometry? CreateCapsuleArcGeometry(Point center, double rInner, double rOuter, double startAngleDeg, double sweepAngleDeg)
        {
            if (sweepAngleDeg <= 0.5) return null;
            if (rInner <= 0 || rOuter <= rInner) return null;

            var rCap = (rOuter - rInner) / 2.0;
            var startRad = startAngleDeg * (Math.PI / 180.0);
            var endRad = (startAngleDeg + sweepAngleDeg) * (Math.PI / 180.0);

            // Points on outer arc
            var pOuterStart = new Point(center.X + rOuter * Math.Cos(startRad), center.Y + rOuter * Math.Sin(startRad));
            var pOuterEnd = new Point(center.X + rOuter * Math.Cos(endRad), center.Y + rOuter * Math.Sin(endRad));

            // Points on inner arc
            var pInnerStart = new Point(center.X + rInner * Math.Cos(startRad), center.Y + rInner * Math.Sin(startRad));
            var pInnerEnd = new Point(center.X + rInner * Math.Cos(endRad), center.Y + rInner * Math.Sin(endRad));

            var isLargeArc = sweepAngleDeg > 180.0;

            var figure = new PathFigure
            {
                StartPoint = pOuterStart,
                IsClosed = true,
                IsFilled = true
            };

            // 1. Outer Arc from Start to End (Clockwise)
            figure.Segments.Add(new ArcSegment(pOuterEnd, new Size(rOuter, rOuter), 0, isLargeArc, SweepDirection.Clockwise, true));

            // 2. Rounded End Cap (from outer end to inner end, Clockwise)
            figure.Segments.Add(new ArcSegment(pInnerEnd, new Size(rCap, rCap), 0, false, SweepDirection.Clockwise, true));

            // 3. Inner Arc from End to Start (Counter-Clockwise)
            figure.Segments.Add(new ArcSegment(pInnerStart, new Size(rInner, rInner), 0, isLargeArc, SweepDirection.Counterclockwise, true));

            // 4. Rounded Start Cap (from inner start to outer start, Clockwise)
            figure.Segments.Add(new ArcSegment(pOuterStart, new Size(rCap, rCap), 0, false, SweepDirection.Clockwise, true));

            var geom = new PathGeometry(new[] { figure });
            geom.Freeze();
            return geom;
        }

        /// <summary>
        /// Creates a full circular donut geometry (for 0% or 100% boundary states).
        /// </summary>
        public static CombinedGeometry CreateDonutGeometry(Point center, double rInner, double rOuter)
        {
            var outer = new EllipseGeometry(center, rOuter, rOuter);
            var inner = new EllipseGeometry(center, rInner, rInner);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
            combined.Freeze();
            return combined;
        }
    }
}
