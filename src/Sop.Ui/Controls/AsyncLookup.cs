using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Sop.Ui.Controls
{
    [TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "PART_ListBox", Type = typeof(ListBox))]
    [TemplatePart(Name = "PART_ClearButton", Type = typeof(Button))]
    public class AsyncLookup : Control
    {
        private TextBox? _textBox;
        private Popup? _popup;
        private ListBox? _listBox;
        private Button? _clearButton;
        private DispatcherTimer? _debounceTimer;
        private bool _isUpdatingText;

        static AsyncLookup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AsyncLookup), new FrameworkPropertyMetadata(typeof(AsyncLookup)));
            FocusableProperty.OverrideMetadata(typeof(AsyncLookup), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty QueryProperty =
            DependencyProperty.Register(nameof(Query), typeof(string), typeof(AsyncLookup),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnQueryChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(AsyncLookup),
                new PropertyMetadata("Search catalog..."));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(AsyncLookup),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(AsyncLookup),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AsyncLookup),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(AsyncLookup),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(AsyncLookup),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DebounceMsProperty =
            DependencyProperty.Register(nameof(DebounceMs), typeof(int), typeof(AsyncLookup),
                new PropertyMetadata(250));

        public static readonly DependencyProperty SearchCommandProperty =
            DependencyProperty.Register(nameof(SearchCommand), typeof(ICommand), typeof(AsyncLookup),
                new PropertyMetadata(null));

        public static readonly RoutedEvent SearchQueryChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SearchQueryChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<string>), typeof(AsyncLookup));

        public string Query
        {
            get => (string)GetValue(QueryProperty);
            set => SetValue(QueryProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public DataTemplate? ItemTemplate
        {
            get => (DataTemplate?)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public int DebounceMs
        {
            get => (int)GetValue(DebounceMsProperty);
            set => SetValue(DebounceMsProperty, value);
        }

        public ICommand? SearchCommand
        {
            get => (ICommand?)GetValue(SearchCommandProperty);
            set => SetValue(SearchCommandProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<string> SearchQueryChanged
        {
            add => AddHandler(SearchQueryChangedEvent, value);
            remove => RemoveHandler(SearchQueryChangedEvent, value);
        }

        private static void OnQueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lookup = (AsyncLookup)d;
            if (lookup._isUpdatingText) return;

            lookup.RestartDebounceTimer();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lookup = (AsyncLookup)d;
            if (e.NewValue is IEnumerable items)
            {
                var enumerator = items.GetEnumerator();
                bool hasItems = enumerator.MoveNext();
                lookup.IsDropDownOpen = hasItems && !string.IsNullOrWhiteSpace(lookup.Query);
            }
            else
            {
                lookup.IsDropDownOpen = false;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.TextChanged -= OnTextBoxTextChanged;
                _textBox.KeyDown -= OnTextBoxKeyDown;
            }
            if (_clearButton != null)
            {
                _clearButton.Click -= OnClearButtonClick;
            }
            if (_listBox != null)
            {
                _listBox.SelectionChanged -= OnListBoxSelectionChanged;
            }

            _textBox = GetTemplateChild("PART_TextBox") as TextBox;
            _popup = GetTemplateChild("PART_Popup") as Popup;
            _listBox = GetTemplateChild("PART_ListBox") as ListBox;
            _clearButton = GetTemplateChild("PART_ClearButton") as Button;

            if (_textBox != null)
            {
                _textBox.TextChanged += OnTextBoxTextChanged;
                _textBox.KeyDown += OnTextBoxKeyDown;
            }
            if (_clearButton != null)
            {
                _clearButton.Click += OnClearButtonClick;
            }
            if (_listBox != null)
            {
                _listBox.SelectionChanged += OnListBoxSelectionChanged;
            }
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_textBox == null) return;
            _isUpdatingText = true;
            try
            {
                Query = _textBox.Text;
            }
            finally
            {
                _isUpdatingText = false;
            }
            RestartDebounceTimer();
        }

        private void RestartDebounceTimer()
        {
            _debounceTimer?.Stop();
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(50, DebounceMs))
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ExecuteSearch();
            };
            _debounceTimer.Start();
        }

        private void ExecuteSearch()
        {
            var q = Query?.Trim() ?? string.Empty;
            var args = new RoutedPropertyChangedEventArgs<string>(string.Empty, q, SearchQueryChangedEvent);
            RaiseEvent(args);

            if (SearchCommand?.CanExecute(q) == true)
            {
                SearchCommand.Execute(q);
            }
        }

        private void OnClearButtonClick(object sender, RoutedEventArgs e)
        {
            Query = string.Empty;
            if (_textBox != null) _textBox.Text = string.Empty;
            IsDropDownOpen = false;
            _textBox?.Focus();
        }

        private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_listBox?.SelectedItem != null)
            {
                SelectedItem = _listBox.SelectedItem;
                IsDropDownOpen = false;
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && IsDropDownOpen && _listBox != null)
            {
                _listBox.Focus();
                if (_listBox.Items.Count > 0)
                {
                    _listBox.SelectedIndex = 0;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsDropDownOpen = false;
                e.Handled = true;
            }
        }
    }
}
