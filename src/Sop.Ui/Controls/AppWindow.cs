using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// A window wearing the register's own chrome instead of Windows'.
    ///
    /// The stock caption bar is painted by the system in the system's colours:
    /// on a dark register it sits above the app as a white strip that no theme
    /// token reaches. This draws the bar itself — mark, title, and the three
    /// caption buttons — inside a <see cref="WindowChrome"/> that keeps every
    /// behaviour a window is expected to have: drag to move, double-click to
    /// maximise, snap to an edge, resize from any border, and the system menu
    /// on right-click or Alt+Space.
    ///
    /// The bar lives in the TEMPLATE, not in <see cref="ContentControl.Content"/>,
    /// so what a screen renders — and what the visual tests photograph — is
    /// exactly what it was before.
    /// </summary>
    public class AppWindow : Window
    {
        static AppWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AppWindow), new FrameworkPropertyMetadata(typeof(AppWindow)));
        }

        public AppWindow()
        {
            // Windows draws the caption, on every machine.
            //
            // This used to draw its own, for a title bar in the register's own
            // colours rather than the system's white strip. It cost more than
            // it bought: the bar had to be kept off-screen edges by hand when
            // maximised, its buttons swapped sides with the language, and on a
            // desktop with no composition the system drew over it and left the
            // window with no way to close. Emptied of its mark and its title at
            // the owner's request it was a blank strip above the app, and the
            // owner asked for it gone — so the system's bar it is, in the place
            // and the direction the rest of Windows puts it.
            UsesOwnChrome = false;

            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, __) => SystemCommands.MinimizeWindow(this)));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, __) => SystemCommands.MaximizeWindow(this)));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, __) => SystemCommands.RestoreWindow(this)));
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, __) => SystemCommands.CloseWindow(this)));
            CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, (_, e) => ShowSystemMenu(e)));
        }

        /// <summary>
        /// Whether this window draws its own caption bar.
        ///
        /// It does on any modern desktop. It does NOT where the desktop
        /// composition engine is off — Windows 7 with Aero disabled, and the
        /// Starter and Home Basic editions, which have no Aero at all. There
        /// the system draws its frame regardless of what the window asks for,
        /// and it lands on top of the app's own bar: the register comes up
        /// with no close, minimise or maximise button at all (reported from a
        /// 32-bit Windows 7 till, 7 Sept 2026). On those machines the window
        /// wears the system's caption instead and the template hides its own,
        /// so there is exactly one bar and its buttons work.
        /// </summary>
        public static readonly DependencyProperty UsesOwnChromeProperty = DependencyProperty.Register(
            nameof(UsesOwnChrome), typeof(bool), typeof(AppWindow), new FrameworkPropertyMetadata(true));

        public bool UsesOwnChrome
        {
            get => (bool)GetValue(UsesOwnChromeProperty);
            private set => SetValue(UsesOwnChromeProperty, value);
        }

        /// <summary>
        /// Is the desktop composited? Windows 8 and later always are; Windows 7
        /// answers for itself. A machine with no dwmapi at all answers no.
        /// </summary>
        public static bool DesktopIsComposited()
        {
            try
            {
                return DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

        /// <summary>
        /// A window may not open taller or wider than the screen it opens on.
        ///
        /// The register asks for 1280×800, which is more than a 1024×768 till
        /// has; centred, that hangs the top of the window — the caption bar,
        /// and every button on it — above the top of the screen. Clamping here,
        /// before the window is shown, also lets the centring use the size it
        /// will actually have.
        /// </summary>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            // MinWidth/MinHeight still win where the screen is smaller than the
            // register's own minimum — WPF enforces them — but then the window
            // is bigger than the screen because it was asked to be, not because
            // nobody looked.
            var work = SystemParameters.WorkArea;
            if (work.Width <= 0 || work.Height <= 0) return;
            if (Width > work.Width) Width = work.Width;
            if (Height > work.Height) Height = work.Height;
        }

        /// <summary>
        /// Room a MAXIMISED window has to leave for its own resize border.
        /// Windows sizes a maximised WindowChrome window to the work area plus
        /// that border on every side, so without this the top of the bar — and
        /// the caption buttons with it — sits off-screen.
        /// </summary>
        public static readonly DependencyProperty MaximisedPadProperty = DependencyProperty.Register(
            nameof(MaximisedPad), typeof(Thickness), typeof(AppWindow),
            new FrameworkPropertyMetadata(new Thickness(0)));

        public Thickness MaximisedPad
        {
            get => (Thickness)GetValue(MaximisedPadProperty);
            private set => SetValue(MaximisedPadProperty, value);
        }

        protected override void OnStateChanged(System.EventArgs e)
        {
            base.OnStateChanged(e);
            SyncMaximisedPad();
            if (WindowState == WindowState.Minimized) ReleaseIdleMemory();
        }

        /// <summary>
        /// The overhang is measured from the window's own rectangle, and that
        /// rectangle is not final when the state first changes — nor after a
        /// move to a second screen, or a change of scaling. Re-measure on each.
        /// Setting the pad resizes what is INSIDE the window, never the window,
        /// so none of these can feed back into themselves.
        /// </summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            SyncMaximisedPad();
        }

        protected override void OnLocationChanged(System.EventArgs e)
        {
            base.OnLocationChanged(e);
            SyncMaximisedPad();
        }

        protected override void OnDpiChanged(DpiScale before, DpiScale after)
        {
            base.OnDpiChanged(before, after);
            SyncMaximisedPad();
        }

        /// <summary>
        /// Hand back what the window is not using while it is out of the way.
        ///
        /// A till spends much of its day on WPF's own machinery — templates,
        /// render data, glyph caches — and the pages behind it stay committed
        /// long after the screen that needed them was left. None of it is
        /// leaked, and Windows would reclaim it under pressure anyway, but a
        /// minimised register has no business sitting on a third of a gigabyte
        /// of a four-gigabyte machine. Windows pages back whatever is touched
        /// again when the window comes up.
        /// </summary>
        private static void ReleaseIdleMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                using var self = System.Diagnostics.Process.GetCurrentProcess();
                EmptyWorkingSet(self.Handle);
            }
            catch (System.Exception)
            {
                // Trimming is a courtesy to the rest of the machine; never let
                // it be the reason a window fails to minimise.
            }
        }

        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr process);

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            SyncMaximisedPad();
        }

        /// <summary>
        /// The template's outer border, which carries the pad.
        ///
        /// The pad is put on it HERE rather than bound in the template: the
        /// template's <c>{Binding MaximisedPad, RelativeSource TemplatedParent}</c>
        /// on the root never attached — the element came back with no binding
        /// expression on Margin at all — so the compensation silently did
        /// nothing and a maximised window hung off every edge of the screen.
        /// </summary>
        private FrameworkElement? _frame;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _frame = GetTemplateChild("Frame") as FrameworkElement;
            // The bar shows no mark to click any more, so the system menu is
            // reached the way it is on any window without one: right-click.
            if (GetTemplateChild("TitleBar") is FrameworkElement bar)
            {
                bar.MouseRightButtonUp -= OnTitleBarRightClick;
                bar.MouseRightButtonUp += OnTitleBarRightClick;
            }
            ApplyPad();
        }

        private void SyncMaximisedPad()
        {
            // Nothing to compensate for when Windows draws the frame itself.
            if (!UsesOwnChrome || WindowState != WindowState.Maximized)
            {
                if (MaximisedPad != default) MaximisedPad = new Thickness(0);
                ApplyPad();
                return;
            }
            var pad = FrameOversize();
            if (MaximisedPad != pad) MaximisedPad = pad;
            ApplyPad();
        }

        private void ApplyPad()
        {
            if (_frame == null) return;
            // The overhang is measured in SCREEN space; a Margin is read in
            // FLOW space, so under Arabic its left and right mean the other
            // sides. Symmetric pads do not care, a window snapped against one
            // edge does — and it would pad the wrong side of the screen.
            var pad = MaximisedPad;
            if (FlowDirection == FlowDirection.RightToLeft)
                pad = new Thickness(pad.Right, pad.Top, pad.Left, pad.Bottom);
            if (_frame.Margin != pad) _frame.Margin = pad;
        }

        /// <summary>
        /// How far outside its screen Windows has actually put this maximised
        /// window, per side, in device-independent units.
        ///
        /// MEASURED, not derived. It was derived once — SM_CXSIZEFRAME plus
        /// SM_CXPADDEDBORDER — and that came to exactly half the real overhang
        /// on a 125% display (7.2 DIP against 14.4), so the right-hand column
        /// of every table and the bottom of every pager hung off the screen on
        /// a maximised till. The window's own rectangle against its monitor's
        /// work area cannot be wrong about it, at any scale factor, and it
        /// reads the monitor the window is ON rather than the primary one.
        /// </summary>
        private Thickness FrameOversize()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return Guessed();
            if (!GetWindowRect(hwnd, out var window)) return Guessed();
            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return Guessed();

            var work = info.Work;
            // Only the sides that actually hang over: a window snapped against
            // a taskbar edge overhangs there and not opposite it.
            double left = Math.Max(0, work.Left - window.Left);
            double top = Math.Max(0, work.Top - window.Top);
            double right = Math.Max(0, window.Right - work.Right);
            double bottom = Math.Max(0, window.Bottom - work.Bottom);
            if (left + top + right + bottom <= 0) return new Thickness(0);

            // Those are physical pixels; the margin is laid out in DIPs.
            var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                        ?? System.Windows.Media.Matrix.Identity;
            return new Thickness(left * toDip.M11, top * toDip.M22, right * toDip.M11, bottom * toDip.M22);
        }

        /// <summary>
        /// Before the window has a handle there is nothing to measure. The
        /// resize border we asked for is the closest thing to an answer, and
        /// a later size or location change re-measures properly anyway.
        /// </summary>
        private Thickness Guessed()
        {
            var border = WindowChrome.GetWindowChrome(this)?.ResizeBorderThickness.Left ?? 0;
            return new Thickness(border);
        }

        private const int MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        /// <summary>The system menu, at the pointer — right-click on the bar, or the mark.</summary>
        /// <summary>
        /// The bar carries no mark to click any more, so the system menu is
        /// reached the way it is on any window without one: right-click the
        /// caption. Move, Size, Maximise and Close all still live there.
        /// </summary>
        private void OnTitleBarRightClick(object sender, MouseButtonEventArgs e)
        {
            SystemCommands.ShowSystemMenu(this, PointToScreen(e.GetPosition(this)));
            e.Handled = true;
        }

        private void ShowSystemMenu(ExecutedRoutedEventArgs e)
        {
            var origin = e.Parameter is Point point
                ? PointToScreen(point)
                : PointToScreen(new Point(0, WindowChrome.GetWindowChrome(this)?.CaptionHeight ?? 0));
            SystemCommands.ShowSystemMenu(this, origin);
        }
    }
}
