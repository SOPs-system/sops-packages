using System;
using System.IO;
using System.Windows;
using Sop.Ui;

namespace Sop.Gallery
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gallery_error.log"), args.ExceptionObject.ToString());
            };
            DispatcherUnhandledException += (s, args) =>
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gallery_error.log"), args.Exception.ToString());
            };

            base.OnStartup(e);
            Theme.Initialize(this);

            var window = new MainWindow();
            Theme.Attach(window);

            var captureAt = Array.IndexOf(e.Args, "--capture");
            if (captureAt >= 0 && captureAt + 1 < e.Args.Length)
            {
                // Headless-ish run for the visual-regression test: render every
                // mode to PNG and exit. Exit code 0 = every capture written.
                var dir = e.Args[captureAt + 1];
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                // Pin the moving parts. The spinner's ring is at whatever
                // angle the frame catches it, which made these baselines fail
                // on a busy machine and pass on an idle one.
                Motion.Enabled = false;
                // Start off-screen: the compared shots are rendered through a
                // VisualBrush, and a real pointer resting over the window would
                // paint a hover state into one run and not the next. Capture
                // moves it on-screen for the popup grabs, which need real pixels.
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -20000;
                window.Top = -20000;
                window.ShowActivated = false;
                // Fixed size: the compared shots go pixel for pixel against a
                // baseline, and Windows will otherwise resize a window to fit
                // whatever work area it decides the window belongs to.
                window.ResizeMode = ResizeMode.NoResize;
                window.Show();
                try
                {
                    await Capture.RunAsync(window, dir);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "capture-error.txt"), ex.ToString());
                    Shutdown(1);
                }
                return;
            }

            MainWindow = window;
            window.Show();
        }
    }
}
