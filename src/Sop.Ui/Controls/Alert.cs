using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>Inline message banner.</summary>
    public class Alert : ContentControl
    {
        static Alert()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(typeof(Alert)));
            FocusableProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(StatusVariant), typeof(Alert), new FrameworkPropertyMetadata(StatusVariant.Info));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(object), typeof(Alert), new FrameworkPropertyMetadata(null));

        public StatusVariant Variant
        {
            get => (StatusVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Shows the close button; it raises <see cref="Dismiss"/>. Off by
        /// default, deliberately: most banners state a condition that is still
        /// true — an allergy, an immutable role, a warning about what a button
        /// will do — and those must not be closable. Turn it on where the
        /// message is an event the reader can be finished with.
        /// </summary>
        public static readonly DependencyProperty DismissableProperty = DependencyProperty.Register(
            nameof(Dismissable), typeof(bool), typeof(Alert), new FrameworkPropertyMetadata(false));

        public static readonly RoutedEvent DismissEvent = EventManager.RegisterRoutedEvent(
            nameof(Dismiss), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Alert));

        public object? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool Dismissable
        {
            get => (bool)GetValue(DismissableProperty);
            set => SetValue(DismissableProperty, value);
        }

        /// <summary>The close button was clicked; what that means is the caller's.</summary>
        public event RoutedEventHandler Dismiss
        {
            add => AddHandler(DismissEvent, value);
            remove => RemoveHandler(DismissEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_Dismiss") is not System.Windows.Controls.Button button) return;
            button.Click += (_, __) =>
            {
                var args = new RoutedEventArgs(DismissEvent, this);
                RaiseEvent(args);
                // Closing a banner means hiding it, which is all any caller here
                // wants. One that needs to do more handles Dismiss and takes
                // over — marking it handled keeps the banner up.
                if (!args.Handled) Visibility = Visibility.Collapsed;
            };
        }
    }
}
