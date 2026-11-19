using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>A hairline rule, with an optional centred label.</summary>
    public class Divider : Control
    {
        static Divider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Divider), new FrameworkPropertyMetadata(typeof(Divider)));
            FocusableProperty.OverrideMetadata(typeof(Divider), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(Divider), new FrameworkPropertyMetadata(null));

        public string? Label
        {
            get => (string?)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
    }
}
