using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sop.Ui;
using Sop.Ui.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// Switching to Arabic on a window that is already open.
    ///
    /// Reported from a running till: after changing the language the window
    /// came up with every glyph drawn back to front, while the sidebar and the
    /// caption buttons stayed where they had been. Two mirrors, one cancelling
    /// the other in the arrangement and both landing on the text.
    ///
    /// The invariant that has to hold: switching the language must leave the
    /// window looking exactly as it would have looked had it opened in that
    /// language. Anything else is a difference the user can see.
    /// </summary>
    public class RtlToggleTests
    {
        private static readonly object Gate = new object();
        private readonly ITestOutputHelper _output;

        public RtlToggleTests(ITestOutputHelper output)
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
        }

        private static void Pump(Window window)
        {
            for (var i = 0; i < 4; i++)
            {
                window.UpdateLayout();
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Loaded);
                window.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
            }
        }

        /// <summary>Content with letters that read differently mirrored, and a left-anchored strip.</summary>
        private static FrameworkElement Content()
        {
            var strip = new Border
            {
                Width = 48,
                Background = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var label = new TextBlock
            {
                Text = "Administrator",
                FontSize = 20,
                Margin = new Thickness(60, 8, 8, 8),
                Foreground = Brushes.Black,
            };
            var grid = new Grid { Background = Brushes.White, Width = 400, Height = 80 };
            grid.Children.Add(strip);
            grid.Children.Add(label);
            return grid;
        }

        private static byte[] Shot(FrameworkElement element)
        {
            var bitmap = new RenderTargetBitmap(400, 80, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var pixels = new byte[400 * 80 * 4];
            bitmap.CopyPixels(pixels, 400 * 4, 0);
            return pixels;
        }

        /// <summary>
        /// The caption belongs to Windows and follows the system's language;
        /// the app's language governs what the app draws. Setting a Window's
        /// own FlowDirection mirrors the native caption — and on some machines
        /// the whole window's pixels — so it must stay left to right whatever
        /// the register is switched to.
        /// </summary>
        [WpfFact]
        public void The_caption_is_left_to_the_system_whatever_the_app_language()
        {
            EnsureApp();
            Theme.IsRtl = false;
            var (window, root) = Open();
            try
            {
                Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
                Assert.Equal(FlowDirection.LeftToRight, root.FlowDirection);

                Theme.IsRtl = true;
                Pump(window);

                // What the app draws turned round…
                Assert.Equal(FlowDirection.RightToLeft, root.FlowDirection);
                // …and the window Windows draws the caption on did not.
                Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
            }
            finally
            {
                Theme.IsRtl = false;
                window.Close();
            }
        }

        private static (Window Window, FrameworkElement Root) Open()
        {
            var root = Content();
            var window = new AppWindow
            {
                Left = -20000, Top = -20000, Width = 600, Height = 300,
                ShowInTaskbar = false, ShowActivated = false,
                Title = "Pharmacy",
                Content = root,
            };
            Theme.Attach(window);
            window.Show();
            Pump(window);
            return (window, root);
        }

        private static int Differing(byte[] a, byte[] b)
        {
            var count = 0;
            for (var i = 0; i < a.Length; i += 4)
            {
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) count++;
            }
            return count;
        }

        /// <summary>
        /// Windows hands a mirrored layout DOWN: a window created while
        /// another one is mirrored inherits WS_EX_LAYOUTRTL unless it says
        /// otherwise, and WPF then mirrors it a second time for its own
        /// right-to-left layout. Two mirrors cancel in the arrangement and
        /// land on the glyphs — which is what was reported.
        /// </summary>
        [WpfFact]
        public void A_window_opened_while_another_is_in_arabic_is_not_mirrored_twice()
        {
            EnsureApp();

            Theme.IsRtl = true;
            var (first, firstRoot) = Open();
            try
            {
                var wanted = Shot(firstRoot);

                // A second window, born while the first is already Arabic.
                var (second, secondRoot) = Open();
                try
                {
                    var got = Shot(secondRoot);
                    var differing = Differing(wanted, got);
                    var fraction = differing / (400.0 * 80.0);
                    _output.WriteLine($"second window differing pixels {differing} ({fraction:P2})");
                    Assert.True(fraction < 0.001,
                        $"a window opened while another was in Arabic drew differently: {fraction:P1} of pixels differ");
                }
                finally
                {
                    second.Close();
                }
            }
            finally
            {
                Theme.IsRtl = false;
                first.Close();
            }
        }

        [WpfFact]
        public void Switching_language_leaves_the_window_as_if_it_had_opened_that_way()
        {
            EnsureApp();

            // Opened straight into Arabic — the arrangement the captures prove.
            Theme.IsRtl = true;
            var (opened, openedRoot) = Open();
            byte[] wanted;
            try
            {
                wanted = Shot(openedRoot);
            }
            finally
            {
                opened.Close();
            }

            // Opened in English, then switched — what the till actually does.
            Theme.IsRtl = false;
            var (toggled, toggledRoot) = Open();
            try
            {
                Theme.IsRtl = true;
                Pump(toggled);
                var got = Shot(toggledRoot);

                var differing = Differing(wanted, got);
                var fraction = differing / (400.0 * 80.0);
                _output.WriteLine($"differing pixels {differing} ({fraction:P2})");
                Assert.True(fraction < 0.001,
                    $"switching to Arabic drew a different window than opening in it: {fraction:P1} of pixels differ");
            }
            finally
            {
                Theme.IsRtl = false;
                toggled.Close();
            }
        }
    }
}
