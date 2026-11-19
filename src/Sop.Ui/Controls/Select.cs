using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public class SelectOption
    {
        public SelectOption() { }

        public SelectOption(string value, string label, bool disabled = false)
        {
            Value = value;
            Label = label;
            Disabled = disabled;
        }

        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Disabled { get; set; }
    }

    /// <summary>A row of the open list: the option plus its live highlight state.</summary>
    public sealed class SelectRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isActive;

        public SelectRow(SelectOption option, int index, bool showCheck)
        {
            Option = option;
            Index = index;
            ShowCheck = showCheck;
        }

        public SelectOption Option { get; }
        public int Index { get; }
        /// <summary>Whether the row reserves the leading checkmark column (the Select's Check).</summary>
        public bool ShowCheck { get; }
        public string Label => Option.Label;
        public string Value => Option.Value;
        public bool IsDisabled => Option.Disabled;

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; Notify(nameof(IsSelected)); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; Notify(nameof(IsActive)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Value picker with the app's shared dropdown popover — the trigger keeps
    /// the form-control look of <see cref="Input"/>, the list is a
    /// <see cref="Menu"/> matched to the trigger's exact width with a checkmark
    /// on the selected option.
    ///
    /// It is a combobox: the field itself is the search box, so typing filters
    /// the list in place. Focus stays in the field the whole time and the
    /// highlight is driven here (<see cref="MenuItem.Active"/>). Arrows move
    /// the highlight, Enter picks it, Esc closes and restores the selected label.
    /// </summary>
    public class Select : Control
    {
        private Input? _input;
        /// <summary>
        /// How many options a dropdown shows at a time, and how many more each
        /// scroll to the bottom adds. A catalog of hundreds would otherwise
        /// build every row to show ten, on every keystroke of a search.
        /// </summary>
        public const int PageSize = 10;

        /// <summary>Grow the page this far before the true bottom of the list.</summary>
        private const double LoadMoreSlack = 24;

        private Menu? _menu;
        private MenuItem? _more;
        private MenuItem? _earlier;
        private string? _query; // null while the field shows the selected label
        private int _active;
        /// <summary>Everything the current search matches, of which Shown is a page.</summary>
        private List<SelectOption> _matches = new List<SelectOption>();
        /// <summary>Options as a list, built once per assignment (see AllOptions).</summary>
        private List<SelectOption>? _all;
        private ItemsControl? _list;
        /// <summary>
        /// The window of matches on screen, <c>[_from, _to)</c>. A list opened
        /// on a selection starts around it rather than at the top, so choosing
        /// the two-thousandth drug does not build the 1999 rows above it.
        /// </summary>
        private int _from;
        private int _to = PageSize;
        /// <summary>Set while a page is being added, so growing the list cannot ask for another.</summary>
        private bool _paging;
        private bool _settingText;
        private bool _wasOpenOnMouseDown;

        static Select()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Select), new FrameworkPropertyMetadata(typeof(Select)));
            FocusableProperty.OverrideMetadata(typeof(Select), new FrameworkPropertyMetadata(false));
        }

        public Select()
        {
            SetValue(ShownPropertyKey, new ObservableCollection<SelectRow>());
        }

        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
            nameof(Options), typeof(IEnumerable), typeof(Select),
            new FrameworkPropertyMetadata(null, (d, _) => { ((Select)d)._all = null; ((Select)d).Refresh(); }));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(Select),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Select)d).OnValueChanged()));

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(Select), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(Select), new FrameworkPropertyMetadata(ControlSize.Md));

        /// <summary>Caption above the options in the popover.</summary>
        public static readonly DependencyProperty MenuLabelProperty = DependencyProperty.Register(
            nameof(MenuLabel), typeof(object), typeof(Select), new FrameworkPropertyMetadata(null));

        /// <summary>Checkmark on the selected option (default true).</summary>
        public static readonly DependencyProperty CheckProperty = DependencyProperty.Register(
            nameof(Check), typeof(bool), typeof(Select), new FrameworkPropertyMetadata(true));

        /// <summary>Type in the field to filter the list (default true).</summary>
        public static readonly DependencyProperty SearchableProperty = DependencyProperty.Register(
            nameof(Searchable), typeof(bool), typeof(Select), new FrameworkPropertyMetadata(true));

        /// <summary>Row shown when the typed text matches nothing.</summary>
        public static readonly DependencyProperty NoMatchLabelProperty = DependencyProperty.Register(
            nameof(NoMatchLabel), typeof(string), typeof(Select), new FrameworkPropertyMetadata("No matches"));

        private static readonly DependencyPropertyKey IsOpenPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsOpen), typeof(bool), typeof(Select), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsOpenProperty = IsOpenPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey ShownPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(Shown), typeof(ObservableCollection<SelectRow>), typeof(Select), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty ShownProperty = ShownPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey DisplayTextPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(DisplayText), typeof(string), typeof(Select), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Select));

        public IEnumerable? Options
        {
            get => (IEnumerable?)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        public string? Value
        {
            get => (string?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string? Placeholder
        {
            get => (string?)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public object? MenuLabel
        {
            get => GetValue(MenuLabelProperty);
            set => SetValue(MenuLabelProperty, value);
        }

        public bool Check
        {
            get => (bool)GetValue(CheckProperty);
            set => SetValue(CheckProperty, value);
        }

        public bool Searchable
        {
            get => (bool)GetValue(SearchableProperty);
            set => SetValue(SearchableProperty, value);
        }

        public string NoMatchLabel
        {
            get => (string)GetValue(NoMatchLabelProperty);
            set => SetValue(NoMatchLabelProperty, value);
        }

        public bool IsOpen => (bool)GetValue(IsOpenProperty);

        /// <summary>The rows currently listed (filtered by the typed query).</summary>
        public ObservableCollection<SelectRow> Shown => (ObservableCollection<SelectRow>)GetValue(ShownProperty);

        /// <summary>What the field shows: the query while typing, else the selected label.</summary>
        public string DisplayText => (string)GetValue(DisplayTextProperty);

        public event RoutedEventHandler ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        /// <summary>
        /// Every option the control was given, before search and paging.
        ///
        /// Materialised once per assignment and kept: callers hand us anything
        /// enumerable, and a catalog of thousands must not be walked again on
        /// every keystroke, every value change and every close.
        /// </summary>
        public List<SelectOption> AllOptions
        {
            get
            {
                if (_all == null)
                {
                    _all = Options == null
                        ? new List<SelectOption>()
                        : Options as List<SelectOption> ?? Options.Cast<SelectOption>().ToList();
                }
                return _all;
            }
        }

        private SelectOption? SelectedOption
        {
            get
            {
                var value = Value;
                // A plain loop over the cached list: FirstOrDefault with a
                // closure allocates on a path that runs per keystroke.
                var all = AllOptions;
                for (var i = 0; i < all.Count; i++)
                {
                    if (all[i].Value == value) return all[i];
                }
                return null;
            }
        }

        public override void OnApplyTemplate()
        {
            if (_input != null)
            {
                _input.TextChanged -= OnTextChanged;
                _input.PreviewKeyDown -= OnInputKeyDown;
                _input.PreviewMouseLeftButtonDown -= OnInputMouseDown;
                _input.PreviewMouseLeftButtonUp -= OnInputMouseUp;
                _input.LostKeyboardFocus -= OnInputLostFocus;
            }
            if (_menu != null)
            {
                _menu.Closed -= OnMenuClosed;
                _menu.Panel.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnRowClick));
                _menu.Panel.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnListScrolled));
            }
            base.OnApplyTemplate();
            _input = GetTemplateChild("PART_Input") as Input;
            _menu = GetTemplateChild("PART_Menu") as Menu;
            if (_input != null)
            {
                _input.TextChanged += OnTextChanged;
                _input.PreviewKeyDown += OnInputKeyDown;
                _input.PreviewMouseLeftButtonDown += OnInputMouseDown;
                _input.PreviewMouseLeftButtonUp += OnInputMouseUp;
                _input.LostKeyboardFocus += OnInputLostFocus;
            }
            _list = GetTemplateChild("PART_Options") as ItemsControl;
            _more = GetTemplateChild("PART_More") as MenuItem;
            if (_more != null) _more.Click += OnMoreClick;
            _earlier = GetTemplateChild("PART_Earlier") as MenuItem;
            if (_earlier != null) _earlier.Click += OnEarlierClick;
            if (_menu != null)
            {
                // The "show more" row is not a choice, so it must not close the
                // list or count as one.
                _menu.Closed += OnMenuClosed;
                _menu.Panel.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnRowClick));
                // Reaching the end of the page brings the next ten with it.
                _menu.Panel.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnListScrolled));
            }
            Refresh();
            // DisplayText may already be final (set before the template
            // existed), so no change notification would push it into the field.
            SyncText();
        }

        private void OnValueChanged()
        {
            if (_query == null) UpdateDisplay();
            var onPage = false;
            foreach (var row in Shown)
            {
                row.IsSelected = row.Value == Value;
                onPage |= row.IsSelected;
            }
            // A value set from outside — a form filling in its fields after
            // handing over the options — can be nowhere near the page on
            // screen. Build the window around it instead, so opening the list
            // lands on what is selected.
            if (!onPage && !IsOpen && _query == null) Refresh();
        }

        private void UpdateDisplay()
        {
            SetValue(DisplayTextPropertyKey, _query ?? SelectedOption?.Label ?? string.Empty);
        }

        private void Refresh()
        {
            var term = Searchable && _query != null ? _query.Trim() : string.Empty;
            var options = AllOptions;
            // One pass, no per-option string allocation: lower-casing every
            // label of a big catalog on every keystroke is what makes a large
            // picker feel heavy to type in.
            _matches.Clear();
            if (_matches.Capacity < options.Count) _matches.Capacity = options.Count;
            var value = Value;
            var selected = -1;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (term.Length != 0 && option.Label.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (selected < 0 && option.Value == value) selected = _matches.Count;
                _matches.Add(option);
            }
            // A new search is a new list, and starts at the top. Opening on a
            // selection instead starts around it: the ten before it are there
            // to scroll back through, and the next ten arrive on the way down.
            if (term.Length == 0 && selected >= 0)
            {
                _from = Math.Max(0, selected - PageSize);
                _to = Math.Min(_matches.Count, Math.Max(_from + PageSize, selected + PageSize));
            }
            else
            {
                _from = 0;
                _to = Math.Min(_matches.Count, PageSize);
            }
            FillPage();
            SetActive(_active);
            UpdateDisplay();
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            // Not a choice: the list stays open on the next page.
            e.Handled = true;
            ShowMore();
        }

        private void OnEarlierClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            ShowEarlier();
        }

        private void OnListScrolled(object sender, ScrollChangedEventArgs e)
        {
            // A page of its own making: adding rows moves the extent, and that
            // must not read as the user asking for another page.
            if (_paging || e.ExtentHeight <= 0 || e.ExtentHeightChange != 0) return;
            if (HasMore && e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - LoadMoreSlack) ShowMore();
            // Upwards only, and only on a scroll the user made: holding the
            // list in place under the new rows moves the offset itself, and
            // that must not read as another request.
            else if (HasEarlier && e.VerticalChange < 0 && e.VerticalOffset <= LoadMoreSlack) ShowEarlier();
        }

        /// <summary>Rebuild Shown from the window <c>[_from, _to)</c>.</summary>
        private void FillPage()
        {
            Shown.Clear();
            AppendPage();
        }

        /// <summary>
        /// Add the rows between what is on screen and the end of the window.
        /// Growing the page must not clear the collection: a Reset throws away
        /// every generated row and builds it again, so page twenty would cost
        /// twenty pages' worth of work.
        /// </summary>
        private void AppendPage()
        {
            var shown = Shown;
            for (var i = _from + shown.Count; i < _to; i++) shown.Add(Row(i));
            SyncPagingRows();
        }

        /// <summary>Put the rows back on the front, from <paramref name="was"/> down to the new start.</summary>
        private void PrependPage(int was)
        {
            var shown = Shown;
            for (var i = was - 1; i >= _from; i--) shown.Insert(0, Row(i));
            // The highlight belongs to an option, not to a position.
            _active += was - _from;
            SyncPagingRows();
        }

        private SelectRow Row(int index)
        {
            var option = _matches[index];
            return new SelectRow(option, index, Check) { IsSelected = option.Value == Value };
        }

        /// <summary>True while scrolling — or arrowing — further down would reveal more.</summary>
        public bool HasMore => _to < _matches.Count;

        /// <summary>True while there are options above the ones on screen.</summary>
        public bool HasEarlier => _from > 0;

        private static readonly DependencyPropertyKey MoreLabelPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(MoreLabel), typeof(string), typeof(Select), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty MoreLabelProperty = MoreLabelPropertyKey.DependencyProperty;

        /// <summary>What the row at the end of a paged list says, e.g. "Show 10 more of 240".</summary>
        public string MoreLabel => (string)GetValue(MoreLabelProperty);

        private static readonly DependencyPropertyKey EarlierLabelPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(EarlierLabel), typeof(string), typeof(Select), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty EarlierLabelProperty = EarlierLabelPropertyKey.DependencyProperty;

        /// <summary>What the row above a windowed list says, e.g. "Show 10 earlier of 240".</summary>
        public string EarlierLabel => (string)GetValue(EarlierLabelProperty);

        /// <summary>Add one more page below; no-op once every match is on screen.</summary>
        public void ShowMore()
        {
            if (!HasMore) return;
            _paging = true;
            try
            {
                _to = Math.Min(_matches.Count, _to + PageSize);
                AppendPage();
            }
            finally { _paging = false; }
        }

        /// <summary>
        /// Add one more page above — the ten options before the ones on screen.
        /// The list is held where it was: rows inserted over the viewport would
        /// otherwise push what the eye is on straight down out of it.
        /// </summary>
        public void ShowEarlier()
        {
            if (!HasEarlier) return;
            _paging = true;
            try
            {
                var scroller = _menu?.Panel.Scroller;
                var offset = scroller?.VerticalOffset ?? 0;
                var extent = scroller?.ExtentHeight ?? 0;
                var was = _from;
                _from = Math.Max(0, _from - PageSize);
                PrependPage(was);
                if (scroller != null) HoldPlace(scroller, offset, extent, was - _from);
            }
            finally { _paging = false; }
        }

        /// <summary>
        /// Scroll down by as much as was just added above, so the list holds
        /// still under the new rows.
        ///
        /// The height comes from a row already on screen: this runs from inside
        /// a scroll, which is inside a layout pass, and asking for a new layout
        /// there does nothing — the extent has not grown yet. Once it has, the
        /// deferred pass below corrects the guess (the "show earlier" row
        /// disappearing at the top of the list is one such difference).
        /// </summary>
        private void HoldPlace(ScrollViewer scroller, double offset, double extent, int added)
        {
            var estimate = added * RowHeight();
            if (estimate > 0) scroller.ScrollToVerticalOffset(offset + estimate);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var grown = scroller.ExtentHeight - extent;
                if (grown > 0 && Math.Abs(grown - estimate) > 0.5) scroller.ScrollToVerticalOffset(offset + grown);
            }));
        }

        /// <summary>How tall one option is, measured from one on screen; 0 when none is built yet.</summary>
        private double RowHeight()
        {
            var container = _list?.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
            return container?.ActualHeight ?? 0;
        }

        /// <summary>
        /// Keep the rows at either end in step with what they are holding back.
        /// Each is shown only while there is something on that side, so a short
        /// list looks exactly as it always did.
        /// </summary>
        private void SyncPagingRows()
        {
            var after = _matches.Count - _to;
            SetValue(MoreLabelPropertyKey, after <= 0 ? string.Empty : Tr(MoreText, after, _matches.Count));
            if (_more != null) _more.Visibility = after > 0 ? Visibility.Visible : Visibility.Collapsed;

            var before = _from;
            SetValue(EarlierLabelPropertyKey, before <= 0 ? string.Empty : Tr(EarlierText, before, _matches.Count));
            if (_earlier != null) _earlier.Visibility = before > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The wording, in the reading language. Sop.Ui carries no dictionary
        /// of its own, so the app installs these; without them the rows still
        /// work and just count.
        /// </summary>
        public static Func<int, int, string>? MoreText { get; set; }

        /// <summary>As <see cref="MoreText"/>, for the row above a windowed list.</summary>
        public static Func<int, int, string>? EarlierText { get; set; }

        private static string Tr(Func<int, int, string>? text, int remaining, int total) =>
            text?.Invoke(remaining, total) ?? $"+{remaining}";

        private void SetActive(int index)
        {
            var shown = Shown;
            _active = shown.Count == 0 ? 0 : ((index % shown.Count) + shown.Count) % shown.Count;
            for (var i = 0; i < shown.Count; i++) shown[i].IsActive = i == _active;
            if (IsOpen && shown.Count > 0)
            {
                // Straight to the row's container. Walking the popup's whole
                // visual tree for it costs the length of the list on every
                // arrow key, which is exactly when a long list must feel light.
                if (_list?.ItemContainerGenerator.ContainerFromIndex(_active) is FrameworkElement container)
                {
                    container.BringIntoView();
                }
                else if (_menu != null)
                {
                    _menu.Items().FirstOrDefault(m => ReferenceEquals(m.DataContext, shown[_active]))?.BringIntoView();
                }
            }
        }

        private void OpenList()
        {
            if (_menu == null || !IsEnabled) return;
            // Open on the current value, so arrows continue from what is selected.
            var shown = Shown;
            var index = -1;
            for (var i = 0; i < shown.Count; i++)
            {
                if (shown[i].Value == Value) { index = i; break; }
            }
            SetValue(IsOpenPropertyKey, true);
            _menu.IsOpen = true;
            SetActive(index < 0 ? 0 : index);
        }

        private void Close(bool refocus = true)
        {
            if (_menu != null) _menu.IsOpen = false;
            SetValue(IsOpenPropertyKey, false);
            _query = null;
            Refresh();
            if (refocus) _input?.Focus();
        }

        private void Choose(SelectOption option)
        {
            if (option.Disabled) return;
            var changed = Value != option.Value;
            Value = option.Value;
            Close();
            if (changed) RaiseEvent(new RoutedEventArgs(ValueChangedEvent, this));
        }

        private void OnRowClick(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement ?? e.Source as FrameworkElement)?.DataContext is SelectRow row)
            {
                e.Handled = true;
                Choose(row.Option);
            }
        }

        private void OnMenuClosed(object? sender, System.EventArgs e)
        {
            // Closed from outside (a click elsewhere): drop the query.
            if (IsOpen) Close(false);
        }

        private void OnInputMouseDown(object sender, MouseButtonEventArgs e)
        {
            _wasOpenOnMouseDown = IsOpen;
        }

        private void OnInputMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_wasOpenOnMouseDown)
            {
                if (IsOpen) Close(true);
                _wasOpenOnMouseDown = false;
                return;
            }

            if (!IsOpen)
            {
                OpenList();
                if (Searchable && _input != null)
                {
                    _input.Focus();
                    _input.SelectAll();
                }
            }
        }

        private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Focus left the field for somewhere other than our own list.
            if (IsOpen && e.NewFocus is DependencyObject next && _menu != null)
            {
                if (ReferenceEquals(next, _input) || (_input != null && _input.IsAncestorOf(next as System.Windows.Media.Visual ?? _input))) return;
                if (!_menu.Panel.IsAncestorOf(next as System.Windows.Media.Visual ?? _menu.Panel))
                    Close(false);
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settingText || _input == null) return;
            if (!Searchable) return;
            if (_input.Text == DisplayText) return;
            _query = _input.Text;
            _active = 0;
            Refresh();
            if (!IsOpen) OpenList();
        }

        /// <summary>Push DisplayText into the field without re-entering the filter.</summary>
        /// <summary>Type into the field, as a search does — for tests.</summary>
        public void SearchForTest(string text)
        {
            _query = text;
            _active = 0;
            Refresh();
        }

        internal void SyncText()
        {
            if (_input == null) return;
            _settingText = true;
            try { _input.Text = DisplayText; _input.CaretIndex = _input.Text.Length; }
            finally { _settingText = false; }
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            switch (e.Key)
            {
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    if (!IsOpen) { OpenList(); return; }
                    if (Shown.Count == 0) return;
                    // Arrowing off either end of the page brings that side's
                    // ten in rather than wrapping round to the other end.
                    if (e.Key == Key.Down && _active == Shown.Count - 1 && HasMore) ShowMore();
                    else if (e.Key == Key.Up && _active == 0 && HasEarlier) ShowEarlier();
                    SetActive(_active + (e.Key == Key.Down ? 1 : -1));
                    return;
            }
            if (!IsOpen) return;
            switch (e.Key)
            {
                case Key.Home:
                case Key.End:
                    e.Handled = true;
                    // The first and the last option, not the first and last
                    // built so far.
                    if (e.Key == Key.End) while (HasMore) ShowMore();
                    else while (HasEarlier) ShowEarlier();
                    SetActive(e.Key == Key.Home ? 0 : Shown.Count - 1);
                    return;
                case Key.Enter:
                    if (_active < Shown.Count && !Shown[_active].IsDisabled)
                    {
                        e.Handled = true;
                        Choose(Shown[_active].Option);
                    }
                    return;
                case Key.Escape:
                    // Handled: a surrounding modal must not take Esc as its own close.
                    e.Handled = true;
                    Close();
                    return;
                case Key.Tab:
                    Close(false);
                    return;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == DisplayTextProperty) SyncText();
        }
    }
}
