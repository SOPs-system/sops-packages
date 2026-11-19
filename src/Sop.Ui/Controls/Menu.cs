using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// The one dropdown popover in the app: a floating panel of
    /// <see cref="MenuItem"/>s with an optional caption. Select and sort menus
    /// are both built on it, so every dropdown looks and behaves the same.
    /// <code>
    /// &lt;ui:Menu x:Name="Sort" PlacementTarget="{Binding ElementName=SortButton}" Align="End" Label="Sort by"&gt;
    ///   &lt;StackPanel&gt;&lt;ui:MenuItem Selected="True"&gt;Name&lt;/ui:MenuItem&gt;…&lt;/StackPanel&gt;
    /// &lt;/ui:Menu&gt;
    /// </code>
    /// A Popup is a top-level surface, so a scrolling modal body can never
    /// clip the panel and it overlays dialogs by construction.
    /// </summary>
    [ContentProperty(nameof(Content))]
    public class Menu : Popup
    {
        private readonly MenuPanel _panel;

        public Menu()
        {
            AllowsTransparency = true;
            StaysOpen = false;
            Placement = PlacementMode.Custom;
            CustomPopupPlacementCallback = Place;
            PopupAnimation = PopupAnimation.None;
            Focusable = false;

            _panel = new MenuPanel();
            _panel.SetBinding(ContentControl.ContentProperty, new Binding(nameof(Content)) { Source = this });
            _panel.SetBinding(MenuPanel.LabelProperty, new Binding(nameof(Label)) { Source = this });
            _panel.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemClick));
            _panel.KeyDown += OnPanelKeyDown;
            // Popups are their own surface: theme flags do not inherit through
            // the popup boundary, so mirror the target's direction and modality.
            Child = _panel;
            // A popup outlives its window unless something says otherwise.
            PopupGuard.SetFollowsOwner(this, true);
            Opened += OnOpened;
        }


        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(object), typeof(Menu), new FrameworkPropertyMetadata(null));

        /// <summary>Small uppercase caption above the items, e.g. "Sort by".</summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(object), typeof(Menu), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(
            nameof(Align), typeof(MenuAlign), typeof(Menu), new FrameworkPropertyMetadata(MenuAlign.Stretch));

        /// <summary>
        /// Move keyboard focus into the panel on open (default). False when the
        /// trigger owns focus — a combobox typing into its own field — and the
        /// caller drives the highlight through <see cref="MenuItem.Active"/>.
        /// </summary>
        public static readonly DependencyProperty ManageFocusProperty = DependencyProperty.Register(
            nameof(ManageFocus), typeof(bool), typeof(Menu), new FrameworkPropertyMetadata(true));

        /// <summary>Close after an item is chosen (default).</summary>
        public static readonly DependencyProperty CloseOnSelectProperty = DependencyProperty.Register(
            nameof(CloseOnSelect), typeof(bool), typeof(Menu), new FrameworkPropertyMetadata(true));

        public object? Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public object? Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public MenuAlign Align
        {
            get => (MenuAlign)GetValue(AlignProperty);
            set => SetValue(AlignProperty, value);
        }

        public bool ManageFocus
        {
            get => (bool)GetValue(ManageFocusProperty);
            set => SetValue(ManageFocusProperty, value);
        }

        public bool CloseOnSelect
        {
            get => (bool)GetValue(CloseOnSelectProperty);
            set => SetValue(CloseOnSelectProperty, value);
        }

        /// <summary>The panel element, for callers that need to scroll a row into view.</summary>
        public MenuPanel Panel => _panel;

        private double Bleed => _panel.TryFindResource("Size.MenuShadowBleed") is double d ? d : 0;
        private double Gap => _panel.TryFindResource("Size.MenuGap") is double d ? d : 6;

        private void OnOpened(object? sender, System.EventArgs e)
        {
            // Custom placement owns the offset (Calibrate sets it per open);
            // a context menu opened at a point keeps the offset it was given.
            if (Placement == PlacementMode.Custom) HorizontalOffset = 0;
            if (PlacementTarget is FrameworkElement target)
            {
                _panel.FlowDirection = target.FlowDirection;
                Theme.SetKeyboardMode(_panel, Theme.GetKeyboardMode(target));
                var stretch = Align == MenuAlign.Stretch;
                _panel.Width = stretch ? target.ActualWidth + 2 * Bleed : double.NaN;
                _panel.MinWidth = stretch ? target.ActualWidth + 2 * Bleed : 0;
                if (Placement == PlacementMode.Custom)
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(() => Calibrate(target)));
            }
            if (ManageFocus)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
                {
                    var items = Items();
                    (items.FirstOrDefault(i => i.Selected) ?? items.FirstOrDefault())?.Focus();
                }));
            }
        }

        /// <summary>Below the trigger, aligned to its start (or end); above only when it does not fit.</summary>
        private CustomPopupPlacement[] Place(Size popupSize, Size targetSize, Point offset)
        {
            var bleed = Bleed;
            var gap = Gap;
            var visibleWidth = popupSize.Width - 2 * bleed;
            // x is the popup's left edge relative to the target's left edge, as
            // laid out left-to-right: Start/Stretch hug the start edge, End the end edge.
            var x = Align == MenuAlign.End ? targetSize.Width - visibleWidth - bleed : -bleed;
            if (FlowDirection == FlowDirection.RightToLeft)
            {
                // Under RTL, WPF measures x from the target's RIGHT edge and the
                // popup still extends rightwards from it. Mirror the layout:
                // the popup's left edge becomes targetWidth - x - popupWidth,
                // and relative to the right edge that is -(x + popupWidth).
                x = -(x + popupSize.Width);
            }
            var below = new CustomPopupPlacement(new Point(x, targetSize.Height + gap - bleed), PopupPrimaryAxis.Vertical);
            var above = new CustomPopupPlacement(new Point(x, -popupSize.Height - gap + bleed), PopupPrimaryAxis.Vertical);
            return new[] { below, above };
        }

        /// <summary>
        /// WPF lands a custom-placed popup a few device pixels off the point the
        /// callback asked for (it positions against the target's window-relative
        /// bounds, which include a rounding step). Measure where the panel
        /// actually is against where its visible card should be, and nudge.
        /// </summary>
        private void Calibrate(FrameworkElement target)
        {
            if (!IsOpen || PresentationSource.FromVisual(target)?.CompositionTarget is not { } compositionTarget) return;
            if (PresentationSource.FromVisual(_panel) == null) return;
            var scale = compositionTarget.TransformToDevice.M11;
            var bleed = Bleed;
            var rtl = FlowDirection == FlowDirection.RightToLeft;
            var targetOrigin = target.PointToScreen(new Point(0, 0)); // left edge in LTR, right edge in RTL
            var panelOrigin = _panel.PointToScreen(new Point(0, 0));
            var visibleWidth = _panel.ActualWidth - 2 * bleed;
            // Where the panel origin should be, in DIPs relative to the target
            // origin. LTR origins are left edges: the card's left edge sits on
            // the target's left (Start/Stretch) or its right edge on the
            // target's right (End). RTL origins are right edges, mirrored.
            var end = Align == MenuAlign.End;
            double wanted = rtl
                ? (end ? -target.ActualWidth + visibleWidth + bleed : bleed)
                : (end ? target.ActualWidth - visibleWidth - bleed : -bleed);
            var actual = (panelOrigin.X - targetOrigin.X) / scale;
            var delta = actual - wanted;
            if (System.Math.Abs(delta) < 0.5 || System.Math.Abs(delta) > 12) return;
            HorizontalOffset = rtl ? delta : -delta;
        }

        private void OnItemClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is MenuItem || (e.Source is MenuItem))
            {
                if (CloseOnSelect) IsOpen = false;
            }
        }

        private void OnPanelKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Handled, so a surrounding modal does not take Esc as its own
                // close request — the menu closes first, the dialog stays open.
                e.Handled = true;
                IsOpen = false;
                return;
            }
            if (e.Key == Key.Tab)
            {
                IsOpen = false;
                return;
            }
            if (!ManageFocus) return;
            if (e.Key != Key.Down && e.Key != Key.Up && e.Key != Key.Home && e.Key != Key.End) return;
            var items = Items();
            if (items.Count == 0) return;
            e.Handled = true;
            var current = Keyboard.FocusedElement is MenuItem focused ? items.IndexOf(focused) : -1;
            var next = e.Key switch
            {
                Key.Home => 0,
                Key.End => items.Count - 1,
                Key.Down => (current + 1) % items.Count,
                _ => (current - 1 + items.Count) % items.Count,
            };
            items[next].Focus();
        }

        private static bool _warmed;

        /// <summary>Rows the warm-up builds — one page, as a real list shows.</summary>
        private const int PrewarmRows = 10;

        /// <summary>
        /// Build one menu off-screen while the app is idle, so the first
        /// dropdown a user opens does not pay for it.
        ///
        /// A popup is its own top-level window: the first one in a process
        /// carries the cost of creating that window, instantiating the panel
        /// and row templates and jitting the whole path — around half a second
        /// on a modest till, and it lands on whichever dropdown is clicked
        /// first. Paid here instead, against a menu nobody sees.
        /// </summary>
        public static void Prewarm(FrameworkElement owner)
        {
            if (_warmed || owner == null) return;
            _warmed = true;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new System.Action(() =>
            {
                var menu = new Menu
                {
                    // Over the window but drawn at zero opacity: a popup pushed
                    // off the screen is never rendered, and rendering is half of
                    // what we are here to pay for. Unlike a real menu it must
                    // not take the mouse capture that would swallow the user's
                    // next click, nor react to one.
                    StaysOpen = true,
                    Placement = PlacementMode.Relative,
                    PlacementTarget = owner,
                    IsHitTestVisible = false,
                };
                // A whole picker, not a bare panel: the rows of a dropdown come
                // from a template against bound data, and that path costs as
                // much on its first run as the popup window itself.
                var probe = new Select
                {
                    Width = 200,
                    Options = Enumerable.Range(0, PrewarmRows)
                        .Select(i => new SelectOption("warm" + i, "…"))
                        .ToList(),
                };
                menu.Content = probe;
                menu.Panel.Opacity = 0;
                PopupGuard.SetFollowsOwner(menu, false);
                try
                {
                    menu.IsOpen = true;
                    menu.Panel.UpdateLayout();
                    probe.ApplyTemplate();
                    if (probe.Template?.FindName("PART_Menu", probe) is Menu inner)
                    {
                        inner.StaysOpen = true;
                        inner.IsHitTestVisible = false;
                        inner.Panel.Opacity = 0;
                        inner.IsOpen = true;
                        inner.Panel.UpdateLayout();
                        owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                            new System.Action(() => inner.IsOpen = false));
                    }
                    // Closed after a frame has actually been drawn — closing in
                    // this same callback would skip the render pass.
                    owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                        new System.Action(() => menu.IsOpen = false));
                }
                catch (System.Exception)
                {
                    // Warming is an optimisation; never let it take the app down.
                    menu.IsOpen = false;
                }
            }));
        }

        public List<MenuItem> Items()
        {
            var list = new List<MenuItem>();
            Collect(_panel, list);
            return list;
        }

        private static void Collect(DependencyObject node, List<MenuItem> into)
        {
            if (node is MenuItem item)
            {
                if (item.IsEnabled && item.IsVisible) into.Add(item);
                return;
            }
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) Collect(VisualTreeHelper.GetChild(node, i), into);
        }
    }

    /// <summary>The floating card a <see cref="Menu"/> renders its items in.</summary>
    public class MenuPanel : ContentControl
    {
        static MenuPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MenuPanel), new FrameworkPropertyMetadata(typeof(MenuPanel)));
            FocusableProperty.OverrideMetadata(typeof(MenuPanel), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(object), typeof(MenuPanel), new FrameworkPropertyMetadata(null));

        public object? Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public MenuPanel()
        {
            Loaded += (_, __) => PlayEntrance();
        }

        /// <summary>
        /// The panel's own scroller, for a list that pages as it is scrolled.
        /// Null until the template applies.
        /// </summary>
        public ScrollViewer? Scroller { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Scroller = GetTemplateChild("PART_Scroll") as ScrollViewer;
        }

        /// <summary>
        /// Fade and settle in (100ms), the theme's `animate-in` at popover
        /// scale. Driven from code: a Loaded storyboard in the template cannot
        /// resolve its target once the panel lives in a popup's name scope.
        /// </summary>
        private void PlayEntrance()
        {
            if (GetTemplateChild("Card") is not FrameworkElement card) return;
            var duration = new Duration(System.TimeSpan.FromMilliseconds(100));
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            card.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration));
            if (card.RenderTransform is TransformGroup group && group.Children.Count == 2
                && group.Children[0] is ScaleTransform scale && group.Children[1] is TranslateTransform slide)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(0.98, 1, duration) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(0.98, 1, duration) { EasingFunction = ease });
                slide.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(-2, 0, duration));
            }
        }
    }

    /// <summary>One row of a <see cref="Menu"/>.</summary>
    public class MenuItem : System.Windows.Controls.Button
    {
        static MenuItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MenuItem), new FrameworkPropertyMetadata(typeof(MenuItem)));
        }

        public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(
            nameof(Selected), typeof(bool), typeof(MenuItem), new FrameworkPropertyMetadata(false));

        /// <summary>Keyboard highlight driven by the caller (see <see cref="Menu.ManageFocus"/>).</summary>
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(
            nameof(Active), typeof(bool), typeof(MenuItem), new FrameworkPropertyMetadata(false));

        /// <summary>Reserve and show the leading checkmark column (default true).</summary>
        public static readonly DependencyProperty CheckProperty = DependencyProperty.Register(
            nameof(Check), typeof(bool), typeof(MenuItem), new FrameworkPropertyMetadata(true));

        /// <summary>Trailing slot, e.g. a badge or a shortcut hint.</summary>
        public static readonly DependencyProperty TrailingProperty = DependencyProperty.Register(
            nameof(Trailing), typeof(object), typeof(MenuItem), new FrameworkPropertyMetadata(null));

        public bool Selected
        {
            get => (bool)GetValue(SelectedProperty);
            set => SetValue(SelectedProperty, value);
        }

        public bool Active
        {
            get => (bool)GetValue(ActiveProperty);
            set => SetValue(ActiveProperty, value);
        }

        public bool Check
        {
            get => (bool)GetValue(CheckProperty);
            set => SetValue(CheckProperty, value);
        }

        public object? Trailing
        {
            get => GetValue(TrailingProperty);
            set => SetValue(TrailingProperty, value);
        }
    }

    /// <summary>A thin rule between groups of menu items.</summary>
    public class MenuSeparator : Control
    {
        static MenuSeparator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MenuSeparator), new FrameworkPropertyMetadata(typeof(MenuSeparator)));
            FocusableProperty.OverrideMetadata(typeof(MenuSeparator), new FrameworkPropertyMetadata(false));
        }
    }

    /// <summary>
    /// A <see cref="Menu"/> opened at the pointer instead of under a trigger,
    /// so placement, keyboard travel and closing all come from the one
    /// dropdown implementation.
    /// </summary>
    public class ContextMenu : Menu
    {
        public ContextMenu()
        {
            Align = MenuAlign.Start;
        }

        /// <summary>Open at <paramref name="point"/>, in <paramref name="relativeTo"/>'s coordinates.</summary>
        public void OpenAt(UIElement relativeTo, Point point)
        {
            PlacementTarget = relativeTo;
            Placement = PlacementMode.RelativePoint;
            var bleed = Panel.TryFindResource("Size.MenuShadowBleed") is double d ? d : 0;
            HorizontalOffset = point.X - bleed;
            VerticalOffset = point.Y - bleed;
            IsOpen = true;
        }
    }
}
