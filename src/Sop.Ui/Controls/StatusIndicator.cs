using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// App state indicator (top-bar / state-row) matching the HTML spec:
    /// an 8px colored dot paired with a status label using an exact 8px gap (Space.2).
    /// </summary>
    public class StatusIndicator : Control
    {
        static StatusIndicator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusIndicator), new FrameworkPropertyMetadata(typeof(StatusIndicator)));
            FocusableProperty.OverrideMetadata(typeof(StatusIndicator), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(StatusVariant), typeof(StatusIndicator), new FrameworkPropertyMetadata(StatusVariant.Success));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StatusIndicator), new FrameworkPropertyMetadata(string.Empty));

        public StatusVariant Variant
        {
            get => (StatusVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}
