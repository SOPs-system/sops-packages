using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui
{
    /// <summary>
    /// The runtime theme switch — the WPF equivalent of flipping
    /// <c>data-theme</c> / <c>dir</c> on <c>&lt;html&gt;</c>.
    ///
    /// Every control style resolves its brushes, fonts and sizes through
    /// <c>DynamicResource</c>, so swapping the token dictionary in
    /// <see cref="Application.Resources"/> re-themes the running app with no
    /// per-control code. Re-theming the app is an edit to the token files only.
    /// </summary>
    public static class Theme
    {
        private const string Pack = "pack://application:,,,/Sop.Ui;component/Themes/";

        private static readonly Uri BaseUri;
        private static readonly Uri IconsUri;
        private static readonly Uri ScrollbarsUri;
        private static readonly Uri SharedUri;
        private static readonly Uri LightUri;
        private static readonly Uri DarkUri;

        static Theme()
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            }
            BaseUri = new Uri(Pack + "Tokens.Base.xaml");
            IconsUri = new Uri(Pack + "Icons.xaml");
            ScrollbarsUri = new Uri(Pack + "Scrollbars.xaml");
            SharedUri = new Uri(Pack + "Shared.xaml");
            LightUri = new Uri(Pack + "Tokens.Light.xaml");
            DarkUri = new Uri(Pack + "Tokens.Dark.xaml");
        }

        private static Application? _app;
        private static ResourceDictionary? _tokens;
        private static ThemeMode _mode = ThemeMode.Light;
        private static bool _rtl;

        /// <summary>Raised after <see cref="Mode"/> or <see cref="IsRtl"/> changes.</summary>
        public static event EventHandler? Changed;

        /// <summary>
        /// Merge the design system into the application: base tokens, icons,
        /// themed scrollbars and the light palette. Call once from
        /// <c>App.OnStartup</c> before any window is created.
        /// </summary>
        public static void Initialize(Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            var merged = app.Resources.MergedDictionaries;
            merged.Add(new ResourceDictionary { Source = BaseUri });
            merged.Add(new ResourceDictionary { Source = IconsUri });
            merged.Add(new ResourceDictionary { Source = ScrollbarsUri });
            merged.Add(new ResourceDictionary { Source = SharedUri });
            _tokens = new ResourceDictionary { Source = LightUri };
            merged.Add(_tokens);
            ApplyFont();
            TrackInputModality();
        }

        /// <summary>Light or dark. The operator picks it in Settings — never the OS preference.</summary>
        public static ThemeMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                if (_app != null && _tokens != null)
                {
                    var merged = _app.Resources.MergedDictionaries;
                    var index = merged.IndexOf(_tokens);
                    _tokens = new ResourceDictionary { Source = value == ThemeMode.Dark ? DarkUri : LightUri };
                    if (index >= 0) merged[index] = _tokens; else merged.Add(_tokens);
                }
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Arabic switches the UI face and mirrors the layout. Windows attached
        /// with <see cref="Attach"/> follow <see cref="FlowDirection"/>.
        /// </summary>
        public static bool IsRtl
        {
            get => _rtl;
            set
            {
                if (_rtl == value) return;
                _rtl = value;
                ApplyFont();
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        public static FlowDirection FlowDirection =>
            _rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        /// <summary>
        /// Make a window follow the theme: what it DRAWS tracks
        /// <see cref="IsRtl"/>, and it hosts the keyboard-modality flag that
        /// drives focus rings (see <see cref="KeyboardModeProperty"/>).
        ///
        /// The direction goes on the window's CONTENT, never on the window.
        /// A Window's own FlowDirection is not a layout property alone: WPF
        /// turns it into WS_EX_LAYOUTRTL, which mirrors the caption Windows
        /// draws — moving close and minimise to the other corner when the
        /// cashier switches language, and mirroring the whole window's pixels
        /// on machines where the two flips met. The caption belongs to
        /// Windows and follows the system's own language; the app's language
        /// governs the app.
        /// </summary>
        public static void Attach(Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            ApplyDirection(window);
            // Crisp text and pixel-aligned edges on every window, like the
            // gallery: Display formatting snaps glyphs to the pixel grid.
            System.Windows.Media.TextOptions.SetTextFormattingMode(window, System.Windows.Media.TextFormattingMode.Display);
            window.UseLayoutRounding = true;
            window.SnapsToDevicePixels = true;
            // Latin digits everywhere, whatever the Windows locale: WPF would
            // otherwise substitute Arabic-Indic digits on an ar-EG machine, and
            // the register's numeric columns rely on Western figures (the web
            // app pins the same with `ar-EG-u-nu-latn`).
            System.Windows.Media.NumberSubstitution.SetSubstitution(window, System.Windows.Media.NumberSubstitutionMethod.European);
            EventHandler handler = (_, __) => ApplyDirection(window);
            Changed += handler;
            // The content arrives after this on the gate windows, and is
            // swapped again when the register replaces the gate.
            var content = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(Window));
            EventHandler onContent = (_, __) => ApplyDirection(window);
            content?.AddValueChanged(window, onContent);
            window.Closed += (_, __) =>
            {
                Changed -= handler;
                content?.RemoveValueChanged(window, onContent);
            };
            SetKeyboardMode(window, _keyboardMode);
            _windows.Add(new WeakReference<Window>(window));
        }

        /// <summary>
        /// The app's direction onto what the app draws, and the window itself
        /// left in the direction Windows gave it.
        /// </summary>
        private static void ApplyDirection(Window window)
        {
            window.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            if (window.Content is FrameworkElement content) content.FlowDirection = FlowDirection;
        }

        private static void ApplyFont()
        {
            if (_app == null) return;
            var key = _rtl ? "Font.Ar" : "Font.En";
            // Font.App is looked up dynamically by every control style; pointing
            // it at the other face re-flows the whole app.
            _app.Resources["Font.App"] = _app.TryFindResource(key);
        }

        // ========== Keyboard modality (the :focus-visible equivalent) ==========

        /// <summary>
        /// True while the most recent input came from the keyboard. Focus rings
        /// show only then, the way the web's <c>:focus-visible</c> keeps a mouse
        /// click from painting a ring. Inherited, so templates read it via
        /// <c>(ui:Theme.KeyboardMode)</c> on themselves.
        /// </summary>
        public static readonly DependencyProperty KeyboardModeProperty =
            DependencyProperty.RegisterAttached(
                "KeyboardMode",
                typeof(bool),
                typeof(Theme),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetKeyboardMode(DependencyObject element) => (bool)element.GetValue(KeyboardModeProperty);
        public static void SetKeyboardMode(DependencyObject element, bool value) => element.SetValue(KeyboardModeProperty, value);

        private static bool _keyboardMode;
        private static bool _tracking;
        private static readonly System.Collections.Generic.List<WeakReference<Window>> _windows = new();

        private static void TrackInputModality()
        {
            if (_tracking) return;
            _tracking = true;
            InputManager.Current.PreProcessInput += (_, e) =>
            {
                var input = e.StagingItem.Input;
                bool? next = input switch
                {
                    KeyEventArgs k when k.RoutedEvent == Keyboard.KeyDownEvent => true,
                    MouseButtonEventArgs m when m.RoutedEvent == Mouse.MouseDownEvent => false,
                    _ => null,
                };
                if (next == null || next == _keyboardMode) return;
                _keyboardMode = next.Value;
                foreach (var reference in _windows)
                {
                    if (reference.TryGetTarget(out var window)) SetKeyboardMode(window, _keyboardMode);
                }
            };
        }
    }
}
