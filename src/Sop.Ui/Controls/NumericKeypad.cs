using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public class KeypadButtonPressedEventArgs : RoutedEventArgs
    {
        public string Key { get; }

        public KeypadButtonPressedEventArgs(RoutedEvent routedEvent, string key) : base(routedEvent)
        {
            Key = key;
        }
    }

    /// <summary>
    /// Touch-friendly POS cashier keypad for rapid quantity and cash tender entry.
    /// Provides 0-9, 00, decimal point, backspace, clear, quick-add denominations, and Enter.
    /// </summary>
    public class NumericKeypad : Control
    {
        static NumericKeypad()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericKeypad), new FrameworkPropertyMetadata(typeof(NumericKeypad)));
        }

        public NumericKeypad()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is Button btn && btn.Tag is string key)
            {
                HandleKey(key);
                e.Handled = true;
            }
        }

        public static readonly DependencyProperty TargetInputProperty =
            DependencyProperty.Register(nameof(TargetInput), typeof(TextBox), typeof(NumericKeypad),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(NumericKeypad),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ShowQuickCashProperty =
            DependencyProperty.Register(nameof(ShowQuickCash), typeof(bool), typeof(NumericKeypad),
                new PropertyMetadata(true));

        public static readonly RoutedEvent KeyPressedEvent =
            EventManager.RegisterRoutedEvent(nameof(KeyPressed), RoutingStrategy.Bubble,
                typeof(EventHandler<KeypadButtonPressedEventArgs>), typeof(NumericKeypad));

        public static readonly RoutedEvent EnterPressedEvent =
            EventManager.RegisterRoutedEvent(nameof(EnterPressed), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(NumericKeypad));

        public TextBox? TargetInput
        {
            get => (TextBox?)GetValue(TargetInputProperty);
            set => SetValue(TargetInputProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public bool ShowQuickCash
        {
            get => (bool)GetValue(ShowQuickCashProperty);
            set => SetValue(ShowQuickCashProperty, value);
        }

        public event EventHandler<KeypadButtonPressedEventArgs> KeyPressed
        {
            add => AddHandler(KeyPressedEvent, value);
            remove => RemoveHandler(KeyPressedEvent, value);
        }

        public event RoutedEventHandler EnterPressed
        {
            add => AddHandler(EnterPressedEvent, value);
            remove => RemoveHandler(EnterPressedEvent, value);
        }

        public void HandleKey(string key)
        {
            RaiseEvent(new KeypadButtonPressedEventArgs(KeyPressedEvent, key));

            if (key == "ENTER")
            {
                RaiseEvent(new RoutedEventArgs(EnterPressedEvent));
                return;
            }

            if (TargetInput != null)
            {
                ApplyToTextBox(TargetInput, key);
                return;
            }

            // Apply directly to Value property
            if (key == "CLEAR")
            {
                Value = string.Empty;
            }
            else if (key == "BACKSPACE")
            {
                if (!string.IsNullOrEmpty(Value))
                {
                    Value = Value.Substring(0, Value.Length - 1);
                }
            }
            else if (key == ".")
            {
                if (!Value.Contains("."))
                {
                    Value = string.IsNullOrEmpty(Value) ? "0." : Value + ".";
                }
            }
            else if (key.StartsWith("+"))
            {
                if (decimal.TryParse(key.Substring(1), out var addend))
                {
                    decimal.TryParse(Value, out var current);
                    Value = (current + addend).ToString("0.00");
                }
            }
            else
            {
                Value += key;
            }
        }

        private static void ApplyToTextBox(TextBox target, string key)
        {
            target.Focus();
            if (key == "CLEAR")
            {
                target.Text = string.Empty;
            }
            else if (key == "BACKSPACE")
            {
                if (target.SelectionLength > 0)
                {
                    int start = target.SelectionStart;
                    target.Text = target.Text.Remove(start, target.SelectionLength);
                    target.SelectionStart = start;
                }
                else if (target.SelectionStart > 0)
                {
                    int start = target.SelectionStart - 1;
                    target.Text = target.Text.Remove(start, 1);
                    target.SelectionStart = start;
                }
            }
            else if (key.StartsWith("+"))
            {
                if (decimal.TryParse(key.Substring(1), out var addend))
                {
                    decimal.TryParse(target.Text, out var current);
                    target.Text = (current + addend).ToString("0.00");
                    target.SelectionStart = target.Text.Length;
                }
            }
            else
            {
                int start = target.SelectionStart;
                target.Text = target.Text.Substring(0, start) + key + target.Text.Substring(start + target.SelectionLength);
                target.SelectionStart = start + key.Length;
            }
        }
    }
}
