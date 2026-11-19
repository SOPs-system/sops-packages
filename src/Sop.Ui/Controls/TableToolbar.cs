using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Standardized action and filter toolbar for data tables and catalog grids.
    /// Provides search input, filter trigger with badge, column visibility button,
    /// export action, item count display, and arbitrary custom action slots.
    /// </summary>
    public class TableToolbar : Control
    {
        static TableToolbar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TableToolbar), new FrameworkPropertyMetadata(typeof(TableToolbar)));
        }

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(TableToolbar),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        public static readonly DependencyProperty SearchPlaceholderProperty =
            DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(TableToolbar),
                new PropertyMetadata("Search..."));

        public static readonly DependencyProperty ItemCountTextProperty =
            DependencyProperty.Register(nameof(ItemCountText), typeof(string), typeof(TableToolbar),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ShowFilterProperty =
            DependencyProperty.Register(nameof(ShowFilter), typeof(bool), typeof(TableToolbar),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowSortProperty =
            DependencyProperty.Register(nameof(ShowSort), typeof(bool), typeof(TableToolbar),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowColumnsProperty =
            DependencyProperty.Register(nameof(ShowColumns), typeof(bool), typeof(TableToolbar),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowExportProperty =
            DependencyProperty.Register(nameof(ShowExport), typeof(bool), typeof(TableToolbar),
                new PropertyMetadata(true));

        public static readonly DependencyProperty FilterBadgeCountProperty =
            DependencyProperty.Register(nameof(FilterBadgeCount), typeof(int), typeof(TableToolbar),
                new PropertyMetadata(0));

        public static readonly DependencyProperty ActiveSortLabelProperty =
            DependencyProperty.Register(nameof(ActiveSortLabel), typeof(string), typeof(TableToolbar),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionsSlotProperty =
            DependencyProperty.Register(nameof(ActionsSlot), typeof(object), typeof(TableToolbar),
                new PropertyMetadata(null));

        public static readonly DependencyProperty FilterChipsSlotProperty =
            DependencyProperty.Register(nameof(FilterChipsSlot), typeof(object), typeof(TableToolbar),
                new PropertyMetadata(null));

        public static readonly RoutedEvent SearchTextChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SearchTextChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<string>), typeof(TableToolbar));

        public static readonly RoutedEvent FilterClickEvent =
            EventManager.RegisterRoutedEvent(nameof(FilterClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        public static readonly RoutedEvent SortClickEvent =
            EventManager.RegisterRoutedEvent(nameof(SortClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        public static readonly RoutedEvent ColumnsClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ColumnsClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        public static readonly RoutedEvent ExportClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ExportClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        public static readonly RoutedEvent RefreshClickEvent =
            EventManager.RegisterRoutedEvent(nameof(RefreshClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        public static readonly RoutedEvent SearchSubmittedEvent =
            EventManager.RegisterRoutedEvent(nameof(SearchSubmitted), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TableToolbar));

        private TextBox? _searchBox;

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string SearchPlaceholder
        {
            get => (string)GetValue(SearchPlaceholderProperty);
            set => SetValue(SearchPlaceholderProperty, value);
        }

        public string ItemCountText
        {
            get => (string)GetValue(ItemCountTextProperty);
            set => SetValue(ItemCountTextProperty, value);
        }

        public bool ShowFilter
        {
            get => (bool)GetValue(ShowFilterProperty);
            set => SetValue(ShowFilterProperty, value);
        }

        public bool ShowSort
        {
            get => (bool)GetValue(ShowSortProperty);
            set => SetValue(ShowSortProperty, value);
        }

        public bool ShowColumns
        {
            get => (bool)GetValue(ShowColumnsProperty);
            set => SetValue(ShowColumnsProperty, value);
        }

        public bool ShowExport
        {
            get => (bool)GetValue(ShowExportProperty);
            set => SetValue(ShowExportProperty, value);
        }

        public int FilterBadgeCount
        {
            get => (int)GetValue(FilterBadgeCountProperty);
            set => SetValue(FilterBadgeCountProperty, value);
        }

        public string ActiveSortLabel
        {
            get => (string)GetValue(ActiveSortLabelProperty);
            set => SetValue(ActiveSortLabelProperty, value);
        }

        public object? ActionsSlot
        {
            get => GetValue(ActionsSlotProperty);
            set => SetValue(ActionsSlotProperty, value);
        }

        public object? FilterChipsSlot
        {
            get => GetValue(FilterChipsSlotProperty);
            set => SetValue(FilterChipsSlotProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<string> SearchTextChanged
        {
            add => AddHandler(SearchTextChangedEvent, value);
            remove => RemoveHandler(SearchTextChangedEvent, value);
        }

        public event RoutedEventHandler FilterClick
        {
            add => AddHandler(FilterClickEvent, value);
            remove => RemoveHandler(FilterClickEvent, value);
        }

        public event RoutedEventHandler SortClick
        {
            add => AddHandler(SortClickEvent, value);
            remove => RemoveHandler(SortClickEvent, value);
        }

        public event RoutedEventHandler ColumnsClick
        {
            add => AddHandler(ColumnsClickEvent, value);
            remove => RemoveHandler(ColumnsClickEvent, value);
        }

        public event RoutedEventHandler ExportClick
        {
            add => AddHandler(ExportClickEvent, value);
            remove => RemoveHandler(ExportClickEvent, value);
        }

        public event RoutedEventHandler RefreshClick
        {
            add => AddHandler(RefreshClickEvent, value);
            remove => RemoveHandler(RefreshClickEvent, value);
        }

        public event RoutedEventHandler SearchSubmitted
        {
            add => AddHandler(SearchSubmittedEvent, value);
            remove => RemoveHandler(SearchSubmittedEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_searchBox != null)
            {
                _searchBox.KeyDown -= OnSearchBoxKeyDown;
            }
            _searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
            if (_searchBox != null)
            {
                _searchBox.KeyDown += OnSearchBoxKeyDown;
            }
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RaiseEvent(new RoutedEventArgs(SearchSubmittedEvent));
                e.Handled = true;
            }
        }

        public TableToolbar()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnToolbarButtonClick));
        }

        private void OnToolbarButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is FrameworkElement el && el.Tag is string action)
            {
                switch (action)
                {
                    case "FILTER":
                        RaiseEvent(new RoutedEventArgs(FilterClickEvent));
                        e.Handled = true;
                        break;
                    case "SORT":
                        RaiseEvent(new RoutedEventArgs(SortClickEvent));
                        e.Handled = true;
                        break;
                    case "COLUMNS":
                        RaiseEvent(new RoutedEventArgs(ColumnsClickEvent));
                        e.Handled = true;
                        break;
                    case "EXPORT":
                        RaiseEvent(new RoutedEventArgs(ExportClickEvent));
                        e.Handled = true;
                        break;
                    case "REFRESH":
                        RaiseEvent(new RoutedEventArgs(RefreshClickEvent));
                        e.Handled = true;
                        break;
                    case "CLEAR_SEARCH":
                        SearchText = string.Empty;
                        _searchBox?.Focus();
                        e.Handled = true;
                        break;
                }
            }
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TableToolbar tb)
            {
                tb.RaiseEvent(new RoutedPropertyChangedEventArgs<string>((string)e.OldValue, (string)e.NewValue, SearchTextChangedEvent));
            }
        }
    }
}
