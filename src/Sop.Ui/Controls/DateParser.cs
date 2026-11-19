using System;
using System.Globalization;

namespace Sop.Ui.Controls
{
    public static class DateParser
    {
        public const int MinYear = 1900;
        public const int MaxYear = 2100;

        public static (string year, string month, string day) FormatSegments(DateTime? date)
        {
            if (!date.HasValue) return (string.Empty, string.Empty, string.Empty);
            var d = date.Value;
            return (
                d.Year.ToString("0000", CultureInfo.InvariantCulture),
                d.Month.ToString("00", CultureInfo.InvariantCulture),
                d.Day.ToString("00", CultureInfo.InvariantCulture)
            );
        }

        public static bool TryParseDate(string? yearText, string? monthText, string? dayText, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrEmpty(yearText) && string.IsNullOrEmpty(monthText) && string.IsNullOrEmpty(dayText))
            {
                date = null;
                return true;
            }

            if (int.TryParse(yearText, out int y) &&
                int.TryParse(monthText, out int m) &&
                int.TryParse(dayText, out int d))
            {
                if (y >= MinYear && y <= MaxYear && m >= 1 && m <= 12)
                {
                    int maxDays = DateTime.DaysInMonth(y, m);
                    if (d >= 1 && d <= maxDays)
                    {
                        date = new DateTime(y, m, d);
                        return true;
                    }
                }
            }

            return false;
        }

        public static int GetMaxDays(string? yearText, string? monthText)
        {
            if (int.TryParse(yearText, out int y) && int.TryParse(monthText, out int m) && m >= 1 && m <= 12)
            {
                int validYear = Math.Max(MinYear, Math.Min(MaxYear, y));
                return DateTime.DaysInMonth(validYear, m);
            }
            return 31;
        }

        public static string StepYear(string? currentYearText, int delta)
        {
            if (!int.TryParse(currentYearText, out int y)) y = DateTime.Today.Year;
            int next = Math.Max(MinYear, Math.Min(MaxYear, y + delta));
            return next.ToString("0000", CultureInfo.InvariantCulture);
        }

        public static string StepMonth(string? currentMonthText, int delta)
        {
            if (!int.TryParse(currentMonthText, out int m)) m = DateTime.Today.Month;
            int next = m + delta;
            if (next > 12) next = 1;
            else if (next < 1) next = 12;
            return next.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string StepDay(string? currentDayText, string? yearText, string? monthText, int delta)
        {
            int maxDays = GetMaxDays(yearText, monthText);
            if (!int.TryParse(currentDayText, out int d)) d = 1;
            int next = d + delta;
            if (next > maxDays) next = 1;
            else if (next < 1) next = maxDays;
            return next.ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool IsNumeric(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }
    }
}
