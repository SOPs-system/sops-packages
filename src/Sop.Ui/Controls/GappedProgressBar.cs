using System;
using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Modern Android Material 3 style gapped progress indicator.
    /// Divides progress into discrete segments separated by an exact gap (Space.1 = 4px or Space.2 = 8px).
    /// </summary>
    public class GappedProgressBar : Control
    {
        static GappedProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GappedProgressBar), new FrameworkPropertyMetadata(typeof(GappedProgressBar)));
            FocusableProperty.OverrideMetadata(typeof(GappedProgressBar), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
            nameof(Segments), typeof(int), typeof(GappedProgressBar),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender, OnSegmentsOrValueChanged));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(GappedProgressBar),
            new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsRender, OnSegmentsOrValueChanged));

        public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
            nameof(Gap), typeof(double), typeof(GappedProgressBar),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnSegmentsOrValueChanged));

        public static readonly DependencyProperty TrackHeightProperty = DependencyProperty.Register(
            nameof(TrackHeight), typeof(double), typeof(GappedProgressBar),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(StatusVariant), typeof(GappedProgressBar),
            new FrameworkPropertyMetadata(StatusVariant.Success, OnSegmentsOrValueChanged));

        public int Segments
        {
            get => (int)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, Math.Max(1, value));
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, Math.Max(0, value));
        }

        public double Gap
        {
            get => (double)GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        public double TrackHeight
        {
            get => (double)GetValue(TrackHeightProperty);
            set => SetValue(TrackHeightProperty, value);
        }

        public StatusVariant Variant
        {
            get => (StatusVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            RebuildSegments();
        }

        private static void OnSegmentsOrValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GappedProgressBar)d).RebuildSegments();
        }

        private void RebuildSegments()
        {
            if (GetTemplateChild("PART_Container") is Panel container)
            {
                container.Children.Clear();
                var count = Math.Max(1, Segments);
                var active = Math.Min(count, Math.Max(0, Value));
                var gap = Gap;

                for (var i = 0; i < count; i++)
                {
                    var isCompleted = i < active;
                    var border = new Border
                    {
                        CornerRadius = new CornerRadius(3),
                        Height = TrackHeight,
                        Margin = (i < count - 1) ? new Thickness(0, 0, gap, 0) : new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    };

                    if (isCompleted)
                    {
                        border.SetResourceReference(Border.BackgroundProperty, GetBrushKeyForVariant(Variant));
                    }
                    else
                    {
                        border.SetResourceReference(Border.BackgroundProperty, "Brush.SurfaceSunken");
                    }

                    container.Children.Add(border);
                }
            }
        }

        private static string GetBrushKeyForVariant(StatusVariant variant)
        {
            switch (variant)
            {
                case StatusVariant.Warning: return "Brush.Warning";
                case StatusVariant.Danger: return "Brush.Danger";
                case StatusVariant.Info: return "Brush.Primary";
                case StatusVariant.Success:
                default: return "Brush.Success";
            }
        }
    }
}
