using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// The overlay layer a window's modals are presented in. Place one as the
    /// LAST child of the window's root Grid so it paints over everything:
    /// <code>
    /// &lt;Grid&gt;
    ///   ...app content...
    ///   &lt;ui:ModalHost /&gt;
    /// &lt;/Grid&gt;
    /// </code>
    /// A <see cref="Modal"/> declared anywhere in that window presents itself
    /// here when opened. The host is transparent and hit-test-invisible while
    /// empty, so it costs nothing when no dialog is up.
    /// </summary>
    public class ModalHost : Grid
    {
        private static readonly DependencyProperty HostProperty = DependencyProperty.RegisterAttached(
            "Host", typeof(ModalHost), typeof(ModalHost), new FrameworkPropertyMetadata(null));

        private readonly Stack<IInputElement?> _focusBeforeOpen = new Stack<IInputElement?>();

        public ModalHost()
        {
            Background = null;
            Loaded += (_, __) =>
            {
                if (Window.GetWindow(this) is Window window) window.SetValue(HostProperty, this);
            };
        }

        internal static ModalHost? Find(DependencyObject element) =>
            Window.GetWindow(element)?.GetValue(HostProperty) as ModalHost;

        /// <summary>A dialog is up — the web's <c>dialog[open]</c> check, for shortcuts that must not fire over one.</summary>
        public bool HasOpenModal => Children.Count > 0;

        /// <summary>The host of <paramref name="element"/>'s window, if that window has one.</summary>
        public static ModalHost? For(DependencyObject element) => Find(element);

        internal void Push(ModalPanel panel)
        {
            _focusBeforeOpen.Push(Keyboard.FocusedElement);
            Children.Add(panel);
            panel.Loaded += FocusFirst;
        }

        internal void Pop(ModalPanel panel)
        {
            panel.Loaded -= FocusFirst;
            Children.Remove(panel);
            var previous = _focusBeforeOpen.Count > 0 ? _focusBeforeOpen.Pop() : null;
            if (Children.Count > 0 && Children[Children.Count - 1] is ModalPanel top)
            {
                top.FocusFirstControl();
                return;
            }
            if (previous is UIElement element && element.IsVisible && element.IsEnabled) element.Focus();
            else if (Window.GetWindow(this) is Window window) window.Focus();
        }

        private static void FocusFirst(object sender, RoutedEventArgs e) => ((ModalPanel)sender).FocusFirstControl();
    }

    /// <summary>
    /// Dialog. Declare it anywhere in a window that has a <see cref="ModalHost"/>;
    /// it is invisible in place and presents in the host while
    /// <see cref="IsOpen"/>. Esc, the backdrop and the close button raise
    /// <see cref="CloseRequested"/>; unless a handler marks that Handled the
    /// modal closes itself.
    ///
    /// Keyboard travel inside the dialog: ↑/↓ walk the controls and Enter
    /// confirms (the last enabled button in the footer — every dialog orders
    /// its footer Cancel-then-Confirm). Controls that already mean something
    /// by those keys keep their meaning: a multi-line TextBox, and Select
    /// while its list is open.
    /// </summary>
    [ContentProperty(nameof(Body))]
    public class Modal : FrameworkElement
    {
        private ModalPanel? _panel;
        private ModalHost? _host;

        public Modal()
        {
            Visibility = Visibility.Collapsed;
        }

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(Modal),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((Modal)d).OnIsOpenChanged((bool)e.NewValue)));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(object), typeof(Modal), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ModalSize), typeof(Modal), new FrameworkPropertyMetadata(ModalSize.Md));

        public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
            nameof(Body), typeof(object), typeof(Modal), new FrameworkPropertyMetadata(null));

        /// <summary>Action row, e.g. Cancel / Confirm buttons.</summary>
        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
            nameof(Footer), typeof(object), typeof(Modal), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Let the footer fill the width instead of hugging the end. A footer
        /// that carries more than buttons — a running total, an inline field —
        /// needs the whole row; the usual Cancel/Confirm pair does not.
        /// </summary>
        public static readonly DependencyProperty WideFooterProperty = DependencyProperty.Register(
            nameof(WideFooter), typeof(bool), typeof(Modal), new FrameworkPropertyMetadata(false));

        public bool WideFooter
        {
            get => (bool)GetValue(WideFooterProperty);
            set => SetValue(WideFooterProperty, value);
        }

        public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Modal));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public object? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ModalSize Size
        {
            get => (ModalSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public object? Body
        {
            get => GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }

        public object? Footer
        {
            get => GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        /// <summary>Raised on Esc, backdrop click, and the close button.</summary>
        public event RoutedEventHandler CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

        internal void RequestClose()
        {
            var args = new RoutedEventArgs(CloseRequestedEvent, this);
            RaiseEvent(args);
            if (!args.Handled) IsOpen = false;
        }

        private void OnIsOpenChanged(bool open)
        {
            if (open) Show(); else Hide();
        }

        private void Show()
        {
            if (_panel != null) return;
            if (!IsLoaded)
            {
                // Opened before the window exists (IsOpen set in XAML): wait.
                RoutedEventHandler? once = null;
                once = (_, __) =>
                {
                    Loaded -= once;
                    if (IsOpen) Show();
                };
                Loaded += once;
                return;
            }
            _host = ModalHost.Find(this) ?? throw new InvalidOperationException(
                "No ModalHost in this window. Place <ui:ModalHost /> as the last child of the window's root Grid.");

            var panel = new ModalPanel(this);
            panel.SetBinding(ModalPanel.TitleProperty, new Binding(nameof(Title)) { Source = this });
            panel.SetBinding(ModalPanel.SizeProperty, new Binding(nameof(Size)) { Source = this });
            panel.SetBinding(ModalPanel.BodyProperty, new Binding(nameof(Body)) { Source = this });
            panel.SetBinding(ModalPanel.FooterProperty, new Binding(nameof(Footer)) { Source = this });
            panel.SetBinding(ModalPanel.WideFooterProperty, new Binding(nameof(WideFooter)) { Source = this });
            panel.SetBinding(DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
            // The body was compiled in the window's name scope; keep ElementName
            // bindings and FindName working now that it renders elsewhere.
            var scope = FindNameScope(this);
            if (scope != null) NameScope.SetNameScope(panel, scope);

            _panel = panel;
            _host.Push(panel);
        }

        private void Hide()
        {
            if (_panel == null || _host == null) return;
            var panel = _panel;
            _panel = null;
            _host.Pop(panel);
            _host = null;
        }

        private static INameScope? FindNameScope(DependencyObject start)
        {
            DependencyObject? node = start;
            while (node != null)
            {
                if (NameScope.GetNameScope(node) is INameScope scope) return scope;
                node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node);
            }
            return null;
        }
    }

    /// <summary>The presented surface: backdrop + floating panel. Created by <see cref="Modal"/>.</summary>
    public class ModalPanel : Control
    {
        static ModalPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ModalPanel), new FrameworkPropertyMetadata(typeof(ModalPanel)));
            FocusableProperty.OverrideMetadata(typeof(ModalPanel), new FrameworkPropertyMetadata(false));
        }

        public ModalPanel(Modal owner)
        {
            Owner = owner;
            KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
        }

        public Modal Owner { get; }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(object), typeof(ModalPanel), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ModalSize), typeof(ModalPanel), new FrameworkPropertyMetadata(ModalSize.Md));

        public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
            nameof(Body), typeof(object), typeof(ModalPanel), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
            nameof(Footer), typeof(object), typeof(ModalPanel), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty WideFooterProperty = DependencyProperty.Register(
            nameof(WideFooter), typeof(bool), typeof(ModalPanel), new FrameworkPropertyMetadata(false));

        public bool WideFooter
        {
            get => (bool)GetValue(WideFooterProperty);
            set => SetValue(WideFooterProperty, value);
        }

        public object? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ModalSize Size
        {
            get => (ModalSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public object? Body
        {
            get => GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }

        public object? Footer
        {
            get => GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        private FrameworkElement? _backdrop;
        private FrameworkElement? _footer;
        private FrameworkElement? _body;

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            var panel = GetTemplateChild("Panel") as FrameworkElement;
            if (panel != null)
            {
                var pos = e.GetPosition(panel);
                // If clicked outside the dialog card (Panel), it's an overlay / backdrop dismiss!
                if (pos.X < 0 || pos.Y < 0 || pos.X > panel.ActualWidth || pos.Y > panel.ActualHeight)
                {
                    e.Handled = true;
                    Owner.RequestClose();
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _backdrop = GetTemplateChild("PART_Backdrop") as FrameworkElement;
            _footer = GetTemplateChild("PART_Footer") as FrameworkElement;
            _body = GetTemplateChild("PART_Body") as FrameworkElement;
            if (_backdrop != null)
            {
                _backdrop.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    Owner.RequestClose();
                };
            }
            if (GetTemplateChild("PART_Close") is ButtonBase close)
            {
                close.Click += (_, __) => Owner.RequestClose();
                close.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    Owner.RequestClose();
                };
            }
        }

        internal void FocusFirstControl()
        {
            var first = Focusables().FirstOrDefault();
            if (first != null) first.Focus();
            else if (GetTemplateChild("PART_Close") is UIElement close) close.Focus();
        }

        /// <summary>The dialog's focusable controls in tree order, skipping hidden/disabled ones.</summary>
        private List<UIElement> Focusables()
        {
            var list = new List<UIElement>();
            if (_body != null) Collect(_body, list);
            if (_footer != null) Collect(_footer, list);
            return list;
        }

        private static void Collect(DependencyObject node, List<UIElement> into)
        {
            if (node is UIElement ui)
            {
                if (!ui.IsVisible) return;
                if (ui.Focusable && ui.IsEnabled && KeyboardNavigation.GetIsTabStop(ui))
                {
                    into.Add(ui);
                    // A focusable control's own template parts are not separate stops.
                    if (ui is not Panel && ui is not ContentPresenter && ui is not ItemsPresenter) return;
                }
            }
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) Collect(VisualTreeHelper.GetChild(node, i), into);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            // A control that already claimed the key wins (Select's open list).
            if (e.Handled) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            var focused = Keyboard.FocusedElement as DependencyObject;
            var multiline = focused is TextBox tb && tb.AcceptsReturn;

            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = true;
                    Owner.RequestClose();
                    return;
                case Key.Down:
                case Key.Up:
                    if (multiline || focused is ComboBox) return;
                    var items = Focusables();
                    if (items.Count == 0) return;
                    var at = focused is UIElement f ? items.IndexOf(f) : -1;
                    var step = e.Key == Key.Down ? 1 : -1;
                    var next = at < 0 ? (step > 0 ? 0 : items.Count - 1) : (at + step + items.Count) % items.Count;
                    e.Handled = true;
                    items[next].Focus();
                    return;
                case Key.Enter:
                    if (multiline) return;
                    // A footer button (Cancel, or the primary itself) activates natively.
                    if (focused is DependencyObject d && _footer != null && IsInside(d, _footer)) return;
                    var action = PrimaryAction();
                    if (action == null) return;
                    e.Handled = true;
                    Invoke(action);
                    return;
            }
        }

        /// <summary>
        /// The confirm action: the last button in the footer. Being disabled
        /// then means Enter does NOTHING — skipping to the next enabled button
        /// would land on Cancel and discard an incomplete form.
        /// </summary>
        private System.Windows.Controls.Button? PrimaryAction()
        {
            if (_footer == null) return null;
            var buttons = new List<System.Windows.Controls.Button>();
            CollectButtons(_footer, buttons);
            var last = buttons.LastOrDefault();
            return last != null && last.IsEnabled ? last : null;
        }

        private static void CollectButtons(DependencyObject node, List<System.Windows.Controls.Button> into)
        {
            if (node is System.Windows.Controls.Button b) { into.Add(b); return; }
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) CollectButtons(VisualTreeHelper.GetChild(node, i), into);
        }

        private static readonly System.Reflection.MethodInfo? OnClickMethod =
            typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                "OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);

        /// <summary>
        /// Click the button now, on this call stack: the automation peer's
        /// Invoke is asynchronous, and Enter must confirm before the key event
        /// finishes bubbling. ButtonBase.OnClick raises Click and runs Command.
        /// </summary>
        private static void Invoke(System.Windows.Controls.Button button)
        {
            if (OnClickMethod != null)
            {
                OnClickMethod.Invoke(button, null);
                return;
            }
            if (UIElementAutomationPeer.CreatePeerForElement(button)?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
                invoke.Invoke();
        }

        private static bool IsInside(DependencyObject node, DependencyObject ancestor)
        {
            DependencyObject? current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
