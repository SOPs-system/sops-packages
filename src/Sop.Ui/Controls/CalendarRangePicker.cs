using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public enum CalendarRangePreset
    {
        Custom,
        Today,
        Yesterday,
        ThisWeek,
        ThisMonth,
        Last30Days,
        ThisYear
    }

    /// <summary>
    /// CalendarRangePicker control for sales reporting, auditing, and transaction-period selection.
    /// Provides quick date presets (Today, Yesterday, This Week, This Month, Last 30 Days) and
    /// an interactive two-click range calendar with apply/cancel actions.
    /// </summary>
    public class CalendarRangePicker : Control
    {
        static CalendarRangePicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CalendarRangePicker), new FrameworkPropertyMetadata(typeof(CalendarRangePicker)));
        }

        public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(
            nameof(StartDate), typeof(DateTime?), typeof(CalendarRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));

        public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(
            nameof(EndDate), typeof(DateTime?), typeof(CalendarRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));

        public static readonly DependencyProperty SelectedPresetProperty = DependencyProperty.Register(
            nameof(SelectedPreset), typeof(CalendarRangePreset), typeof(CalendarRangePicker),
            new FrameworkPropertyMetadata(CalendarRangePreset.Custom, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPresetChanged));

        public static readonly DependencyProperty DateFormatProperty = DependencyProperty.Register(
            nameof(DateFormat), typeof(string), typeof(CalendarRangePicker), new FrameworkPropertyMetadata("yyyy-MM-dd", OnFormatChanged));

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(CalendarRangePicker), new FrameworkPropertyMetadata("Select date range", OnFormatChanged));

        public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(CalendarRangePicker), new FrameworkPropertyMetadata("Select date range"));

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(CalendarRangePicker),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(ControlSize), typeof(CalendarRangePicker), new FrameworkPropertyMetadata(ControlSize.Md));

        public static readonly RoutedEvent RangeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(RangeChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<(DateTime? Start, DateTime? End)>), typeof(CalendarRangePicker));

        public static readonly RoutedEvent RangeAppliedEvent = EventManager.RegisterRoutedEvent(
            nameof(RangeApplied), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CalendarRangePicker));

        public DateTime? StartDate
        {
            get => (DateTime?)GetValue(StartDateProperty);
            set => SetValue(StartDateProperty, value);
        }

        public DateTime? EndDate
        {
            get => (DateTime?)GetValue(EndDateProperty);
            set => SetValue(EndDateProperty, value);
        }

        public CalendarRangePreset SelectedPreset
        {
            get => (CalendarRangePreset)GetValue(SelectedPresetProperty);
            set => SetValue(SelectedPresetProperty, value);
        }

        public string DateFormat
        {
            get => (string)GetValue(DateFormatProperty);
            set => SetValue(DateFormatProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public ControlSize Size
        {
            get => (ControlSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<(DateTime? Start, DateTime? End)> RangeChanged
        {
            add => AddHandler(RangeChangedEvent, value);
            remove => RemoveHandler(RangeChangedEvent, value);
        }

        public event RoutedEventHandler RangeApplied
        {
            add => AddHandler(RangeAppliedEvent, value);
            remove => RemoveHandler(RangeAppliedEvent, value);
        }

        private Popup? _popup;
        private Calendar? _calendar;
        private DateTime? _tempStart;
        private DateTime? _tempEnd;
        private bool _isUpdatingInternally;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_TriggerButton") is ButtonBase trigger)
            {
                trigger.Click -= OnTriggerClicked;
                trigger.Click += OnTriggerClicked;
            }

            _popup = GetTemplateChild("PART_Popup") as Popup;
            if (_popup != null)
            {
                _popup.Opened -= OnPopupOpened;
                _popup.Opened += OnPopupOpened;
            }

            WirePresets();

            if (GetTemplateChild("PART_ApplyButton") is System.Windows.Controls.Button applyBtn)
            {
                applyBtn.Click -= OnApplyClicked;
                applyBtn.Click += OnApplyClicked;
            }

            if (GetTemplateChild("PART_CancelButton") is System.Windows.Controls.Button cancelBtn)
            {
                cancelBtn.Click -= OnCancelClicked;
                cancelBtn.Click += OnCancelClicked;
            }

            EnsureCalendar();
            UpdateDisplayText();
        }

        private void OnTriggerClicked(object sender, RoutedEventArgs e)
        {
            IsOpen = !IsOpen;
        }

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            _tempStart = StartDate;
            _tempEnd = EndDate;
            EnsureCalendar();
            SyncCalendarSelection();
        }

        private void EnsureCalendar()
        {
            if (_calendar != null) return;

            _calendar = GetTemplateChild("PART_Calendar") as Calendar;
            if (_calendar == null && _popup?.Child != null)
            {
                _calendar = FindVisualChild<Calendar>(_popup.Child);
            }

            if (_calendar != null)
            {
                CalendarAssist.SetEnableTwoClickRange(_calendar, true);
                _calendar.SelectedDatesChanged -= OnCalendarSelectedDatesChanged;
                _calendar.SelectedDatesChanged += OnCalendarSelectedDatesChanged;
            }
        }

        private void SyncCalendarSelection()
        {
            if (_calendar == null) return;

            _isUpdatingInternally = true;
            try
            {
                _calendar.SelectedDates.Clear();
                if (_tempStart.HasValue)
                {
                    var start = _tempStart.Value.Date;
                    var end = _tempEnd?.Date ?? start;
                    if (start > end)
                    {
                        var t = start;
                        start = end;
                        end = t;
                    }

                    _calendar.DisplayDate = start;
                    _calendar.SelectedDates.AddRange(start, end);
                }
            }
            catch
            {
                // Ignore range addition errors in edge cases
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        private void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingInternally || _calendar == null) return;

            var dates = _calendar.SelectedDates.OrderBy(d => d).ToList();
            if (dates.Count > 0)
            {
                _tempStart = dates[0];
                _tempEnd = dates.Count > 1 ? dates[dates.Count - 1] : dates[0];
                SelectedPreset = CalendarRangePreset.Custom;
            }
        }

        private void WirePresets()
        {
            WirePresetButton("PART_PresetToday", CalendarRangePreset.Today);
            WirePresetButton("PART_PresetYesterday", CalendarRangePreset.Yesterday);
            WirePresetButton("PART_PresetThisWeek", CalendarRangePreset.ThisWeek);
            WirePresetButton("PART_PresetThisMonth", CalendarRangePreset.ThisMonth);
            WirePresetButton("PART_PresetLast30Days", CalendarRangePreset.Last30Days);
            WirePresetButton("PART_PresetThisYear", CalendarRangePreset.ThisYear);
        }

        private void WirePresetButton(string partName, CalendarRangePreset preset)
        {
            if (GetTemplateChild(partName) is ButtonBase btn)
            {
                btn.Click += (_, _) => ApplyPreset(preset, commit: false);
            }
        }

        public void ApplyPreset(CalendarRangePreset preset, bool commit = true)
        {
            var today = DateTime.Today;
            DateTime start;
            DateTime end;

            switch (preset)
            {
                case CalendarRangePreset.Today:
                    start = today;
                    end = today;
                    break;
                case CalendarRangePreset.Yesterday:
                    start = today.AddDays(-1);
                    end = today.AddDays(-1);
                    break;
                case CalendarRangePreset.ThisWeek:
                    var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    start = today.AddDays(-1 * diff);
                    end = today;
                    break;
                case CalendarRangePreset.ThisMonth:
                    start = new DateTime(today.Year, today.Month, 1);
                    end = today;
                    break;
                case CalendarRangePreset.Last30Days:
                    start = today.AddDays(-29);
                    end = today;
                    break;
                case CalendarRangePreset.ThisYear:
                    start = new DateTime(today.Year, 1, 1);
                    end = today;
                    break;
                default:
                    return;
            }

            _tempStart = start;
            _tempEnd = end;
            SelectedPreset = preset;
            if (commit)
            {
                StartDate = start;
                EndDate = end;
            }
            SyncCalendarSelection();
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            StartDate = _tempStart;
            EndDate = _tempEnd;
            IsOpen = false;
            RaiseEvent(new RoutedEventArgs(RangeAppliedEvent, this));
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            _tempStart = StartDate;
            _tempEnd = EndDate;
            IsOpen = false;
        }

        private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarRangePicker picker)
            {
                picker.UpdateDisplayText();
                picker.RaiseEvent(new RoutedPropertyChangedEventArgs<(DateTime? Start, DateTime? End)>(
                    (null, null),
                    (picker.StartDate, picker.EndDate),
                    RangeChangedEvent));
            }
        }

        private static void OnSelectedPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarRangePicker picker && e.NewValue is CalendarRangePreset preset && preset != CalendarRangePreset.Custom)
            {
                picker.ApplyPreset(preset);
            }
        }

        private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarRangePicker picker)
            {
                picker.UpdateDisplayText();
            }
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarRangePicker picker && (bool)e.NewValue)
            {
                picker.EnsureCalendar();
                picker.SyncCalendarSelection();
            }
        }

        public void UpdateDisplayText()
        {
            if (!StartDate.HasValue && !EndDate.HasValue)
            {
                DisplayText = Placeholder;
                return;
            }

            var arrow = FlowDirection == FlowDirection.RightToLeft ? "←" : "→";
            var format = string.IsNullOrEmpty(DateFormat) ? "yyyy-MM-dd" : DateFormat;

            if (StartDate.HasValue && EndDate.HasValue)
            {
                if (StartDate.Value.Date == EndDate.Value.Date)
                {
                    DisplayText = StartDate.Value.ToString(format, CultureInfo.InvariantCulture);
                }
                else
                {
                    DisplayText = $"{StartDate.Value.ToString(format, CultureInfo.InvariantCulture)}  {arrow}  {EndDate.Value.ToString(format, CultureInfo.InvariantCulture)}";
                }
            }
            else if (StartDate.HasValue)
            {
                DisplayText = $"From {StartDate.Value.ToString(format, CultureInfo.InvariantCulture)}";
            }
            else
            {
                DisplayText = $"Until {EndDate!.Value.ToString(format, CultureInfo.InvariantCulture)}";
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
