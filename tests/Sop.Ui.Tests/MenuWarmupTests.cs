using System;
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
    /// The first popup in a process is expensive — its own window, the panel
    /// and row templates, and the jitting of all of it. The app warms one up
    /// while it is idle so the cost never lands on a user's first click.
    /// </summary>
    public class MenuWarmupTests
    {
        private readonly ITestOutputHelper _output;

        public MenuWarmupTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WpfFact]
        public void Warming_the_menu_absorbs_the_cost_of_the_first_dropdown()
        {
            if (Application.Current == null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Theme.Initialize(app);
            }
            Theme.Mode = ThemeMode.Light;
            Theme.IsRtl = false;

            var select = new Select
            {
                Width = 320,
                Options = Enumerable.Range(0, 40).Select(i => new SelectOption("v" + i, "Option " + i)).ToList(),
            };
            var window = new Window
            {
                Left = -20000,
                Top = -20000,
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = new Grid { Children = { select } },
            };
            window.Show();
            window.UpdateLayout();
            try
            {
                Sop.Ui.Controls.Menu.Prewarm(window);
                // Prewarm runs when the app goes idle; wait for that to happen.
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.SystemIdle);

                // A real menu takes the mouse while it is open. The warm-up one
                // must not: the click it swallowed would be the user's.
                Assert.Null(System.Windows.Input.Mouse.Captured);

                var menu = (Sop.Ui.Controls.Menu)select.Template.FindName("PART_Menu", select);
                var clock = Stopwatch.StartNew();
                menu.IsOpen = true;
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
                var ms = clock.Elapsed.TotalMilliseconds;
                menu.IsOpen = false;

                _output.WriteLine("first open after warm-up=" + ms.ToString("0.0") + "ms");

                // Cold — with nothing warmed — this is around half a second.
                Assert.True(ms < 250, "the first dropdown took " + ms.ToString("0") + "ms after warm-up");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
