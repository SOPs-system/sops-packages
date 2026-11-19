using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Text field on the shared control frame (Select's trigger uses the same
    /// frame, which is what keeps a text box and a picker visually identical
    /// in a form row). Affixes live inside the frame.
    /// </summary>
    public class Input : TextBox
    {
        static Input()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Input), new FrameworkPropertyMetadata(typeof(Input)));
        }

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(Input), new FrameworkPropertyMetadata(ControlSize.Md));

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(Input), new FrameworkPropertyMetadata(null));

        /// <summary>e.g. a currency symbol or search icon.</summary>
        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(
            nameof(Prefix), typeof(object), typeof(Input),
            new FrameworkPropertyMetadata(null, (d, e) => d.SetValue(HasPrefixPropertyKey, e.NewValue != null)));

        public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
            nameof(Suffix), typeof(object), typeof(Input),
            new FrameworkPropertyMetadata(null, (d, e) => d.SetValue(HasSuffixPropertyKey, e.NewValue != null)));

        /// <summary>Tabular figures + end alignment for amounts.</summary>
        public static readonly DependencyProperty NumericProperty = DependencyProperty.Register(
            nameof(Numeric), typeof(bool), typeof(Input), new FrameworkPropertyMetadata(false));

        private static readonly DependencyPropertyKey HasPrefixPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasPrefix), typeof(bool), typeof(Input), new FrameworkPropertyMetadata(false));

        private static readonly DependencyPropertyKey HasSuffixPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasSuffix), typeof(bool), typeof(Input), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty HasPrefixProperty = HasPrefixPropertyKey.DependencyProperty;
        public static readonly DependencyProperty HasSuffixProperty = HasSuffixPropertyKey.DependencyProperty;

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public string? Placeholder
        {
            get => (string?)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public object? Prefix
        {
            get => GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public object? Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public bool Numeric
        {
            get => (bool)GetValue(NumericProperty);
            set => SetValue(NumericProperty, value);
        }

        public bool HasPrefix => (bool)GetValue(HasPrefixProperty);
        public bool HasSuffix => (bool)GetValue(HasSuffixProperty);
    }
}
