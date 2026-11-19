using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Attached properties and interaction behavior for WPF Calendar controls:
    /// 1. Provides <see cref="IsRangeMiddleProperty"/> to style in-between range days with soft brand tint.
    /// 2. Provides <see cref="EnableTwoClickRangeProperty"/> for two-click range selection.
    /// </summary>
    public static class CalendarAssist
    {
        public static readonly DependencyProperty IsRangeMiddleProperty = DependencyProperty.RegisterAttached(
            "IsRangeMiddle", typeof(bool), typeof(CalendarAssist),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static bool GetIsRangeMiddle(DependencyObject obj) => (bool)obj.GetValue(IsRangeMiddleProperty);
        public static void SetIsRangeMiddle(DependencyObject obj, bool value) => obj.SetValue(IsRangeMiddleProperty, value);

        public static readonly DependencyProperty EnableTwoClickRangeProperty = DependencyProperty.RegisterAttached(
            "EnableTwoClickRange", typeof(bool), typeof(CalendarAssist),
            new FrameworkPropertyMetadata(false, OnEnableTwoClickRangeChanged));

        public static bool GetEnableTwoClickRange(DependencyObject obj) => (bool)obj.GetValue(EnableTwoClickRangeProperty);
        public static void SetEnableTwoClickRange(DependencyObject obj, bool value) => obj.SetValue(EnableTwoClickRangeProperty, value);

        private static readonly DependencyProperty RangeStartProperty = DependencyProperty.RegisterAttached(
            "RangeStart", typeof(DateTime?), typeof(CalendarAssist), new FrameworkPropertyMetadata(null));

        private static readonly DependencyProperty IsRangeCompleteProperty = DependencyProperty.RegisterAttached(
            "IsRangeComplete", typeof(bool), typeof(CalendarAssist), new FrameworkPropertyMetadata(true));

        private static void OnEnableTwoClickRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Calendar calendar) return;

            if ((bool)e.NewValue)
            {
                if (calendar.SelectionMode != CalendarSelectionMode.MultipleRange)
                {
                    calendar.SelectionMode = CalendarSelectionMode.MultipleRange;
                }

                calendar.PreviewMouseLeftButtonDown += OnCalendarPreviewMouseLeftButtonDown;
                calendar.SelectedDatesChanged += OnCalendarSelectedDatesChanged;
                calendar.DisplayDateChanged += OnCalendarDisplayDateChanged;
                calendar.DisplayModeChanged += OnCalendarDisplayModeChanged;
                calendar.Loaded += OnCalendarLoaded;

                if (calendar.IsLoaded)
                {
                    UpdateRangeButtons(calendar);
                }
            }
            else
            {
                calendar.PreviewMouseLeftButtonDown -= OnCalendarPreviewMouseLeftButtonDown;
                calendar.SelectedDatesChanged -= OnCalendarSelectedDatesChanged;
                calendar.DisplayDateChanged -= OnCalendarDisplayDateChanged;
                calendar.DisplayModeChanged -= OnCalendarDisplayModeChanged;
                calendar.Loaded -= OnCalendarLoaded;
            }
        }

        private static void OnCalendarLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Calendar calendar)
            {
                UpdateRangeButtons(calendar);
            }
        }

        private static void OnCalendarDisplayModeChanged(object? sender, CalendarModeChangedEventArgs e)
        {
            if (sender is Calendar calendar && calendar.DisplayMode == CalendarMode.Month)
            {
                calendar.Dispatcher.BeginInvoke(new Action(() => UpdateRangeButtons(calendar)), DispatcherPriority.Loaded);
            }
        }

        private static void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
        {
            if (sender is Calendar calendar)
            {
                calendar.Dispatcher.BeginInvoke(new Action(() => UpdateRangeButtons(calendar)), DispatcherPriority.Loaded);
            }
        }

        private static void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is Calendar calendar)
            {
                calendar.Dispatcher.BeginInvoke(new Action(() => UpdateRangeButtons(calendar)), DispatcherPriority.Loaded);
            }
        }

        private static void OnCalendarPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Calendar calendar) return;
            if (calendar.DisplayMode != CalendarMode.Month) return;

            var dayButton = FindVisualParent<CalendarDayButton>(e.OriginalSource as DependencyObject);
            if (dayButton?.DataContext is not DateTime clickedDate) return;

            // Blackout dates cannot be selected
            if (calendar.BlackoutDates.Contains(clickedDate)) return;

            // Ensure selection mode allows multiple dates before modifying SelectedDates
            if (calendar.SelectionMode != CalendarSelectionMode.MultipleRange)
            {
                calendar.SelectionMode = CalendarSelectionMode.MultipleRange;
            }

            var isComplete = (bool)calendar.GetValue(IsRangeCompleteProperty);
            var rangeStart = (DateTime?)calendar.GetValue(RangeStartProperty);

            if (isComplete || !rangeStart.HasValue)
            {
                calendar.SetValue(RangeStartProperty, clickedDate);
                calendar.SetValue(IsRangeCompleteProperty, false);

                calendar.SelectedDates.Clear();
                calendar.SelectedDates.Add(clickedDate);
            }
            else
            {
                var start = rangeStart.Value < clickedDate ? rangeStart.Value : clickedDate;
                var end = rangeStart.Value < clickedDate ? clickedDate : rangeStart.Value;

                calendar.SetValue(IsRangeCompleteProperty, true);

                calendar.SelectedDates.Clear();
                try
                {
                    calendar.SelectedDates.AddRange(start, end);
                }
                catch
                {
                    for (var d = start; d <= end; d = d.AddDays(1))
                    {
                        if (!calendar.BlackoutDates.Contains(d))
                        {
                            calendar.SelectedDates.Add(d);
                        }
                    }
                }
            }

            if (clickedDate.Month != calendar.DisplayDate.Month || clickedDate.Year != calendar.DisplayDate.Year)
            {
                calendar.DisplayDate = clickedDate;
            }

            if (Mouse.Captured != null)
            {
                Mouse.Capture(null);
            }

            calendar.Focus();
            UpdateRangeButtons(calendar);
            e.Handled = true;
        }

        public static void UpdateRangeButtons(Calendar calendar)
        {
            if (calendar.DisplayMode != CalendarMode.Month) return;

            var selected = calendar.SelectedDates;
            DateTime? min = null;
            DateTime? max = null;

            if (selected.Count > 1)
            {
                min = selected.Min();
                max = selected.Max();
            }

            var buttons = FindVisualChildren<CalendarDayButton>(calendar);
            foreach (var btn in buttons)
            {
                if (btn.DataContext is DateTime date)
                {
                    bool isMiddle = min.HasValue && max.HasValue && min.Value != max.Value && date > min.Value && date < max.Value;
                    SetIsRangeMiddle(btn, isMiddle);
                }
                else
                {
                    SetIsRangeMiddle(btn, false);
                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typedParent) return typedParent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
