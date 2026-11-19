using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// A single filter chip badge displaying an active filter condition, optional prefix label,
    /// optional result count badge, and a remove button.
    /// </summary>
    public class FilterChip : ContentControl
    {
        static FilterChip()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterChip), new FrameworkPropertyMetadata(typeof(FilterChip)));
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(FilterChip), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
            nameof(Count), typeof(object), typeof(FilterChip), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
            nameof(IsSelected), typeof(bool), typeof(FilterChip), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty IsRemovableProperty = DependencyProperty.Register(
            nameof(IsRemovable), typeof(bool), typeof(FilterChip), new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(FilterChip), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
            nameof(RemoveCommand), typeof(ICommand), typeof(FilterChip), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty RemoveCommandParameterProperty = DependencyProperty.Register(
            nameof(RemoveCommandParameter), typeof(object), typeof(FilterChip), new FrameworkPropertyMetadata(null));

        public static readonly RoutedEvent RemovedEvent = EventManager.RegisterRoutedEvent(
            nameof(Removed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterChip));

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterChip));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public object? Count
        {
            get => GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsRemovable
        {
            get => (bool)GetValue(IsRemovableProperty);
            set => SetValue(IsRemovableProperty, value);
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public ICommand? RemoveCommand
        {
            get => (ICommand?)GetValue(RemoveCommandProperty);
            set => SetValue(RemoveCommandProperty, value);
        }

        public object? RemoveCommandParameter
        {
            get => GetValue(RemoveCommandParameterProperty);
            set => SetValue(RemoveCommandParameterProperty, value);
        }

        public event RoutedEventHandler Removed
        {
            add => AddHandler(RemovedEvent, value);
            remove => RemoveHandler(RemovedEvent, value);
        }

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_RemoveButton") is System.Windows.Controls.Button removeBtn)
            {
                removeBtn.Click -= OnRemoveClicked;
                removeBtn.Click += OnRemoveClicked;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.OriginalSource is DependencyObject dep && FindVisualParent<System.Windows.Controls.Button>(dep) != null)
            {
                return; // Let remove button handle its own click
            }

            IsSelected = !IsSelected;
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }

        private void OnRemoveClicked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var param = RemoveCommandParameter ?? this;
            RemoveCommand?.Execute(param);
            RaiseEvent(new RoutedEventArgs(RemovedEvent, this));
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }

    /// <summary>
    /// An ItemsControl container for rendering collections of <see cref="FilterChip"/> elements
    /// with wrapping layout, item dismissal, and optional "Clear all" action.
    /// </summary>
    public class FilterChips : ItemsControl
    {
        static FilterChips()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterChips), new FrameworkPropertyMetadata(typeof(FilterChips)));
        }

        public static readonly DependencyProperty HasClearAllProperty = DependencyProperty.Register(
            nameof(HasClearAll), typeof(bool), typeof(FilterChips), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty ClearAllTextProperty = DependencyProperty.Register(
            nameof(ClearAllText), typeof(string), typeof(FilterChips), new FrameworkPropertyMetadata("Clear all"));

        public static readonly DependencyProperty ClearAllCommandProperty = DependencyProperty.Register(
            nameof(ClearAllCommand), typeof(ICommand), typeof(FilterChips), new FrameworkPropertyMetadata(null));

        public static readonly RoutedEvent ItemRemovedEvent = EventManager.RegisterRoutedEvent(
            nameof(ItemRemoved), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<object?>), typeof(FilterChips));

        public static readonly RoutedEvent ClearAllClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ClearAllClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterChips));

        public bool HasClearAll
        {
            get => (bool)GetValue(HasClearAllProperty);
            set => SetValue(HasClearAllProperty, value);
        }

        public string ClearAllText
        {
            get => (string)GetValue(ClearAllTextProperty);
            set => SetValue(ClearAllTextProperty, value);
        }

        public ICommand? ClearAllCommand
        {
            get => (ICommand?)GetValue(ClearAllCommandProperty);
            set => SetValue(ClearAllCommandProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<object?> ItemRemoved
        {
            add => AddHandler(ItemRemovedEvent, value);
            remove => RemoveHandler(ItemRemovedEvent, value);
        }

        public event RoutedEventHandler ClearAllClicked
        {
            add => AddHandler(ClearAllClickedEvent, value);
            remove => RemoveHandler(ClearAllClickedEvent, value);
        }

        protected override bool IsItemItsOwnContainerOverride(object item) => item is FilterChip;

        protected override DependencyObject GetContainerForItemOverride() => new FilterChip();

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is FilterChip chip)
            {
                if (!(item is FilterChip) && chip.Content == null)
                {
                    chip.Content = item;
                }

                chip.Removed -= OnChipRemoved;
                chip.Removed += OnChipRemoved;
            }
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (element is FilterChip chip)
            {
                chip.Removed -= OnChipRemoved;
            }
        }

        private void OnChipRemoved(object sender, RoutedEventArgs e)
        {
            if (sender is FilterChip chip)
            {
                var item = ItemContainerGenerator.ItemFromContainer(chip) ?? chip;
                RaiseEvent(new RoutedPropertyChangedEventArgs<object?>(null, item, ItemRemovedEvent));
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_ClearAllButton") is System.Windows.Controls.Button clearBtn)
            {
                clearBtn.Click -= OnClearAllButtonClicked;
                clearBtn.Click += OnClearAllButtonClicked;
            }
        }

        private void OnClearAllButtonClicked(object sender, RoutedEventArgs e)
        {
            ClearAllCommand?.Execute(null);
            RaiseEvent(new RoutedEventArgs(ClearAllClickedEvent, this));
        }
    }
}
