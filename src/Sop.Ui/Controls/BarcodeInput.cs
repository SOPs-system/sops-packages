using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public class BarcodeScanEventArgs : RoutedEventArgs
    {
        public string Barcode { get; }
        public bool IsHardwareScanner { get; }

        public BarcodeScanEventArgs(RoutedEvent routedEvent, string barcode, bool isHardwareScanner)
            : base(routedEvent)
        {
            Barcode = barcode;
            IsHardwareScanner = isHardwareScanner;
        }
    }

    [TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_ClearButton", Type = typeof(Button))]
    public class BarcodeInput : Control
    {
        private TextBox? _textBox;
        private Button? _clearButton;
        private readonly Stopwatch _burstTimer = new Stopwatch();
        private int _burstKeyCount;

        static BarcodeInput()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BarcodeInput), new FrameworkPropertyMetadata(typeof(BarcodeInput)));
            FocusableProperty.OverrideMetadata(typeof(BarcodeInput), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(BarcodeInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(BarcodeInput),
                new PropertyMetadata("Scan or type barcode..."));

        public static readonly DependencyProperty ClearOnSubmitProperty =
            DependencyProperty.Register(nameof(ClearOnSubmit), typeof(bool), typeof(BarcodeInput),
                new PropertyMetadata(true));

        public static readonly DependencyProperty KeepFocusOnScanProperty =
            DependencyProperty.Register(nameof(KeepFocusOnScan), typeof(bool), typeof(BarcodeInput),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(BarcodeInput),
                new PropertyMetadata(null));

        public static readonly RoutedEvent ScanSubmittedEvent =
            EventManager.RegisterRoutedEvent(nameof(ScanSubmitted), RoutingStrategy.Bubble,
                typeof(EventHandler<BarcodeScanEventArgs>), typeof(BarcodeInput));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool ClearOnSubmit
        {
            get => (bool)GetValue(ClearOnSubmitProperty);
            set => SetValue(ClearOnSubmitProperty, value);
        }

        public bool KeepFocusOnScan
        {
            get => (bool)GetValue(KeepFocusOnScanProperty);
            set => SetValue(KeepFocusOnScanProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public event EventHandler<BarcodeScanEventArgs> ScanSubmitted
        {
            add => AddHandler(ScanSubmittedEvent, value);
            remove => RemoveHandler(ScanSubmittedEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.KeyDown -= OnTextBoxKeyDown;
                _textBox.PreviewTextInput -= OnTextBoxPreviewTextInput;
            }
            if (_clearButton != null)
            {
                _clearButton.Click -= OnClearButtonClick;
            }

            _textBox = GetTemplateChild("PART_TextBox") as TextBox;
            _clearButton = GetTemplateChild("PART_ClearButton") as Button;

            if (_textBox != null)
            {
                _textBox.KeyDown += OnTextBoxKeyDown;
                _textBox.PreviewTextInput += OnTextBoxPreviewTextInput;
            }
            if (_clearButton != null)
            {
                _clearButton.Click += OnClearButtonClick;
            }
        }

        public new bool Focus()
        {
            return _textBox?.Focus() ?? base.Focus();
        }

        private void OnTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_burstTimer.IsRunning)
            {
                _burstTimer.Restart();
                _burstKeyCount = 1;
            }
            else
            {
                _burstKeyCount++;
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitScan();
            }
        }

        private void OnClearButtonClick(object sender, RoutedEventArgs e)
        {
            Text = string.Empty;
            _textBox?.Focus();
        }

        public void SubmitScan()
        {
            var raw = Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return;

            // Scanner detection: typical hardware scanners dump characters in < 45ms per keystroke
            var elapsed = _burstTimer.ElapsedMilliseconds;
            bool isHardwareScanner = _burstKeyCount >= 5 && elapsed > 0 && (elapsed / (double)_burstKeyCount) < 55.0;
            _burstTimer.Reset();
            _burstKeyCount = 0;

            var args = new BarcodeScanEventArgs(ScanSubmittedEvent, raw, isHardwareScanner);
            RaiseEvent(args);

            if (Command?.CanExecute(raw) == true)
            {
                Command.Execute(raw);
            }

            if (ClearOnSubmit)
            {
                Text = string.Empty;
            }

            if (KeepFocusOnScan)
            {
                _textBox?.Focus();
                _textBox?.SelectAll();
            }
        }
    }
}
