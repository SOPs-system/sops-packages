using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Calendar = System.Windows.Controls.Calendar;

namespace Sop.Ui.Controls
{
    [TemplatePart(Name = "PART_Border", Type = typeof(Border))]
    [TemplatePart(Name = "PART_CalendarButton", Type = typeof(ButtonBase))]
    [TemplatePart(Name = "PART_YearBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_MonthBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_DayBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_YearPlaceholder", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_MonthPlaceholder", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_DayPlaceholder", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "PART_Calendar", Type = typeof(Calendar))]
    public class DateInput : Control
    {
        private Border? _border;
        private ButtonBase? _calendarButton;
        private TextBox? _yearBox, _monthBox, _dayBox;
        private TextBlock? _yearPlaceholder, _monthPlaceholder, _dayPlaceholder;
        private Popup? _popup;
        private Calendar? _calendar;
        private bool _isUpdating, _isSyncingCalendar;
        private int _popupClosedTimestamp;

        static DateInput()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DateInput), new FrameworkPropertyMetadata(typeof(DateInput)));
            FocusableProperty.OverrideMetadata(typeof(DateInput), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(DateInput),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

        public static readonly DependencyProperty DisplayDateProperty =
            DependencyProperty.Register(nameof(DisplayDate), typeof(DateTime), typeof(DateInput),
                new FrameworkPropertyMetadata(DateTime.Today));

        public static readonly DependencyProperty DisplayDateStartProperty =
            DependencyProperty.Register(nameof(DisplayDateStart), typeof(DateTime?), typeof(DateInput),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty DisplayDateEndProperty =
            DependencyProperty.Register(nameof(DisplayDateEnd), typeof(DateTime?), typeof(DateInput),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(DateInput),
                new FrameworkPropertyMetadata(false, OnIsDropDownOpenChanged));

        public static readonly DependencyProperty YearPlaceholderProperty =
            DependencyProperty.Register(nameof(YearPlaceholder), typeof(string), typeof(DateInput),
                new FrameworkPropertyMetadata("YYYY"));

        public static readonly DependencyProperty MonthPlaceholderProperty =
            DependencyProperty.Register(nameof(MonthPlaceholder), typeof(string), typeof(DateInput),
                new FrameworkPropertyMetadata("MM"));

        public static readonly DependencyProperty DayPlaceholderProperty =
            DependencyProperty.Register(nameof(DayPlaceholder), typeof(string), typeof(DateInput),
                new FrameworkPropertyMetadata("DD"));

        public static readonly RoutedEvent SelectedDateChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectedDateChanged), RoutingStrategy.Bubble,
                typeof(SelectionChangedEventHandler), typeof(DateInput));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public DateTime DisplayDate
        {
            get => (DateTime)GetValue(DisplayDateProperty);
            set => SetValue(DisplayDateProperty, value);
        }

        public DateTime? DisplayDateStart
        {
            get => (DateTime?)GetValue(DisplayDateStartProperty);
            set => SetValue(DisplayDateStartProperty, value);
        }

        public DateTime? DisplayDateEnd
        {
            get => (DateTime?)GetValue(DisplayDateEndProperty);
            set => SetValue(DisplayDateEndProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public string YearPlaceholder
        {
            get => (string)GetValue(YearPlaceholderProperty);
            set => SetValue(YearPlaceholderProperty, value);
        }

        public string MonthPlaceholder
        {
            get => (string)GetValue(MonthPlaceholderProperty);
            set => SetValue(MonthPlaceholderProperty, value);
        }

        public string DayPlaceholder
        {
            get => (string)GetValue(DayPlaceholderProperty);
            set => SetValue(DayPlaceholderProperty, value);
        }

        public event SelectionChangedEventHandler SelectedDateChanged
        {
            add => AddHandler(SelectedDateChangedEvent, value);
            remove => RemoveHandler(SelectedDateChangedEvent, value);
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var input = (DateInput)d;
            input.SyncTextFromSelectedDate();

            if (input._calendar != null && !input._isSyncingCalendar)
            {
                input._isSyncingCalendar = true;
                try
                {
                    input._calendar.SelectedDate = (DateTime?)e.NewValue;
                    if (e.NewValue is DateTime dt)
                    {
                        input._calendar.DisplayDate = dt;
                    }
                }
                finally
                {
                    input._isSyncingCalendar = false;
                }
            }

            var args = new SelectionChangedEventArgs(
                SelectedDateChangedEvent,
                e.OldValue != null ? new object[] { e.OldValue } : Array.Empty<object>(),
                e.NewValue != null ? new object[] { e.NewValue } : Array.Empty<object>());
            input.RaiseEvent(args);
        }

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var input = (DateInput)d;
            var open = (bool)e.NewValue;
            if (open)
            {
                if (input._calendar != null)
                {
                    input._isSyncingCalendar = true;
                    try
                    {
                        input._calendar.SelectedDate = input.SelectedDate;
                        input._calendar.DisplayDate = input.SelectedDate ?? input.DisplayDate;
                    }
                    finally
                    {
                        input._isSyncingCalendar = false;
                    }
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_calendarButton != null)
            {
                _calendarButton.Click -= OnCalendarButtonClick;
            }
            if (_popup != null)
            {
                _popup.Closed -= OnPopupClosed;
                _popup.Opened -= OnPopupOpened;
            }
            if (_calendar != null)
            {
                _calendar.SelectedDatesChanged -= OnCalendarSelectedDatesChanged;
                _calendar.PreviewMouseUp -= OnCalendarPreviewMouseUp;
            }

            _border = GetTemplateChild("PART_Border") as Border;
            _calendarButton = GetTemplateChild("PART_CalendarButton") as ButtonBase;
            _yearBox = GetTemplateChild("PART_YearBox") as TextBox;
            _monthBox = GetTemplateChild("PART_MonthBox") as TextBox;
            _dayBox = GetTemplateChild("PART_DayBox") as TextBox;
            _yearPlaceholder = GetTemplateChild("PART_YearPlaceholder") as TextBlock;
            _monthPlaceholder = GetTemplateChild("PART_MonthPlaceholder") as TextBlock;
            _dayPlaceholder = GetTemplateChild("PART_DayPlaceholder") as TextBlock;
            _popup = GetTemplateChild("PART_Popup") as Popup;

            if (_calendarButton != null)
            {
                _calendarButton.Click += OnCalendarButtonClick;
            }
            if (_popup != null)
            {
                _popup.Closed += OnPopupClosed;
                _popup.Opened += OnPopupOpened;
            }

            EnsureCalendar();

            HookSegmentBox(_yearBox, OnYearKeyDown);
            HookSegmentBox(_monthBox, OnMonthKeyDown);
            HookSegmentBox(_dayBox, OnDayKeyDown);

            SyncTextFromSelectedDate();
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            EnsureCalendar();
            if (_calendar != null)
            {
                _isSyncingCalendar = true;
                try
                {
                    _calendar.SelectedDate = SelectedDate;
                    _calendar.DisplayDate = SelectedDate ?? DisplayDate;
                }
                finally
                {
                    _isSyncingCalendar = false;
                }
            }
        }

        private void EnsureCalendar()
        {
            if (_calendar == null)
            {
                _calendar = GetTemplateChild("PART_Calendar") as Calendar;
                if (_calendar == null && _popup != null)
                {
                    if (_popup.Child is Border b && b.Child is Calendar c)
                    {
                        _calendar = c;
                    }
                    else if (_popup.Child != null)
                    {
                        _calendar = FindVisualChild<Calendar>(_popup.Child);
                    }
                }
            }

            if (_calendar != null)
            {
                _calendar.SelectedDatesChanged -= OnCalendarSelectedDatesChanged;
                _calendar.SelectedDatesChanged += OnCalendarSelectedDatesChanged;
                _calendar.PreviewMouseUp -= OnCalendarPreviewMouseUp;
                _calendar.PreviewMouseUp += OnCalendarPreviewMouseUp;
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private void OnCalendarPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var dayButton = CalendarAssist.FindVisualParent<CalendarDayButton>(e.OriginalSource as DependencyObject);
            if (dayButton?.DataContext is DateTime clickedDate)
            {
                SelectedDate = clickedDate;
                IsDropDownOpen = false;
                _yearBox?.Focus();
            }
        }

        private void HookSegmentBox(TextBox? box, KeyEventHandler keyHandler)
        {
            if (box == null) return;
            box.TextChanged += OnSegmentTextChanged;
            box.PreviewTextInput += OnNumericTextInput;
            box.KeyDown += keyHandler;
            box.GotFocus += (s, e) => (s as TextBox)?.SelectAll();
        }

        private void OnCalendarButtonClick(object sender, RoutedEventArgs e)
        {
            var now = Environment.TickCount;
            if (unchecked(now - _popupClosedTimestamp) < 200)
            {
                return;
            }
            IsDropDownOpen = !IsDropDownOpen;
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _popupClosedTimestamp = Environment.TickCount;
            IsDropDownOpen = false;
        }

        private void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingCalendar) return;
            DateTime? chosen = null;
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is DateTime dt)
            {
                chosen = dt;
            }
            else if (_calendar?.SelectedDate.HasValue == true)
            {
                chosen = _calendar.SelectedDate.Value;
            }

            if (chosen.HasValue)
            {
                SelectedDate = chosen.Value;
                IsDropDownOpen = false;
                _yearBox?.Focus();
            }
        }

        private void SyncTextFromSelectedDate()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                var (y, m, d) = DateParser.FormatSegments(SelectedDate);
                if (_yearBox != null) _yearBox.Text = y;
                if (_monthBox != null) _monthBox.Text = m;
                if (_dayBox != null) _dayBox.Text = d;
            }
            finally
            {
                _isUpdating = false;
            }
            UpdatePlaceholders();
        }

        private void UpdatePlaceholders()
        {
            if (_yearPlaceholder != null) _yearPlaceholder.Visibility = string.IsNullOrEmpty(_yearBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (_monthPlaceholder != null) _monthPlaceholder.Visibility = string.IsNullOrEmpty(_monthBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (_dayPlaceholder != null) _dayPlaceholder.Visibility = string.IsNullOrEmpty(_dayBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSegmentTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            UpdatePlaceholders();

            if (sender == _yearBox && _yearBox?.Text.Length == 4 && _yearBox.IsKeyboardFocusWithin)
            {
                _monthBox?.Focus();
                _monthBox?.SelectAll();
            }
            else if (sender == _monthBox && _monthBox?.Text.Length == 2 && _monthBox.IsKeyboardFocusWithin)
            {
                _dayBox?.Focus();
                _dayBox?.SelectAll();
            }

            if (DateParser.TryParseDate(_yearBox?.Text, _monthBox?.Text, _dayBox?.Text, out var parsedDate) && SelectedDate != parsedDate)
            {
                _isUpdating = true;
                try
                {
                    SelectedDate = parsedDate;
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void OnNumericTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DateParser.IsNumeric(e.Text);
        }

        private void OnYearKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Right && _yearBox != null && _yearBox.SelectionStart == _yearBox.Text.Length)
            {
                _monthBox?.Focus();
                _monthBox?.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _yearBox!.Text = DateParser.StepYear(_yearBox.Text, 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                IsDropDownOpen = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _yearBox!.Text = DateParser.StepYear(_yearBox.Text, -1);
                e.Handled = true;
            }
        }

        private void OnMonthKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && _monthBox != null && _monthBox.SelectionStart == 0 && _monthBox.SelectionLength == 0)
            {
                _yearBox?.Focus();
                if (_yearBox != null) _yearBox.SelectionStart = _yearBox.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Left && _monthBox != null && _monthBox.SelectionStart == 0)
            {
                _yearBox?.Focus();
                _yearBox?.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Right && _monthBox != null && _monthBox.SelectionStart == _monthBox.Text.Length)
            {
                _dayBox?.Focus();
                _dayBox?.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _monthBox!.Text = DateParser.StepMonth(_monthBox.Text, 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _monthBox!.Text = DateParser.StepMonth(_monthBox.Text, -1);
                e.Handled = true;
            }
        }

        private void OnDayKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && _dayBox != null && _dayBox.SelectionStart == 0 && _dayBox.SelectionLength == 0)
            {
                _monthBox?.Focus();
                if (_monthBox != null) _monthBox.SelectionStart = _monthBox.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Left && _dayBox != null && _dayBox.SelectionStart == 0)
            {
                _monthBox?.Focus();
                _monthBox?.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _dayBox!.Text = DateParser.StepDay(_dayBox.Text, _yearBox?.Text, _monthBox?.Text, 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _dayBox!.Text = DateParser.StepDay(_dayBox.Text, _yearBox?.Text, _monthBox?.Text, -1);
                e.Handled = true;
            }
        }
    }
}
