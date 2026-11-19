using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    [TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_StepUp", Type = typeof(RepeatButton))]
    [TemplatePart(Name = "PART_StepDown", Type = typeof(RepeatButton))]
    public class NumericInput : Control
    {
        private TextBox? _textBox;
        private RepeatButton? _stepUpButton;
        private RepeatButton? _stepDownButton;
        private bool _isUpdatingText;

        static NumericInput()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericInput), new FrameworkPropertyMetadata(typeof(NumericInput)));
            FocusableProperty.OverrideMetadata(typeof(NumericInput), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(decimal?), typeof(NumericInput),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(decimal?), typeof(NumericInput),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(decimal?), typeof(NumericInput),
                new PropertyMetadata(null));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(decimal), typeof(NumericInput),
                new PropertyMetadata(1m));

        public static readonly DependencyProperty DecimalsProperty =
            DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(NumericInput),
                new PropertyMetadata(2, OnDecimalsChanged));

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(NumericInput),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(NumericInput),
                new PropertyMetadata(null));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NumericInput),
                new PropertyMetadata("0.00"));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NumericInput),
                new PropertyMetadata(false));

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<decimal?>), typeof(NumericInput));

        public decimal? Value
        {
            get => (decimal?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public decimal? Minimum
        {
            get => (decimal?)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public decimal? Maximum
        {
            get => (decimal?)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public decimal Step
        {
            get => (decimal)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public int Decimals
        {
            get => (int)GetValue(DecimalsProperty);
            set => SetValue(DecimalsProperty, value);
        }

        public string? Prefix
        {
            get => (string?)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string? Suffix
        {
            get => (string?)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<decimal?> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericInput)d;
            control.SyncTextFromValue();

            var args = new RoutedPropertyChangedEventArgs<decimal?>(
                (decimal?)e.OldValue,
                (decimal?)e.NewValue,
                ValueChangedEvent);
            control.RaiseEvent(args);
        }

        private static void OnDecimalsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericInput)d;
            control.SyncTextFromValue();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_stepUpButton != null) _stepUpButton.Click -= OnStepUpClick;
            if (_stepDownButton != null) _stepDownButton.Click -= OnStepDownClick;
            if (_textBox != null)
            {
                _textBox.TextChanged -= OnTextBoxTextChanged;
                _textBox.LostFocus -= OnTextBoxLostFocus;
                _textBox.PreviewTextInput -= OnTextBoxPreviewTextInput;
                _textBox.KeyDown -= OnTextBoxKeyDown;
            }

            _textBox = GetTemplateChild("PART_TextBox") as TextBox;
            _stepUpButton = GetTemplateChild("PART_StepUp") as RepeatButton;
            _stepDownButton = GetTemplateChild("PART_StepDown") as RepeatButton;

            if (_stepUpButton != null) _stepUpButton.Click += OnStepUpClick;
            if (_stepDownButton != null) _stepDownButton.Click += OnStepDownClick;
            if (_textBox != null)
            {
                _textBox.TextChanged += OnTextBoxTextChanged;
                _textBox.LostFocus += OnTextBoxLostFocus;
                _textBox.PreviewTextInput += OnTextBoxPreviewTextInput;
                _textBox.KeyDown += OnTextBoxKeyDown;
            }

            SyncTextFromValue();
        }

        private void OnStepUpClick(object sender, RoutedEventArgs e)
        {
            if (IsReadOnly) return;
            var current = Value ?? (Minimum ?? 0m);
            var next = current + Step;
            if (Maximum.HasValue && next > Maximum.Value) next = Maximum.Value;
            Value = Math.Round(next, Decimals);
        }

        private void OnStepDownClick(object sender, RoutedEventArgs e)
        {
            if (IsReadOnly) return;
            var current = Value ?? (Minimum ?? 0m);
            var next = current - Step;
            if (Minimum.HasValue && next < Minimum.Value) next = Minimum.Value;
            Value = Math.Round(next, Decimals);
        }

        private void SyncTextFromValue()
        {
            if (_isUpdatingText || _textBox == null) return;
            _isUpdatingText = true;
            try
            {
                if (!Value.HasValue)
                {
                    _textBox.Text = string.Empty;
                }
                else
                {
                    var format = Decimals > 0 ? $"N{Decimals}" : "N0";
                    _textBox.Text = Value.Value.ToString(format, CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                _isUpdatingText = false;
            }
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText || _textBox == null) return;

            var raw = _textBox.Text.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                Value = null;
                return;
            }

            // Strip thousands separators for parsing
            var sanitized = raw.Replace(",", string.Empty);
            if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                if (Minimum.HasValue && parsed < Minimum.Value) parsed = Minimum.Value;
                if (Maximum.HasValue && parsed > Maximum.Value) parsed = Maximum.Value;
                parsed = Math.Round(parsed, Decimals);

                if (Value != parsed)
                {
                    _isUpdatingText = true;
                    try
                    {
                        Value = parsed;
                    }
                    finally
                    {
                        _isUpdatingText = false;
                    }
                }
            }
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            SyncTextFromValue();
        }

        private void OnTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (var ch in e.Text)
            {
                if (char.IsDigit(ch)) continue;
                if (ch == '.' && Decimals > 0 && !(_textBox?.Text.Contains(".") == true)) continue;
                if (ch == '-' && (Minimum == null || Minimum < 0) && _textBox?.SelectionStart == 0 && !(_textBox?.Text.Contains("-") == true)) continue;

                e.Handled = true;
                return;
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (IsReadOnly) return;

            if (e.Key == Key.Up)
            {
                OnStepUpClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                OnStepDownClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
