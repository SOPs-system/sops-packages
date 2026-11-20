using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// An individual segmented digit box cell inside a <see cref="PinInput"/>.
    /// Automatically styled by the design system with theme tokens, focus rings,
    /// and size variants.
    /// </summary>
    public class PinDigitBox : TextBox
    {
        static PinDigitBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PinDigitBox), new FrameworkPropertyMetadata(typeof(PinDigitBox)));
        }

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(PinDigitBox),
            new FrameworkPropertyMetadata(ControlSize.Md));

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }
    }

    /// <summary>
    /// A segmented numeric PIN / OTP input control that displays separate digit boxes
    /// according to <see cref="Length"/> (e.g. 4 or 6 digits).
    /// Supports automatic focus advancement, backspace navigation, paste handling,
    /// masking with bullet dots (•), and fires <see cref="Completed"/> when all digits are filled.
    /// </summary>
    [TemplatePart(Name = "PART_BoxesPanel", Type = typeof(Panel))]
    public class PinInput : Control
    {
        private Panel? _boxesPanel;
        private readonly List<PinDigitBox> _digitBoxes = new List<PinDigitBox>();
        private bool _isUpdatingInternally;

        static PinInput()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PinInput), new FrameworkPropertyMetadata(typeof(PinInput)));
            FocusableProperty.OverrideMetadata(typeof(PinInput), new FrameworkPropertyMetadata(true));
        }

        #region Dependency Properties

        /// <summary>Number of digit boxes (e.g. 4 for PIN, 6 for 2FA / OTP).</summary>
        public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(
            nameof(Length), typeof(int), typeof(PinInput),
            new FrameworkPropertyMetadata(4, (d, _) => ((PinInput)d).RebuildBoxes()));

        /// <summary>The full PIN string entered by the user.</summary>
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(PinInput),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((PinInput)d).OnValueChanged((string?)e.NewValue)));

        /// <summary>Alias for Value.</summary>
        public static readonly DependencyProperty PinProperty = DependencyProperty.Register(
            nameof(Pin), typeof(string), typeof(PinInput),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((PinInput)d).Value = (string?)e.NewValue ?? string.Empty));

        /// <summary>True to mask entered digits with bullets (•), false to display clear digits.</summary>
        public static readonly DependencyProperty IsMaskedProperty = DependencyProperty.Register(
            nameof(IsMasked), typeof(bool), typeof(PinInput),
            new FrameworkPropertyMetadata(true, (d, _) => ((PinInput)d).UpdateBoxVisuals()));

        /// <summary>Control size (Sm, Md, Lg).</summary>
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(PinInput),
            new FrameworkPropertyMetadata(ControlSize.Md, (d, _) => ((PinInput)d).UpdateSizes()));

        /// <summary>True if the PIN input is read-only.</summary>
        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(PinInput),
            new FrameworkPropertyMetadata(false, (d, _) => ((PinInput)d).UpdateReadOnlyState()));

        /// <summary>Masking character (default: '•').</summary>
        public static readonly DependencyProperty MaskCharProperty = DependencyProperty.Register(
            nameof(MaskChar), typeof(string), typeof(PinInput),
            new FrameworkPropertyMetadata("•", (d, _) => ((PinInput)d).UpdateBoxVisuals()));

        /// <summary>Optional custom style for each digit box cell.</summary>
        public static readonly DependencyProperty DigitBoxStyleProperty = DependencyProperty.Register(
            nameof(DigitBoxStyle), typeof(Style), typeof(PinInput),
            new FrameworkPropertyMetadata(null, (d, _) => ((PinInput)d).UpdateDigitBoxStyles()));

        public int Length
        {
            get => (int)GetValue(LengthProperty);
            set => SetValue(LengthProperty, Math.Max(2, Math.Min(12, value)));
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Pin
        {
            get => Value;
            set => Value = value;
        }

        public bool IsMasked
        {
            get => (bool)GetValue(IsMaskedProperty);
            set => SetValue(IsMaskedProperty, value);
        }

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public string MaskChar
        {
            get => (string)GetValue(MaskCharProperty);
            set => SetValue(MaskCharProperty, value);
        }

        public Style? DigitBoxStyle
        {
            get => (Style?)GetValue(DigitBoxStyleProperty);
            set => SetValue(DigitBoxStyleProperty, value);
        }

        #endregion

        #region Routed Events

        public static readonly RoutedEvent CompletedEvent = EventManager.RegisterRoutedEvent(
            nameof(Completed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinInput));

        public static readonly RoutedEvent PinChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(PinChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PinInput));

        /// <summary>Fired when all digits of the PIN have been entered.</summary>
        public event RoutedEventHandler Completed
        {
            add => AddHandler(CompletedEvent, value);
            remove => RemoveHandler(CompletedEvent, value);
        }

        /// <summary>Fired whenever any digit in the PIN changes.</summary>
        public event RoutedEventHandler PinChanged
        {
            add => AddHandler(PinChangedEvent, value);
            remove => RemoveHandler(PinChangedEvent, value);
        }

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _boxesPanel = GetTemplateChild("PART_BoxesPanel") as Panel;
            RebuildBoxes();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            if (e.OriginalSource == this && _digitBoxes.Count > 0)
            {
                // Focus the first empty digit box or the last box
                var firstEmpty = _digitBoxes.FirstOrDefault(b => string.IsNullOrEmpty(b.Text)) ?? _digitBoxes.Last();
                firstEmpty.Focus();
                firstEmpty.SelectAll();
                e.Handled = true;
            }
        }

        private void RebuildBoxes()
        {
            if (_boxesPanel == null) return;

            _boxesPanel.Children.Clear();
            _digitBoxes.Clear();

            int count = Math.Max(2, Math.Min(12, Length));
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var box = new PinDigitBox
                {
                    Name = $"PART_DigitBox_{index}",
                    MaxLength = 1,
                    IsReadOnly = IsReadOnly,
                    Size = Size,
                    Tag = index
                };

                if (DigitBoxStyle != null)
                {
                    box.Style = DigitBoxStyle;
                }

                box.PreviewKeyDown += (s, e) => OnBoxPreviewKeyDown(index, e);
                box.PreviewTextInput += (s, e) => OnBoxPreviewTextInput(index, e);
                DataObject.AddPastingHandler(box, (s, e) => OnBoxPaste(e));
                box.GotKeyboardFocus += (s, e) => box.SelectAll();
                box.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (!box.IsKeyboardFocusWithin)
                    {
                        box.Focus();
                        e.Handled = true;
                    }
                };

                _digitBoxes.Add(box);
                _boxesPanel.Children.Add(box);
            }

            UpdateBoxVisuals();
        }

        private void OnBoxPreviewKeyDown(int index, KeyEventArgs e)
        {
            if (IsReadOnly) return;

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                if (!string.IsNullOrEmpty(_digitBoxes[index].Text))
                {
                    SetDigit(index, '\0');
                }
                else if (index > 0)
                {
                    // Move back to previous box and clear it
                    int prev = index - 1;
                    _digitBoxes[prev].Focus();
                    SetDigit(prev, '\0');
                    _digitBoxes[prev].SelectAll();
                }
            }
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                SetDigit(index, '\0');
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
                int target = index - 1;
                if (target >= 0 && target < _digitBoxes.Count)
                {
                    _digitBoxes[target].Focus();
                    _digitBoxes[target].SelectAll();
                }
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
                int target = index + 1;
                if (target >= 0 && target < _digitBoxes.Count)
                {
                    _digitBoxes[target].Focus();
                    _digitBoxes[target].SelectAll();
                }
            }
        }

        private void OnBoxPreviewTextInput(int index, TextCompositionEventArgs e)
        {
            if (IsReadOnly)
            {
                e.Handled = true;
                return;
            }

            if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            char digit = e.Text[0];
            SetDigit(index, digit);

            // Focus next digit box
            if (index + 1 < _digitBoxes.Count)
            {
                _digitBoxes[index + 1].Focus();
                _digitBoxes[index + 1].SelectAll();
            }

            // Check if PIN is complete
            CheckCompletion();
        }

        private void OnBoxPaste(DataObjectPastingEventArgs e)
        {
            if (IsReadOnly || !e.SourceDataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.SourceDataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text))
            {
                e.CancelCommand();
                return;
            }

            e.CancelCommand();
            var digitsOnly = new string(text.Where(char.IsDigit).Take(Length).ToArray());
            if (digitsOnly.Length > 0)
            {
                Value = digitsOnly;
                int nextFocus = Math.Min(digitsOnly.Length, _digitBoxes.Count - 1);
                if (nextFocus >= 0 && nextFocus < _digitBoxes.Count)
                {
                    _digitBoxes[nextFocus].Focus();
                }
            }
        }

        private void SetDigit(int index, char digit)
        {
            var raw = Value ?? string.Empty;
            char[] chars = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                chars[i] = (i < raw.Length) ? raw[i] : ' ';
            }

            if (digit == '\0')
            {
                chars[index] = ' ';
            }
            else
            {
                chars[index] = digit;
            }

            // Trim trailing spaces for the clean string representation
            string newPin = new string(chars).TrimEnd();
            Value = newPin;
        }

        private void OnValueChanged(string? newValue)
        {
            if (_isUpdatingInternally) return;

            // Sync with Pin property
            if (Pin != newValue)
            {
                Pin = newValue ?? string.Empty;
            }

            UpdateBoxVisuals();
            RaiseEvent(new RoutedEventArgs(PinChangedEvent, this));
            CheckCompletion();
        }

        private void UpdateBoxVisuals()
        {
            if (_digitBoxes.Count == 0) return;

            _isUpdatingInternally = true;
            try
            {
                var pin = Value ?? string.Empty;
                for (int i = 0; i < _digitBoxes.Count; i++)
                {
                    if (i < pin.Length && pin[i] != ' ')
                    {
                        _digitBoxes[i].Text = IsMasked ? MaskChar : pin[i].ToString();
                    }
                    else
                    {
                        _digitBoxes[i].Text = string.Empty;
                    }
                }
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        private void UpdateSizes()
        {
            var size = Size;
            foreach (var box in _digitBoxes)
            {
                box.Size = size;
            }
        }

        private void UpdateReadOnlyState()
        {
            foreach (var box in _digitBoxes)
            {
                box.IsReadOnly = IsReadOnly;
            }
        }

        private void UpdateDigitBoxStyles()
        {
            var style = DigitBoxStyle;
            foreach (var box in _digitBoxes)
            {
                if (style != null)
                {
                    box.Style = style;
                }
                else
                {
                    box.ClearValue(StyleProperty);
                }
            }
        }

        private void CheckCompletion()
        {
            var val = Value ?? string.Empty;
            if (val.Length == Length && !val.Contains(' '))
            {
                RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
            }
        }

        /// <summary>Clears the entire entered PIN and refocuses the first digit box.</summary>
        public void Clear()
        {
            Value = string.Empty;
            if (_digitBoxes.Count > 0)
            {
                _digitBoxes[0].Focus();
            }
        }
    }
}
