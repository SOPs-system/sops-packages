using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Surface container. <see cref="Interactive"/> gives it hover/press
    /// affordance, keyboard focus and a <see cref="Click"/> event — use for
    /// pressable tiles like the product grid.
    /// </summary>
    public class Card : ContentControl
    {
        static Card()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
            FocusableProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty InteractiveProperty = DependencyProperty.Register(
            nameof(Interactive), typeof(bool), typeof(Card),
            new FrameworkPropertyMetadata(false, (d, e) => ((Card)d).Focusable = (bool)e.NewValue));

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Card));

        /// <summary>
        /// Clip the content to the card's rounded corners — for a card that IS
        /// its content (a table filling it edge to edge, with Padding="0").
        /// </summary>
        public static readonly DependencyProperty ClipContentProperty = DependencyProperty.Register(
            nameof(ClipContent), typeof(bool), typeof(Card),
            new FrameworkPropertyMetadata(false, (d, _) => ((Card)d).UpdateClip()));

        public bool Interactive
        {
            get => (bool)GetValue(InteractiveProperty);
            set => SetValue(InteractiveProperty, value);
        }

        public bool ClipContent
        {
            get => (bool)GetValue(ClipContentProperty);
            set => SetValue(ClipContentProperty, value);
        }

        private FrameworkElement? _contentHost;

        public override void OnApplyTemplate()
        {
            if (_contentHost != null) _contentHost.SizeChanged -= OnHostSizeChanged;
            base.OnApplyTemplate();
            _contentHost = GetTemplateChild("PART_ContentHost") as FrameworkElement;
            if (_contentHost != null) _contentHost.SizeChanged += OnHostSizeChanged;
            UpdateClip();
        }

        private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => UpdateClip();

        private void UpdateClip()
        {
            if (_contentHost == null) return;
            if (!ClipContent)
            {
                _contentHost.Clip = null;
                return;
            }
            var radius = TryFindResource("Radius.Lg") is CornerRadius r ? r.TopLeft : 0;
            _contentHost.Clip = new System.Windows.Media.RectangleGeometry(
                new Rect(0, 0, _contentHost.ActualWidth, _contentHost.ActualHeight), radius, radius);
        }

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        private bool _pressed;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!Interactive) return;
            _pressed = true;
            Focus();
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!Interactive || !_pressed) return;
            _pressed = false;
            ReleaseMouseCapture();
            if (IsMouseOver) RaiseEvent(new RoutedEventArgs(ClickEvent, this));
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Interactive || e.Handled) return;
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                RaiseEvent(new RoutedEventArgs(ClickEvent, this));
                e.Handled = true;
            }
        }
    }

    /// <summary>Title / subtitle row with an optional trailing actions slot.</summary>
    public class CardHeader : Control
    {
        static CardHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CardHeader), new FrameworkPropertyMetadata(typeof(CardHeader)));
            FocusableProperty.OverrideMetadata(typeof(CardHeader), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(object), typeof(CardHeader), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
            nameof(Subtitle), typeof(object), typeof(CardHeader), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
            nameof(Actions), typeof(object), typeof(CardHeader), new FrameworkPropertyMetadata(null));

        public object? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object? Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public object? Actions
        {
            get => GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }
    }

    /// <summary>End-aligned action row under a top rule.</summary>
    public class CardFooter : ContentControl
    {
        static CardFooter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CardFooter), new FrameworkPropertyMetadata(typeof(CardFooter)));
            FocusableProperty.OverrideMetadata(typeof(CardFooter), new FrameworkPropertyMetadata(false));
        }
    }
}
