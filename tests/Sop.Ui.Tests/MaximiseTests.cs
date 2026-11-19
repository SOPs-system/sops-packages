using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sop.Ui;
using Sop.Ui.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// A maximised window has to end where the screen ends.
    ///
    /// Windows sizes a maximised WindowChrome window to the work area PLUS its
    /// own resize border on every side, so the outer edges — the caption
    /// buttons at the top right, the last column of a table, the pager along
    /// the bottom — sit off-screen unless the frame is inset by exactly that
    /// much. This measures what is actually drawn against the work area.
    /// </summary>
    public class MaximiseTests
    {
        private static readonly object Gate = new object();
        private readonly ITestOutputHelper _output;

        public MaximiseTests(ITestOutputHelper output)
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

        private static void Pump(Window window)
        {
            // Three passes: measuring the overhang sets a margin, which asks
            // for another layout, and the visual transform PointToScreen reads
            // is only current once that has settled.
            for (var i = 0; i < 3; i++)
            {
                window.UpdateLayout();
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Loaded);
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
            }
        }

        [WpfFact]
        public void A_maximised_window_draws_inside_the_work_area()
        {
            EnsureApp();
            var window = new AppWindow
            {
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = new Border { Background = Brushes.Transparent },
            };
            try
            {
                window.Show();
                Pump(window);
                // The system draws the frame here, so there is nothing to
                // compensate for and nothing this test can say.
                if (!window.UsesOwnChrome) return;

                window.WindowState = WindowState.Maximized;
                Pump(window);

                var frame = window.Template?.FindName("Frame", window) as FrameworkElement;
                Assert.NotNull(frame);

                var topLeft = frame!.PointToScreen(new Point(0, 0));
                var bottomRight = frame.PointToScreen(new Point(frame.ActualWidth, frame.ActualHeight));
                // PointToScreen gives physical pixels; the work area is in DIPs.
                var toDip = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice
                            ?? Matrix.Identity;
                topLeft = toDip.Transform(topLeft);
                bottomRight = toDip.Transform(bottomRight);
                var work = SystemParameters.WorkArea;

                var windowTopLeft = toDip.Transform(window.PointToScreen(new Point(0, 0)));
                _output.WriteLine($"work {work.Left},{work.Top} {work.Width}x{work.Height}");
                _output.WriteLine($"window at {windowTopLeft.X},{windowTopLeft.Y} size {window.ActualWidth}x{window.ActualHeight}");
                _output.WriteLine($"drawn {topLeft.X},{topLeft.Y} to {bottomRight.X},{bottomRight.Y}");
                var bound = System.Windows.Data.BindingOperations.GetBindingExpression(frame, FrameworkElement.MarginProperty);
                _output.WriteLine($"frame {frame.ActualWidth}x{frame.ActualHeight} margin {frame.Margin}");
                _output.WriteLine($"margin binding: {(bound == null ? "NONE" : bound.Status + " parent=" + (bound.ResolvedSource?.GetType().Name ?? "null"))}");
                _output.WriteLine($"pad {window.MaximisedPad}");

                // A pixel of slack for rounding at fractional scale factors.
                const double Slack = 1.0;
                Assert.True(topLeft.X >= work.Left - Slack,
                    $"the left edge is {work.Left - topLeft.X:0.0} DIP off-screen");
                Assert.True(topLeft.Y >= work.Top - Slack,
                    $"the top edge is {work.Top - topLeft.Y:0.0} DIP off-screen");
                Assert.True(bottomRight.X <= work.Right + Slack,
                    $"the right edge is {bottomRight.X - work.Right:0.0} DIP off-screen");
                Assert.True(bottomRight.Y <= work.Bottom + Slack,
                    $"the bottom edge is {bottomRight.Y - work.Bottom:0.0} DIP off-screen");

                // …and it must actually FILL it, not sit inset by a guess:
                // a maximised till that leaves a visible gutter looks broken
                // in the other direction.
                const double Gutter = 2.0;
                Assert.True(work.Left - topLeft.X >= -Gutter && topLeft.X - work.Left <= Gutter,
                    $"the left edge is inset by {topLeft.X - work.Left:0.0} DIP");
                Assert.True(bottomRight.X - work.Right >= -Gutter && work.Right - bottomRight.X <= Gutter,
                    $"the right edge is inset by {work.Right - bottomRight.X:0.0} DIP");
                Assert.True(bottomRight.Y - work.Bottom >= -Gutter && work.Bottom - bottomRight.Y <= Gutter,
                    $"the bottom edge is inset by {work.Bottom - bottomRight.Y:0.0} DIP");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
