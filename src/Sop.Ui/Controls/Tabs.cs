using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sop.Ui.Controls
{
    public class TabItem
    {
        public TabItem() { }

        public TabItem(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Segmented tab strip. Items are <see cref="TabItem"/>s (Key + Label);
    /// <see cref="SelectedKey"/> is the controlled value. <see cref="Block"/>
    /// stretches the strip and gives every tab equal width.
    /// </summary>
    public class Tabs : ItemsControl
    {
        static Tabs()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Tabs), new FrameworkPropertyMetadata(typeof(Tabs)));
            FocusableProperty.OverrideMetadata(typeof(Tabs), new FrameworkPropertyMetadata(false));
        }

        public Tabs()
        {
            ItemsSource = new ObservableCollection<TabItem>();
        }

        public static readonly DependencyProperty SelectedKeyProperty = DependencyProperty.Register(
            nameof(SelectedKey), typeof(string), typeof(Tabs),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Tabs)d).SyncChecked()));

        public static readonly DependencyProperty BlockProperty = DependencyProperty.Register(
            nameof(Block), typeof(bool), typeof(Tabs), new FrameworkPropertyMetadata(false));

        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Tabs));

        public string? SelectedKey
        {
            get => (string?)GetValue(SelectedKeyProperty);
            set => SetValue(SelectedKeyProperty, value);
        }

        public bool Block
        {
            get => (bool)GetValue(BlockProperty);
            set => SetValue(BlockProperty, value);
        }

        public event RoutedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        /// <summary>Convenience for XAML-declared tabs when no ItemsSource is bound.</summary>
        public IList<TabItem>? TabItems => ItemsSource as IList<TabItem>;

        protected override DependencyObject GetContainerForItemOverride() => new TabButton();

        protected override bool IsItemItsOwnContainerOverride(object item) => item is TabButton;

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is TabButton button && item is TabItem tab)
            {
                button.Key = tab.Key;
                button.Content = tab.Label;
                button.IsChecked = tab.Key == SelectedKey;
                button.Checked += OnTabChecked;
            }
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            if (element is TabButton button) button.Checked -= OnTabChecked;
            base.ClearContainerForItemOverride(element, item);
        }

        private bool _syncing;

        private void OnTabChecked(object sender, RoutedEventArgs e)
        {
            if (_syncing || sender is not TabButton button) return;
            if (SelectedKey == button.Key) return;
            SelectedKey = button.Key;
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
        }

        private void SyncChecked()
        {
            _syncing = true;
            try
            {
                foreach (var item in Items)
                {
                    if (ItemContainerGenerator.ContainerFromItem(item) is TabButton button)
                        button.IsChecked = button.Key == SelectedKey;
                }
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    /// <summary>One segment of <see cref="Tabs"/>. Radio semantics: cannot be unchecked by clicking.</summary>
    public class TabButton : ToggleButton
    {
        static TabButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TabButton), new FrameworkPropertyMetadata(typeof(TabButton)));
        }

        public string Key { get; set; } = string.Empty;

        protected override void OnToggle()
        {
            if (IsChecked != true) IsChecked = true;
        }
    }
}
