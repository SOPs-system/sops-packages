using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Indeterminate progress ring: Material 3 style with rounded arc caps,
    /// drawn as precise capsule geometry with distinct visible gaps.
    /// Sizes Sm 14 · Md 18 · Lg 24 (or explicit Width/Height).
    /// Inherits the foreground color of its container (e.g. white inside a button).
    /// </summary>
    public class Spinner : Control
    {
        private readonly RotateTransform _rotation = new RotateTransform();

        static Spinner()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Spinner), new FrameworkPropertyMetadata(typeof(Spinner)));
            FocusableProperty.OverrideMetadata(typeof(Spinner), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(Spinner),
            new FrameworkPropertyMetadata(ControlSize.Md, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public Spinner()
        {
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = _rotation;
            // Turns only while it can be seen
            IsVisibleChanged += (_, e) => Spin(e.NewValue is bool visible && visible);
        }

        private void Spin(bool on)
        {
            if (!on || !Motion.Enabled)
            {
                _rotation.BeginAnimation(RotateTransform.AngleProperty, null);
                return;
            }
            var turn = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1200))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _rotation.BeginAnimation(RotateTransform.AngleProperty, turn);
        }

        private double Diameter => Size switch
        {
            ControlSize.Sm => Token("Size.SpinnerSm", 14),
            ControlSize.Lg => Token("Size.SpinnerLg", 24),
            _ => Token("Size.Spinner", 18),
        };

        private double Token(string key, double fallback) =>
            TryFindResource(key) is double value ? value : fallback;

        protected override Size MeasureOverride(Size constraint)
        {
            var d = Diameter;
            return new Size(d, d);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var stroke = Token("Size.SpinnerStroke", 2.5);
            var d = Math.Min(ActualWidth, ActualHeight);
            if (d <= stroke) return;

            var rMid = (d - stroke) / 2.0;
            var rInner = rMid - (stroke / 2.0);
            var rOuter = rMid + (stroke / 2.0);
            var center = new Point(d / 2.0, d / 2.0);

            var activeBrush = Foreground;
            var trackBrush = TryFindResource("Brush.SurfaceSunken") as Brush ?? Background;

            // Geometry-based capsule drawing
            var capAngle = rMid > 0 ? ((stroke / 2.0) / rMid) * (180.0 / Math.PI) : 4.0;
            const double requestedGap = 16.0;
            var centerGapAngle = requestedGap + (2.0 * capAngle);

            const double activeSweep = 250.0;
            var trackSweep = 360.0 - activeSweep - (centerGapAngle * 2.0);

            // Active Arc Capsule
            var activeGeom = SopsCircularProgress.CreateCapsuleArcGeometry(center, rInner, rOuter, -90.0, activeSweep);
            if (activeGeom != null)
            {
                dc.DrawGeometry(activeBrush, null, activeGeom);
            }

            // Track Arc Capsule separated by two wide gaps
            if (trackSweep > 4.0)
            {
                var trackStart = -90.0 + activeSweep + centerGapAngle;
                var trackGeom = SopsCircularProgress.CreateCapsuleArcGeometry(center, rInner, rOuter, trackStart, trackSweep);
                if (trackGeom != null)
                {
                    dc.DrawGeometry(trackBrush, null, trackGeom);
                }
            }
        }
    }
}
