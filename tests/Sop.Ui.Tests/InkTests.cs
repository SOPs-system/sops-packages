using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sop.Ui;
using Xunit;
using Xunit.Abstractions;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// A code in a chip has to sit in the MIDDLE of it.
    ///
    /// The device-code chips stand in a horizontal row next to a Copy button,
    /// and a horizontal StackPanel stretches its children to the height of the
    /// tallest — the button. So the chip grew to the button's height while the
    /// text inside it kept rendering at the top of the space, leaving 10
    /// pixels above the code and 17 below it on the approved support screen.
    /// The fix is one alignment; this is the shape that has to keep holding.
    /// </summary>
    public class InkTests
    {
        private static readonly object Gate = new object();
        private readonly ITestOutputHelper _output;

        public InkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static void EnsureApp()
        {
            lock (Gate)
            {
                if (Application.Current == null)
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    Theme.Initialize(app);
                }
            }
            Theme.Mode = ThemeMode.Light;
            Theme.IsRtl = false;
        }

        /// <summary>Where the drawn letters sit inside the chip: space above them, space below.</summary>
        private static (double Above, double Below) Gaps(Border chip, TextBlock text)
        {
            var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
            var formatted = new FormattedText(
                text.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, text.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
            var ink = formatted.BuildGeometry(new Point(0, 0)).Bounds;

            // Top-left of the text's own box, in the chip's coordinates.
            var origin = text.TranslatePoint(new Point(0, 0), chip);
            var inkTop = origin.Y + ink.Top;
            var inkBottom = origin.Y + ink.Bottom;
            return (inkTop, chip.ActualHeight - inkBottom);
        }

        /// <summary>The chip as the settings screens build it: a code beside a Copy button.</summary>
        private static (Window Window, Border Chip, TextBlock Text) Row(VerticalAlignment alignment)
        {
            var mono = Application.Current.TryFindResource("Font.Mono") as FontFamily ?? new FontFamily("Consolas");
            var size = Application.Current.TryFindResource("Text.Sm") as double? ?? 13.0;
            var text = new TextBlock
            {
                Text = "AB12-CD34-EF56-7890",
                FontFamily = mono,
                FontSize = size,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = alignment,
            };
            var chip = new Border { Padding = new Thickness(12, 6, 12, 6), Child = text };
            // The tall sibling that stretches the row — a real Copy button.
            var copy = new Sop.Ui.Controls.Button { Content = "Copy", Margin = new Thickness(8, 0, 0, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            row.Children.Add(chip);
            row.Children.Add(copy);

            var window = new Window
            {
                Left = -20000, Top = -20000, Width = 500, Height = 200,
                ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None,
                Content = new Grid { Children = { row } },
            };
            window.Show();
            window.UpdateLayout();
            return (window, chip, text);
        }

        [WpfFact]
        public void A_code_sits_in_the_middle_of_its_chip()
        {
            EnsureApp();

            // What it looked like before: the text left at the top of a chip
            // stretched to the button beside it.
            var (stretchedWindow, stretchedChip, stretchedText) = Row(VerticalAlignment.Stretch);
            try
            {
                var before = Gaps(stretchedChip, stretchedText);
                _output.WriteLine($"chip {stretchedChip.ActualHeight:0.##} tall");
                _output.WriteLine($"stretched: above {before.Above:0.##} below {before.Below:0.##}");
                // The fault this test exists for is real and several pixels
                // wide, not a rounding error.
                Assert.True(before.Below - before.Above > 2,
                    $"nothing to correct: above {before.Above:0.##}, below {before.Below:0.##}");
            }
            finally
            {
                stretchedWindow.Close();
            }

            var (window, chip, text) = Row(VerticalAlignment.Center);
            try
            {
                var after = Gaps(chip, text);
                _output.WriteLine($"centred:   above {after.Above:0.##} below {after.Below:0.##}");
                // Half a pixel: the eye cannot see less, and snapping to the
                // device pixel grid moves it by that much anyway.
                Assert.True(Math.Abs(after.Above - after.Below) <= 0.5,
                    $"the code sits {Math.Abs(after.Above - after.Below):0.0} DIP off centre "
                    + $"(above {after.Above:0.##}, below {after.Below:0.##})");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
