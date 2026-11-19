using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// A password field on the Input frame: masked by default, with the
    /// eye toggle the web's login form puts in the suffix slot. The value is
    /// a plain <see cref="Password"/> string (two-way) — a register's
    /// sign-in box has no use for SecureString theatre, the hash is what is
    /// stored. Enter raises <see cref="Submit"/>.
    /// </summary>
    [TemplatePart(Name = "PART_Password", Type = typeof(PasswordBox))]
    [TemplatePart(Name = "PART_Text", Type = typeof(TextBox))]
    public class PasswordInput : Control
    {
        private PasswordBox? _password;
        private TextBox? _text;
        private bool _syncing;

        static PasswordInput()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PasswordInput), new FrameworkPropertyMetadata(typeof(PasswordInput)));
        }

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
            nameof(Password), typeof(string), typeof(PasswordInput),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((PasswordInput)d).PushValue((string?)e.NewValue ?? string.Empty)));

        /// <summary>True while the characters are shown in clear.</summary>
        public static readonly DependencyProperty RevealedProperty = DependencyProperty.Register(
            nameof(Revealed), typeof(bool), typeof(PasswordInput),
            new FrameworkPropertyMetadata(false, (d, _) => ((PasswordInput)d).OnRevealedChanged()));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(PasswordInput), new FrameworkPropertyMetadata(ControlSize.Md));

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(PasswordInput), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(
            nameof(Prefix), typeof(object), typeof(PasswordInput), new FrameworkPropertyMetadata(null));

        /// <summary>Tooltip / automation name of the eye button ("Show password").</summary>
        public static readonly DependencyProperty ToggleLabelProperty = DependencyProperty.Register(
            nameof(ToggleLabel), typeof(string), typeof(PasswordInput), new FrameworkPropertyMetadata(null));

        private static readonly DependencyPropertyKey IsEmptyPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsEmpty), typeof(bool), typeof(PasswordInput), new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;

        public static readonly RoutedEvent SubmitEvent = EventManager.RegisterRoutedEvent(
            nameof(Submit), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PasswordInput));

        public static readonly RoutedEvent PasswordChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(PasswordChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PasswordInput));

        /// <summary>The value changed, by typing or by code.</summary>
        public event RoutedEventHandler PasswordChanged
        {
            add => AddHandler(PasswordChangedEvent, value);
            remove => RemoveHandler(PasswordChangedEvent, value);
        }

        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public bool Revealed
        {
            get => (bool)GetValue(RevealedProperty);
            set => SetValue(RevealedProperty, value);
        }

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

        public string? ToggleLabel
        {
            get => (string?)GetValue(ToggleLabelProperty);
            set => SetValue(ToggleLabelProperty, value);
        }

        public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

        /// <summary>Enter pressed inside the field.</summary>
        public event RoutedEventHandler Submit
        {
            add => AddHandler(SubmitEvent, value);
            remove => RemoveHandler(SubmitEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_password != null) _password.PasswordChanged -= OnBoxChanged;
            if (_text != null) _text.TextChanged -= OnTextChanged;
            _password = GetTemplateChild("PART_Password") as PasswordBox;
            _text = GetTemplateChild("PART_Text") as TextBox;
            if (_password != null) _password.PasswordChanged += OnBoxChanged;
            if (_text != null) _text.TextChanged += OnTextChanged;
            if (GetTemplateChild("PART_Toggle") is System.Windows.Controls.Primitives.ButtonBase toggle)
                toggle.Click += (_, __) => Revealed = !Revealed;
            PushValue(Password);
        }

        /// <summary>Move keyboard focus into whichever box is showing.</summary>
        public new void Focus()
        {
            var target = Revealed ? (Control?)_text : _password;
            if (target != null) Keyboard.Focus(target);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            // Focus landing on the frame forwards to the box inside.
            if (e.NewFocus == this) Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter && !e.Handled)
            {
                RaiseEvent(new RoutedEventArgs(SubmitEvent, this));
                e.Handled = true;
            }
        }

        private void OnBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing || _password == null) return;
            _syncing = true;
            Password = _password.Password;
            _syncing = false;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing || _text == null) return;
            _syncing = true;
            Password = _text.Text;
            _syncing = false;
        }

        private void PushValue(string value)
        {
            SetValue(IsEmptyPropertyKey, value.Length == 0);
            RaiseEvent(new RoutedEventArgs(PasswordChangedEvent, this));
            if (_syncing) return;
            _syncing = true;
            if (_password != null && _password.Password != value) _password.Password = value;
            if (_text != null && _text.Text != value) _text.Text = value;
            _syncing = false;
        }

        private void OnRevealedChanged()
        {
            // Keep the caret where the cashier is looking when the mask flips.
            var focused = IsKeyboardFocusWithin;
            PushValue(Password);
            if (focused) Dispatcher.BeginInvoke(new System.Action(Focus), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
