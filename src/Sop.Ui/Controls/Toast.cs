using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sop.Ui.Controls
{
    /// <summary>Transient notification card. The host that stacks them arrives with the shell.</summary>
    public class Toast : Control
    {
        public Toast()
        {
            Loaded += (_, __) => PlayEntrance();
        }

        static Toast()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Toast), new FrameworkPropertyMetadata(typeof(Toast)));
            FocusableProperty.OverrideMetadata(typeof(Toast), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(ToastVariant), typeof(Toast), new FrameworkPropertyMetadata(ToastVariant.Neutral));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(object), typeof(Toast), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
            nameof(Subtitle), typeof(object), typeof(Toast), new FrameworkPropertyMetadata(null));

        /// <summary>Shows the dismiss button; it raises <see cref="Dismiss"/>.</summary>
        public static readonly DependencyProperty DismissableProperty = DependencyProperty.Register(
            nameof(Dismissable), typeof(bool), typeof(Toast), new FrameworkPropertyMetadata(false));

        public static readonly RoutedEvent DismissEvent = EventManager.RegisterRoutedEvent(
            nameof(Dismiss), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Toast));

        public ToastVariant Variant
        {
            get => (ToastVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public object? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object? Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public bool Dismissable
        {
            get => (bool)GetValue(DismissableProperty);
            set => SetValue(DismissableProperty, value);
        }

        public event RoutedEventHandler Dismiss
        {
            add => AddHandler(DismissEvent, value);
            remove => RemoveHandler(DismissEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_Dismiss") is System.Windows.Controls.Button button)
                button.Click += (_, __) => RaiseEvent(new RoutedEventArgs(DismissEvent, this));
        }

        /// <summary>
        /// Entrance: subtle scale + fade, the theme's `animate-in`. Driven from
        /// code for the same reason MenuPanel's is — a Loaded storyboard in the
        /// template resolves its target names against whatever name scope the
        /// toast is declared in, which throws once it sits inside a UserControl
        /// rather than directly in a window.
        /// </summary>
        public void PlayEntrance()
        {
            if (GetTemplateChild("Root") is not FrameworkElement root) return;
            var duration = new Duration(TimeSpan.FromMilliseconds(150));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
            if (root.RenderTransform is TransformGroup group && group.Children.Count == 2
                && group.Children[0] is ScaleTransform scale && group.Children[1] is TranslateTransform slide)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, duration) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, duration) { EasingFunction = ease });
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(2, 0, duration));
            }
        }
    }
}
