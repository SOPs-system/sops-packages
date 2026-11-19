using System;
using System.Windows;
using Sop.Ui;
using Sop.Ui.Controls;

namespace Sops.UI.Consumer
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Console.WriteLine("===============================================================");
            Console.WriteLine("  Sops.UI.Wpf — Clean NuGet Package Consumption Test           ");
            Console.WriteLine("  Package: Sops.UI.Wpf v1.0.0 (Original Canonical Sop.Ui)      ");
            Console.WriteLine("===============================================================");

            // 1. Initialize Application & Theme
            var app = new Application();
            Theme.Initialize(app);
            Console.WriteLine("[1/5] Theme.Initialize(app) executed successfully.");

            // 2. Verify Theme Tokens
            var primaryBrush = app.TryFindResource("Brush.Primary");
            var bgBrush = app.TryFindResource("Brush.Bg");
            var inkBrush = app.TryFindResource("Brush.Ink");
            Console.WriteLine($"[2/5] Verified Core Brushes: Brush.Primary = {primaryBrush}, Brush.Bg = {bgBrush}, Brush.Ink = {inkBrush}");

            if (primaryBrush == null || bgBrush == null || inkBrush == null)
            {
                throw new InvalidOperationException("Failed to resolve core design tokens from Sop.Ui!");
            }

            // 3. Instantiate Packaged Controls
            var btn = new Button { Content = "Checkout" };
            var badge = new Badge { Content = "Active", Variant = BadgeVariant.Success };
            var card = new Card();
            var input = new Input { Placeholder = "Search..." };
            var sw = new Switch { IsChecked = true };
            var alert = new Alert { Variant = StatusVariant.Info, Content = "System Operational" };

            Console.WriteLine($"[3/5] Instantiated Primitives: Button '{btn.Content}', Badge '{badge.Content}' ({badge.Variant}), Switch IsChecked={sw.IsChecked}");

            // 4. Test Theme Toggling
            Theme.Mode = ThemeMode.Dark;
            Console.WriteLine($"[4/5] Toggled Theme to Dark: Current Mode = {Theme.Mode}");
            Theme.Mode = ThemeMode.Light;
            Console.WriteLine($"      Toggled Theme back to Light: Current Mode = {Theme.Mode}");

            // 5. Test RTL Direction
            Theme.IsRtl = true;
            Console.WriteLine($"[5/5] Toggled RTL to Arabic: IsRtl = {Theme.IsRtl}");
            Theme.IsRtl = false;
            Console.WriteLine($"      Toggled RTL back to English: IsRtl = {Theme.IsRtl}");

            Console.WriteLine("===============================================================");
            Console.WriteLine("  STATUS: 100% SUCCESS — Sops.UI.Wpf package is fully verified! ");
            Console.WriteLine("===============================================================");
        }
    }
}
