using System;
using System.Windows;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// The one button. Variants live in Controls/Button.xaml and nowhere else —
    /// a screen that wants a differently-styled button gets a new variant
    /// there, never a one-off style at the call site.
    ///
    /// Sizes are POS touch targets: <c>Md</c> = 44px default, <c>Lg</c> = 56px
    /// register/keypad, <c>Sm</c> = 36px dense admin rows.
    /// </summary>
    public class Button : System.Windows.Controls.Button
    {
        static Button()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(typeof(Button)));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(ButtonVariant), typeof(Button), new FrameworkPropertyMetadata(ButtonVariant.Primary));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(Button), new FrameworkPropertyMetadata(ControlSize.Md));

        /// <summary>Square hit area for icon-only buttons (compose with any size).</summary>
        public static readonly DependencyProperty IconOnlyProperty = DependencyProperty.Register(
            nameof(IconOnly), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty FullWidthProperty = DependencyProperty.Register(
            nameof(FullWidth), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false));

        /// <summary>Shows a spinner and disables the button.</summary>
        public static readonly DependencyProperty LoadingProperty = DependencyProperty.Register(
            nameof(Loading), typeof(bool), typeof(Button),
            new FrameworkPropertyMetadata(false, (d, _) => d.CoerceValue(IsEnabledProperty)));

        /// <summary>Leading icon.</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(Button), new FrameworkPropertyMetadata(null));

        /// <summary>Fill that fades in over the base on hover (set per variant in Button.xaml).</summary>
        public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.Register(
            nameof(HoverBackground), typeof(System.Windows.Media.Brush), typeof(Button), new FrameworkPropertyMetadata(null));

        /// <summary>Fill that fades in while pressed.</summary>
        public static readonly DependencyProperty PressedBackgroundProperty = DependencyProperty.Register(
            nameof(PressedBackground), typeof(System.Windows.Media.Brush), typeof(Button), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(Button), new FrameworkPropertyMetadata(default(CornerRadius)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public System.Windows.Media.Brush? HoverBackground
        {
            get => (System.Windows.Media.Brush?)GetValue(HoverBackgroundProperty);
            set => SetValue(HoverBackgroundProperty, value);
        }

        public System.Windows.Media.Brush? PressedBackground
        {
            get => (System.Windows.Media.Brush?)GetValue(PressedBackgroundProperty);
            set => SetValue(PressedBackgroundProperty, value);
        }

        public ButtonVariant Variant
        {
            get => (ButtonVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public bool IconOnly
        {
            get => (bool)GetValue(IconOnlyProperty);
            set => SetValue(IconOnlyProperty, value);
        }

        public bool FullWidth
        {
            get => (bool)GetValue(FullWidthProperty);
            set => SetValue(FullWidthProperty, value);
        }

        public bool Loading
        {
            get => (bool)GetValue(LoadingProperty);
            set => SetValue(LoadingProperty, value);
        }

        public static Func<string, bool>? PermissionChecker { get; set; }
        public static Func<string, string?>? PermissionTooltipProvider { get; set; }
        public static event EventHandler? PermissionInvalidated;

        private static readonly System.Collections.Generic.List<WeakReference<Button>> ActiveButtons =
            new System.Collections.Generic.List<WeakReference<Button>>();

        public static void InvalidatePermissions()
        {
            lock (ActiveButtons)
            {
                for (var i = ActiveButtons.Count - 1; i >= 0; i--)
                {
                    if (ActiveButtons[i].TryGetTarget(out var btn))
                    {
                        if (btn.Dispatcher.CheckAccess())
                        {
                            btn.UpdatePermissionState();
                            btn.CoerceValue(IsEnabledProperty);
                        }
                        else if (!btn.Dispatcher.HasShutdownStarted && !btn.Dispatcher.HasShutdownFinished)
                        {
                            btn.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                btn.UpdatePermissionState();
                                btn.CoerceValue(IsEnabledProperty);
                            }));
                        }
                    }
                    else
                    {
                        ActiveButtons.RemoveAt(i);
                    }
                }
            }
            PermissionInvalidated?.Invoke(null, EventArgs.Empty);
        }

        public static readonly DependencyProperty PermissionProperty = DependencyProperty.Register(
            nameof(Permission), typeof(string), typeof(Button),
            new FrameworkPropertyMetadata(null, (d, _) =>
            {
                var btn = (Button)d;
                btn.UpdatePermissionState();
                btn.CoerceValue(IsEnabledProperty);
            }));

        public string? Permission
        {
            get => (string?)GetValue(PermissionProperty);
            set => SetValue(PermissionProperty, value);
        }

        private object? _originalToolTip;
        private bool _permissionToolTipApplied;

        public Button()
        {
            lock (ActiveButtons)
            {
                ActiveButtons.Add(new WeakReference<Button>(this));
            }
            Loaded += (_, __) =>
            {
                UpdatePermissionState();
                CoerceValue(IsEnabledProperty);
            };
        }

        private bool HasRequiredPermission()
        {
            var perm = Permission;
            if (string.IsNullOrEmpty(perm) || PermissionChecker == null)
                return true;
            return PermissionChecker(perm!);
        }

        private void UpdatePermissionState()
        {
            if (!HasRequiredPermission())
            {
                if (ToolTip != null && !_permissionToolTipApplied)
                {
                    _originalToolTip = ToolTip;
                }
                var perm = Permission;
                var msg = string.IsNullOrEmpty(perm) ? null : PermissionTooltipProvider?.Invoke(perm!);
                if (!string.IsNullOrEmpty(msg))
                {
                    if (_originalToolTip != null && !string.IsNullOrEmpty(_originalToolTip.ToString()))
                    {
                        ToolTip = $"{_originalToolTip} — {msg}";
                    }
                    else
                    {
                        ToolTip = msg;
                    }
                    _permissionToolTipApplied = true;
                }
            }
            else
            {
                if (_permissionToolTipApplied)
                {
                    ToolTip = _originalToolTip;
                    _originalToolTip = null;
                    _permissionToolTipApplied = false;
                }
            }
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        protected override bool IsEnabledCore => base.IsEnabledCore && !Loading && HasRequiredPermission();
    }

    /// <summary>
    /// Square icon-only button — ghost by default. Set
    /// <c>AutomationProperties.Name</c> so it is labelled for screen readers.
    /// </summary>
    public class IconButton : Button
    {
        static IconButton()
        {
            // Shares Button's style; only the defaults differ.
            DefaultStyleKeyProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(typeof(Button)));
            VariantProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(ButtonVariant.Ghost));
            IconOnlyProperty.OverrideMetadata(typeof(IconButton), new FrameworkPropertyMetadata(true));
        }
    }
}
