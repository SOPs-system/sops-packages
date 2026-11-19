using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sop.Ui;

namespace Sop.Gallery
{
    /// <summary>
    /// Renders the gallery to PNGs for the visual-regression test:
    /// the page in light/dark × LTR/RTL, and the open modal in light and dark.
    /// File names are the contract with tests/Sop.Ui.Tests/VisualTests.cs.
    /// </summary>
    internal static class Capture
    {
        public static readonly string[] Names =
        {
            "gallery-light-ltr", "gallery-dark-ltr", "gallery-light-rtl", "gallery-dark-rtl",
            "modal-light-ltr", "modal-dark-ltr", "modal-light-rtl",
        };

        /// <summary>
        /// The window's size for every compared shot — Windows will otherwise
        /// resize a window to fit a work area, and a shot at a different size
        /// fails the comparison as "size changed" with nothing visibly wrong.
        /// </summary>
        private const double PageWidth = 1280;
        private const double PageHeight = 900;

        public static async Task RunAsync(MainWindow window, string dir)
        {
            Directory.CreateDirectory(dir);
            var root = (FrameworkElement)window.Content;

            await Page(window, dir, "gallery-light-ltr", ThemeMode.Light, rtl: false);
            await Page(window, dir, "gallery-dark-ltr", ThemeMode.Dark, rtl: false);
            await Page(window, dir, "gallery-light-rtl", ThemeMode.Light, rtl: true);
            await Page(window, dir, "gallery-dark-rtl", ThemeMode.Dark, rtl: true);

            await ModalShot(window, root, dir, "modal-light-ltr", ThemeMode.Light, rtl: false);
            await ModalShot(window, root, dir, "modal-dark-ltr", ThemeMode.Dark, rtl: false);
            await ModalShot(window, root, dir, "modal-light-rtl", ThemeMode.Light, rtl: true);

            // Review-only screen grabs of the popup surfaces (not compared by
            // the visual test: they depend on where the window sits on screen).
            // These DO need real pixels, so the window comes back on-screen —
            // after every compared shot is already taken.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - window.Width) / 2);
            window.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - window.Height) / 2);
            await Settle(window);
            Theme.Mode = ThemeMode.Light;
            Theme.IsRtl = false;
            await PopupShot(window, dir, "review-menu-light-ltr", () => window.OpenSortForCapture());
            await PopupShot(window, dir, "review-select-light-ltr", () => window.OpenCategoryForCapture());
            await PopupShot(window, dir, "review-context-light-ltr", () => window.OpenContextForCapture());
            Theme.IsRtl = true;
            await PopupShot(window, dir, "review-menu-light-rtl", () => window.OpenSortForCapture());
            await PopupShot(window, dir, "review-context-light-rtl", () => window.OpenContextForCapture());
            Theme.Mode = ThemeMode.Dark;
            await PopupShot(window, dir, "review-select-dark-rtl", () => window.OpenCategoryForCapture());

            Theme.Mode = ThemeMode.Light;
            Theme.IsRtl = false;
        }

        private static async Task PopupShot(MainWindow window, string dir, string name, Action open)
        {
            await Settle(window);
            window.Activate();
            open();
            await Settle(window, 300, scrollTop: false);
            ScreenGrab(window, Path.Combine(dir, name + ".png"));
            File.WriteAllLines(Path.Combine(dir, name + ".layout.txt"), window.DescribeOpenPopups());
            window.ClosePopupsForCapture();
            await Settle(window);
        }

        /// <summary>Copy the window's on-screen pixels (popups included) to a PNG.</summary>
        private static void ScreenGrab(Window window, string path)
        {
            var source = PresentationSource.FromVisual(window) as System.Windows.Interop.HwndSource;
            var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var left = (int)Math.Round(window.Left * scale);
            var top = (int)Math.Round(window.Top * scale);
            var w = (int)Math.Round(window.ActualWidth * scale);
            var h = (int)Math.Round(window.ActualHeight * scale);
            using var bitmap = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(w, h));
            }
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        /// <summary>Put the window back to the size the baselines were taken at.</summary>
        private static void PinSize(Window window)
        {
            if (window.Width == PageWidth && window.Height == PageHeight) return;
            window.Width = PageWidth;
            window.Height = PageHeight;
            window.UpdateLayout();
        }

        private static async Task Page(MainWindow window, string dir, string name, ThemeMode mode, bool rtl)
        {
            Theme.Mode = mode;
            Theme.IsRtl = rtl;
            PinSize(window);
            await Settle(window);
            Save(window.PageContent, window.Background, Path.Combine(dir, name + ".png"));
            DumpLayout(window, Path.Combine(dir, name + ".layout.txt"));
        }

        /// <summary>Widths and offsets of the page and its sections, for layout debugging.</summary>
        private static void DumpLayout(MainWindow window, string path)
        {
            var page = window.PageContent;
            var lines = new System.Collections.Generic.List<string>
            {
                $"window client {((FrameworkElement)window.Content).ActualWidth:0}×{((FrameworkElement)window.Content).ActualHeight:0}",
                $"page {page.ActualWidth:0}×{page.ActualHeight:0} at {page.TranslatePoint(new Point(), window)}",
            };
            if (page is System.Windows.Controls.Panel panel)
            {
                foreach (FrameworkElement child in panel.Children)
                {
                    lines.Add($"  {child.GetType().Name} {child.ActualWidth:0}×{child.ActualHeight:0} desired {child.DesiredSize.Width:0} at {child.TranslatePoint(new Point(), page)}");
                    if (child is System.Windows.Controls.Grid grid)
                    {
                        foreach (FrameworkElement cell in grid.Children)
                            lines.Add($"    {cell.GetType().Name} col {System.Windows.Controls.Grid.GetColumn(cell)} {cell.ActualWidth:0} at {cell.TranslatePoint(new Point(), page)}");
                    }
                }
            }
            File.WriteAllLines(path, lines);
        }

        private static async Task ModalShot(MainWindow window, FrameworkElement root, string dir, string name, ThemeMode mode, bool rtl)
        {
            Theme.Mode = mode;
            Theme.IsRtl = rtl;
            PinSize(window);
            await Settle(window);
            window.CaptureModal.IsOpen = true;
            await Settle(window, 400); // let the 150ms entrance finish
            Save(root, window.Background, Path.Combine(dir, name + ".png"));
            window.CaptureModal.IsOpen = false;
            await Settle(window);
        }

        private static async Task Settle(Window window, int delayMs = 150, bool scrollTop = true)
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            await Task.Delay(delayMs);
            if (!scrollTop) return;
            // A selected table row brings itself into view whenever the table
            // re-lays out (a theme swap does that) and scrolls the page with it;
            // every shot starts from the top, so scroll AFTER layout settled.
            (window as MainWindow)?.PageScroller.ScrollToTop();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Paint the element through a VisualBrush so its own layout offset
        /// inside the window does not shift the bitmap, then encode as PNG.
        /// </summary>
        private static void Save(FrameworkElement element, Brush background, string path)
        {
            var width = (int)Math.Ceiling(element.ActualWidth);
            var height = (int)Math.Ceiling(element.ActualHeight);
            if (width <= 0 || height <= 0) throw new InvalidOperationException($"{path}: element has no size");

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var rect = new Rect(0, 0, width, height);
                dc.DrawRectangle(background, null, rect);
                // An absolute viewbox pins the brush to the element's layout box:
                // the default (content bounds) would include focus rings and drop
                // shadows that extend past it and shift everything by a few px.
                // The brush's coordinate space includes the element's offset
                // inside its parent (its margin), so the viewbox starts there.
                var offset = VisualTreeHelper.GetOffset(element);
                var brush = new VisualBrush(element)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(offset.X, offset.Y, width, height),
                };
                // A VisualBrush re-applies the mirror only when the element
                // INHERITED its right-to-left direction — an element that sets
                // its own already carries the flip inside the brush, and
                // undoing it again draws every glyph back to front. The
                // direction moved from the window onto its content when the
                // caption was handed back to Windows, which turned the first
                // case into the second for every capture here.
                var parent = VisualTreeHelper.GetParent(element) as FrameworkElement;
                var mirrored = element.FlowDirection == FlowDirection.RightToLeft
                               && parent?.FlowDirection == FlowDirection.RightToLeft;
                if (mirrored) dc.PushTransform(new ScaleTransform(-1, 1, width / 2.0, 0));
                dc.DrawRectangle(brush, null, rect);
                if (mirrored) dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
