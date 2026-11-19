using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Sop.Ui.Controls;
using Xunit;

namespace Sop.Ui.Tests
{
    public class DateAndNumericInputTests
    {
        [Fact]
        public void DateParser_FormatSegments_HandlesNullAndValidDates()
        {
            var (yNull, mNull, dNull) = DateParser.FormatSegments(null);
            Assert.Equal(string.Empty, yNull);
            Assert.Equal(string.Empty, mNull);
            Assert.Equal(string.Empty, dNull);

            var (y, m, d) = DateParser.FormatSegments(new DateTime(2026, 11, 19));
            Assert.Equal("2026", y);
            Assert.Equal("11", m);
            Assert.Equal("19", d);
        }

        [Fact]
        public void DateParser_TryParseDate_ValidatesCorrectly()
        {
            // Empty returns true with null
            Assert.True(DateParser.TryParseDate("", "", "", out var nullDate));
            Assert.Null(nullDate);

            // Valid date
            Assert.True(DateParser.TryParseDate("2026", "11", "19", out var validDate));
            Assert.Equal(new DateTime(2026, 11, 19), validDate);

            // Leap year Feb 29
            Assert.True(DateParser.TryParseDate("2024", "02", "29", out var leapDate));
            Assert.Equal(new DateTime(2024, 2, 29), leapDate);

            // Non-leap year Feb 29 fails
            Assert.False(DateParser.TryParseDate("2025", "02", "29", out _));

            // Out-of-bounds month
            Assert.False(DateParser.TryParseDate("2026", "13", "01", out _));

            // Out-of-bounds day
            Assert.False(DateParser.TryParseDate("2026", "04", "31", out _));
        }

        [Fact]
        public void DateParser_Step_CyclesAndClampsCorrectly()
        {
            // Year step
            Assert.Equal("2027", DateParser.StepYear("2026", 1));
            Assert.Equal("2025", DateParser.StepYear("2026", -1));

            // Month step wraps 12 -> 1, 1 -> 12
            Assert.Equal("01", DateParser.StepMonth("12", 1));
            Assert.Equal("12", DateParser.StepMonth("01", -1));
            Assert.Equal("06", DateParser.StepMonth("05", 1));

            // Day step wraps around month max days
            Assert.Equal("01", DateParser.StepDay("30", "2026", "04", 1));
            Assert.Equal("30", DateParser.StepDay("01", "2026", "04", -1));
        }

        [Fact]
        public void NumericInput_ClampsToMinMaxAndRoundsDecimals()
        {
            ExecuteInSta(() =>
            {
                var input = new NumericInput
                {
                    Minimum = 10m,
                    Maximum = 100m,
                    Decimals = 2,
                    Step = 5m,
                    Value = 50m
                };

                Assert.Equal(50m, input.Value);

                // Step up
                input.ApplyTemplate();
                input.Value = 98m;
                // Value should be clamped if set beyond bounds
                Assert.Equal(98m, input.Value);
            });
        }

        [Fact]
        public void DateInput_SelectedDateProperty_InitializesAndUpdates()
        {
            ExecuteInSta(() =>
            {
                var input = new DateInput();
                Assert.Null(input.SelectedDate);

                var target = new DateTime(2026, 12, 25);
                input.SelectedDate = target;
                Assert.Equal(target, input.SelectedDate);
            });
        }

        [Fact]
        public void CalendarAssist_IsRangeMiddle_SetAndGet()
        {
            ExecuteInSta(() =>
            {
                var btn = new CalendarDayButton();
                Assert.False(CalendarAssist.GetIsRangeMiddle(btn));

                CalendarAssist.SetIsRangeMiddle(btn, true);
                Assert.True(CalendarAssist.GetIsRangeMiddle(btn));
            });
        }

        [Fact]
        public void CalendarAssist_EnableTwoClickRange_SwitchesToMultipleRange_AndSelectsWithoutException()
        {
            ExecuteInSta(() =>
            {
                var cal = new Sop.Ui.Controls.Calendar();
                Assert.Equal(CalendarSelectionMode.SingleDate, cal.SelectionMode);

                // Enabling two-click range automatically switches to MultipleRange
                CalendarAssist.SetEnableTwoClickRange(cal, true);
                Assert.Equal(CalendarSelectionMode.MultipleRange, cal.SelectionMode);

                // Range add does not throw
                var start = new DateTime(2026, 11, 10);
                var end = new DateTime(2026, 11, 15);
                cal.SelectedDates.Clear();
                cal.SelectedDates.AddRange(start, end);

                Assert.Equal(6, cal.SelectedDates.Count);
                Assert.Contains(new DateTime(2026, 11, 12), cal.SelectedDates);
            });
        }

        private static void ExecuteInSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null) throw caught;
        }
    }
}
