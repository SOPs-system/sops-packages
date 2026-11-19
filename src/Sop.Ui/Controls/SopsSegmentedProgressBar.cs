using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Capsule Segmented Progress Bar (SopsSegmentedProgressBar).
    /// Two horizontal pill-shaped segments separated by an exact gap.
    /// All four ends are fully rounded (radius = BarHeight / 2).
    /// Supports smooth 200ms transitions, custom brushes, and theme tokens.
    /// </summary>
    public class SopsSegmentedProgressBar : Control
    {
        private FrameworkElement? _completedSegment;
        private FrameworkElement? _remainingSegment;
        private TextBlock? _percentageLabel;
        private ColumnDefinition? _colCompleted;
        private ColumnDefinition? _colGap;
        private ColumnDefinition? _colRemaining;

        static SopsSegmentedProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SopsSegmentedProgressBar),
                new FrameworkPropertyMetadata(typeof(SopsSegmentedProgressBar)));
            FocusableProperty.OverrideMetadata(
                typeof(SopsSegmentedProgressBar),
                new FrameworkPropertyMetadata(false));
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceValue));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged, CoerceMaximum));

        public static readonly DependencyProperty CompletedBrushProperty = DependencyProperty.Register(
            nameof(CompletedBrush), typeof(Brush), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty RemainingBrushProperty = DependencyProperty.Register(
            nameof(RemainingBrush), typeof(Brush), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
            nameof(BarHeight), typeof(double), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnDimensionsChanged));

        public static readonly DependencyProperty SegmentGapProperty = DependencyProperty.Register(
            nameof(SegmentGap), typeof(double), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnDimensionsChanged));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(new CornerRadius(4.0), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
            nameof(IsAnimated), typeof(bool), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(
            nameof(AnimationDuration), typeof(Duration), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(new Duration(TimeSpan.FromMilliseconds(200))));

        public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(
            nameof(ShowPercentage), typeof(bool), typeof(SopsSegmentedProgressBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region CLR Properties

        /// <summary>Current progress value (0 to Maximum).</summary>
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Maximum progress value (default: 100).</summary>
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>Brush for the completed progress segment (defaults to Brush.Primary theme brush).</summary>
        public Brush? CompletedBrush
        {
            get => (Brush?)GetValue(CompletedBrushProperty);
            set => SetValue(CompletedBrushProperty, value);
        }

        /// <summary>Brush for the remaining track segment (defaults to Brush.SurfaceSunken theme brush).</summary>
        public Brush? RemainingBrush
        {
            get => (Brush?)GetValue(RemainingBrushProperty);
            set => SetValue(RemainingBrushProperty, value);
        }

        /// <summary>Height of the progress segments (default: 8px).</summary>
        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }

        /// <summary>Gap space between segments (default: 4px).</summary>
        public double SegmentGap
        {
            get => (double)GetValue(SegmentGapProperty);
            set => SetValue(SegmentGapProperty, value);
        }

        /// <summary>Corner radius of segment ends (default: BarHeight / 2 for full pill).</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>Whether progress changes animate smoothly (default: true).</summary>
        public bool IsAnimated
        {
            get => (bool)GetValue(IsAnimatedProperty);
            set => SetValue(IsAnimatedProperty, value);
        }

        /// <summary>Duration of progress transitions (default: 200ms).</summary>
        public Duration AnimationDuration
        {
            get => (Duration)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        /// <summary>Whether to display a text percentage indicator beside or above (default: false).</summary>
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
                var bar = (SopsSegmentedProgressBar)d;
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
            if (d is SopsSegmentedProgressBar bar)
            {
                bar.UpdateSegments();
            }
        }

        private static void OnDimensionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SopsSegmentedProgressBar bar)
            {
                var half = Math.Max(0, bar.BarHeight / 2.0);
                bar.SetCurrentValue(CornerRadiusProperty, new CornerRadius(half));
                bar.UpdateSegments();
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _completedSegment = GetTemplateChild("PART_CompletedSegment") as FrameworkElement;
            _remainingSegment = GetTemplateChild("PART_RemainingSegment") as FrameworkElement;
            _percentageLabel = GetTemplateChild("PART_PercentageLabel") as TextBlock;
            _colCompleted = GetTemplateChild("PART_ColCompleted") as ColumnDefinition;
            _colGap = GetTemplateChild("PART_ColGap") as ColumnDefinition;
            _colRemaining = GetTemplateChild("PART_ColRemaining") as ColumnDefinition;

            var half = Math.Max(0, BarHeight / 2.0);
            SetCurrentValue(CornerRadiusProperty, new CornerRadius(half));

            UpdateSegments();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateSegments();
        }

        public void UpdateSegments()
        {
            var max = Maximum <= 0.0 ? 100.0 : Maximum;
            var val = Math.Max(0.0, Math.Min(Value, max));
            var ratio = val / max;

            if (_percentageLabel != null)
            {
                var pct = (int)Math.Round(ratio * 100.0);
                _percentageLabel.Text = $"{pct}%";
            }

            // At 0%: only remaining segment is visible, no gap
            if (ratio <= 0.0)
            {
                if (_colCompleted != null) _colCompleted.Width = new GridLength(0);
                if (_colGap != null) _colGap.Width = new GridLength(0);
                if (_colRemaining != null) _colRemaining.Width = new GridLength(1, GridUnitType.Star);

                if (_completedSegment != null) _completedSegment.Visibility = Visibility.Collapsed;
                if (_remainingSegment != null) _remainingSegment.Visibility = Visibility.Visible;
                return;
            }

            // At 100%: only completed segment is visible, no gap
            if (ratio >= 1.0)
            {
                if (_colCompleted != null) _colCompleted.Width = new GridLength(1, GridUnitType.Star);
                if (_colGap != null) _colGap.Width = new GridLength(0);
                if (_colRemaining != null) _colRemaining.Width = new GridLength(0);

                if (_completedSegment != null) _completedSegment.Visibility = Visibility.Visible;
                if (_remainingSegment != null) _remainingSegment.Visibility = Visibility.Collapsed;
                return;
            }

            // Between 0% and 100%: both segments visible, gap active
            if (_completedSegment != null) _completedSegment.Visibility = Visibility.Visible;
            if (_remainingSegment != null) _remainingSegment.Visibility = Visibility.Visible;

            if (_colGap != null)
            {
                _colGap.Width = new GridLength(Math.Max(0, SegmentGap));
            }

            var compWeight = Math.Max(0.001, ratio);
            var remWeight = Math.Max(0.001, 1.0 - ratio);

            if (_colCompleted != null) _colCompleted.Width = new GridLength(compWeight, GridUnitType.Star);
            if (_colRemaining != null) _colRemaining.Width = new GridLength(remWeight, GridUnitType.Star);
        }
    }
}
