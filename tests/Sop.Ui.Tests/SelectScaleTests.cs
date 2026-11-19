using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sop.Ui;
using Sop.Ui.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// A picker over a large catalog — thousands of drugs, a long supplier
    /// list — has to stay as quick as one over five. These pin the cost of
    /// opening, typing and paging so a regression shows up as a failure
    /// rather than as a user noticing the lag.
    /// </summary>
    public class SelectScaleTests
    {
        private readonly ITestOutputHelper _output;

        public SelectScaleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly object Gate = new object();

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

        private static Window Show(UIElement content)
        {
            var window = new Window
            {
                Left = -20000,
                Top = -20000,
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = content,
            };
            window.Show();
            window.UpdateLayout();
            return window;
        }

        private static List<SelectOption> Catalog(int count) =>
            Enumerable.Range(0, count)
                .Select(i => new SelectOption("id-" + i, "Option " + i.ToString("00000")))
                .ToList();

        /// <summary>
        /// How long the work takes with its layout — a popup is its own visual
        /// tree, so the window's own UpdateLayout would not cover it; the pump
        /// at Render priority does.
        /// </summary>
        private static double Time(Window window, Action work)
        {
            var clock = Stopwatch.StartNew();
            work();
            window.UpdateLayout();
            window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
            return clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Open a picker over <paramref name="size"/> options in its own window, and time it.</summary>
        private static (double Open, double Filter, double Page) Measure(int size)
        {
            var select = new Select { Width = 320 };
            var stack = new StackPanel();
            stack.Children.Add(select);
            var window = Show(new Grid { Children = { stack } });
            try
            {
                select.Options = Catalog(size);
                window.UpdateLayout();
                var menu = (Sop.Ui.Controls.Menu)select.Template.FindName("PART_Menu", select);
                var open = Time(window, () => menu.IsOpen = true);
                // Twenty pages deep — scrolling down a long list. Ten rows a
                // page: growing the page must add those ten, not rebuild every
                // row already on screen.
                var page = Time(window, () => { for (var i = 0; i < 20; i++) select.ShowMore(); });
                menu.IsOpen = false;
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);

                // The search itself, with the list closed: what a keystroke
                // costs in the control rather than in WPF's layout. This is
                // where a catalog of thousands is felt — filtering it is per
                // keystroke, laying out ten rows is not.
                var clock = Stopwatch.StartNew();
                for (var i = 0; i < 20; i++)
                {
                    select.SearchForTest("O");
                    select.SearchForTest("Op");
                    select.SearchForTest("Opt");
                    select.SearchForTest("Opti");
                    select.SearchForTest("Optio");
                }
                var filter = clock.Elapsed.TotalMilliseconds / 100;
                return (open, filter, page);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_picker_over_thousands_of_options_stays_responsive()
        {
            EnsureApp();
            // One throwaway run first: the first popup of a process pays for
            // its own window and for jitting the whole menu path, and that
            // cost belongs to neither the list nor this measurement.
            Measure(20);

            var report = new List<string>();
            var results = new Dictionary<int, (double Open, double Filter, double Page)>();
            foreach (var size in new[] { 20, 3000 })
            {
                var runs = new List<(double Open, double Filter, double Page)>();
                for (var i = 0; i < 3; i++) runs.Add(Measure(size));
                var best = (
                    Open: runs.Min(r => r.Open),
                    Filter: runs.Min(r => r.Filter),
                    Page: runs.Min(r => r.Page));
                results[size] = best;
                report.Add(size + ": open=" + best.Open.ToString("0.0")
                    + " keystroke=" + best.Filter.ToString("0.000")
                    + " page20=" + best.Page.ToString("0.0"));
            }

            foreach (var line in report) _output.WriteLine(line);

            var small = results[20];
            var big = results[3000];
            // Budgets are loose on purpose: they catch the order-of-magnitude
            // regressions, not a slow build agent. A keystroke over a catalog
            // of 3000 cost around 12ms when the control re-projected the whole
            // list and lower-cased every label on each one; it is a fraction of
            // a millisecond when it filters the list it already holds.
            Assert.True(big.Filter < 2, "a keystroke over 3000 options took " + big.Filter.ToString("0.00") + "ms");
            // Opening builds one page, however long the list is.
            Assert.True(big.Open < small.Open * 3 + 40, "opening 3000 options took " + big.Open.ToString("0")
                + "ms against " + small.Open.ToString("0") + "ms for 20");
            // Twenty pages is 200 rows built; rebuilding the whole list on each
            // page instead would be twenty times that.
            Assert.True(big.Page < 600, "twenty pages of 3000 options took " + big.Page.ToString("0") + "ms");
        }
    }
}
