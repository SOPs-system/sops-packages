using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>Status pill — order states, stock levels, payment status.</summary>
    public class Badge : ContentControl
    {
        static Badge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(Badge)));
            FocusableProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(BadgeVariant), typeof(Badge), new FrameworkPropertyMetadata(BadgeVariant.Neutral));

        public BadgeVariant Variant
        {
            get => (BadgeVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }
    }
}
