using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// SplitButton control combining a primary action button and an adjacent dropdown chevron toggle button
    /// for secondary action menus.
    /// </summary>
    public class SplitButton : ContentControl
    {
        static SplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
        }

        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant), typeof(ButtonVariant), typeof(SplitButton), new FrameworkPropertyMetadata(ButtonVariant.Primary));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(SplitButton), new FrameworkPropertyMetadata(ControlSize.Md));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command), typeof(ICommand), typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(
            nameof(CommandTarget), typeof(IInputElement), typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(SplitButton), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

        public static readonly DependencyProperty DropDownContentProperty = DependencyProperty.Register(
            nameof(DropDownContent), typeof(object), typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty DropDownPlacementProperty = DependencyProperty.Register(
            nameof(DropDownPlacement), typeof(PlacementMode), typeof(SplitButton), new FrameworkPropertyMetadata(PlacementMode.Bottom));

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SplitButton));

        public static readonly RoutedEvent DropDownOpenedEvent = EventManager.RegisterRoutedEvent(
            nameof(DropDownOpened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SplitButton));

        public static readonly RoutedEvent DropDownClosedEvent = EventManager.RegisterRoutedEvent(
            nameof(DropDownClosed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SplitButton));

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

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public IInputElement? CommandTarget
        {
            get => (IInputElement?)GetValue(CommandTargetProperty);
            set => SetValue(CommandTargetProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public object? DropDownContent
        {
            get => GetValue(DropDownContentProperty);
            set => SetValue(DropDownContentProperty, value);
        }

        public PlacementMode DropDownPlacement
        {
            get => (PlacementMode)GetValue(DropDownPlacementProperty);
            set => SetValue(DropDownPlacementProperty, value);
        }

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public event RoutedEventHandler DropDownOpened
        {
            add => AddHandler(DropDownOpenedEvent, value);
            remove => RemoveHandler(DropDownOpenedEvent, value);
        }

        public event RoutedEventHandler DropDownClosed
        {
            add => AddHandler(DropDownClosedEvent, value);
            remove => RemoveHandler(DropDownClosedEvent, value);
        }

        private System.Windows.Controls.Button? _primaryBtn;
        private ToggleButton? _dropDownBtn;
        private Popup? _popup;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_primaryBtn != null)
            {
                _primaryBtn.Click -= OnPrimaryButtonClicked;
            }

            _primaryBtn = GetTemplateChild("PART_PrimaryButton") as System.Windows.Controls.Button;
            _dropDownBtn = GetTemplateChild("PART_DropDownButton") as ToggleButton;
            _popup = GetTemplateChild("PART_Popup") as Popup;

            if (_primaryBtn != null)
            {
                _primaryBtn.Click += OnPrimaryButtonClicked;
            }

            if (_popup != null)
            {
                _popup.Closed -= OnPopupClosed;
                _popup.Closed += OnPopupClosed;
            }
        }

        private void OnPrimaryButtonClicked(object sender, RoutedEventArgs e)
        {
            if (IsOpen) IsOpen = false;

            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }

            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SplitButton btn)
            {
                var isOpen = (bool)e.NewValue;
                if (isOpen)
                {
                    btn.RaiseEvent(new RoutedEventArgs(DropDownOpenedEvent, btn));
                }
                else
                {
                    btn.RaiseEvent(new RoutedEventArgs(DropDownClosedEvent, btn));
                }
            }
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            if (IsOpen) IsOpen = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                IsOpen = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && IsOpen)
            {
                IsOpen = false;
                e.Handled = true;
            }
        }
    }
}
