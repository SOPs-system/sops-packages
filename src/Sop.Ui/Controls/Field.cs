using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Wraps a form control with label, hint, and error message.
    /// <code>
    /// &lt;ui:Field Label="Price" Error="{Binding PriceError}"&gt;
    ///   &lt;ui:Input Numeric="True" /&gt;
    /// &lt;/ui:Field&gt;
    /// </code>
    /// A Field carrying an <see cref="Error"/> marks itself invalid through the
    /// inherited attached <see cref="IsInvalidProperty"/>, and every control
    /// inside turns red by itself — Input, Select and anything else on the
    /// control frame need no property threaded through the call site.
    /// </summary>
    [ContentProperty(nameof(Content))]
    public class Field : Control
    {
        static Field()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(typeof(Field)));
            FocusableProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(false));
        }

        /// <summary>
        /// Inherited flag read by control templates (<c>ui:Field.IsInvalid</c>)
        /// so they can paint the danger frame. Set by the Field, not by callers.
        /// </summary>
        public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.RegisterAttached(
            "IsInvalid", typeof(bool), typeof(Field),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsInvalid(DependencyObject element) => (bool)element.GetValue(IsInvalidProperty);
        public static void SetIsInvalid(DependencyObject element, bool value) => element.SetValue(IsInvalidProperty, value);

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(object), typeof(Field), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
            nameof(Hint), typeof(object), typeof(Field), new FrameworkPropertyMetadata(null));

        /// <summary>Present ⇒ replaces the hint, and every control inside turns red.</summary>
        public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(
            nameof(Error), typeof(object), typeof(Field),
            new FrameworkPropertyMetadata(null, (d, e) => SetIsInvalid(d, e.NewValue != null && !(e.NewValue is string s && s.Length == 0))));

        public static readonly DependencyProperty RequiredProperty = DependencyProperty.Register(
            nameof(Required), typeof(bool), typeof(Field), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(object), typeof(Field), new FrameworkPropertyMetadata(null));

        public object? Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public object? Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        public object? Error
        {
            get => GetValue(ErrorProperty);
            set => SetValue(ErrorProperty, value);
        }

        public bool Required
        {
            get => (bool)GetValue(RequiredProperty);
            set => SetValue(RequiredProperty, value);
        }

        public object? Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_Label") is FrameworkElement label)
            {
                // Clicking the label focuses the control, like <label for>.
                label.MouseLeftButtonDown += (_, e) =>
                {
                    if (FocusFirstControl()) e.Handled = true;
                };
            }
        }

        private bool FocusFirstControl()
        {
            if (Content is not DependencyObject root) return false;
            var target = FindFocusable(root);
            return target != null && target.Focus();
        }

        private static IInputElement? FindFocusable(DependencyObject node)
        {
            if (node is UIElement ui && ui.Focusable && ui.IsEnabled && ui.IsVisible) return ui;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var found = FindFocusable(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
