using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Sop.Ui;
using Sop.Ui.Controls;

namespace Sop.Gallery
{
    public sealed class DrugRow
    {
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Concentration { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Stock { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var rows = new List<DrugRow>
            {
                new DrugRow { Name = "Panadol Extra — بانادول", Sku = "DRG-000001", Concentration = "500 mg / 65 mg", Price = "E£45.00", Stock = "124" },
                new DrugRow { Name = "Augmentin 1g — أوجمنتين", Sku = "DRG-000002", Concentration = "875 mg / 125 mg", Price = "E£210.00", Stock = "31" },
                new DrugRow { Name = "Brufen Syrup — بروفين", Sku = "DRG-000003", Concentration = "100 mg", Price = "E£32.50", Stock = "58" },
                new DrugRow { Name = "Vitamin C Serum", Sku = "DRG-000004", Concentration = "15%", Price = "E£399.00", Stock = "12" },
                new DrugRow { Name = "Insulin Glargine", Sku = "DRG-000005", Concentration = "100 IU", Price = "E£850.00", Stock = "6" },
            };
            DrugTable.ItemsSource = rows;
            DrugTable.SelectedItem = rows[1];

            GalleryTabs.TabItems!.Add(new TabItem("today", "Today"));
            GalleryTabs.TabItems!.Add(new TabItem("week", "This week"));
            GalleryTabs.TabItems!.Add(new TabItem("all", "All time"));

            Theme.Changed += (_, __) => SyncToggles();
            SyncToggles();
        }

        private void SyncToggles()
        {
            DarkToggle.Content = Theme.Mode == ThemeMode.Dark ? "Light" : "Dark";
            RtlToggle.Content = Theme.IsRtl ? "LTR" : "RTL — عربي";
        }

        private void ToggleDark(object sender, RoutedEventArgs e) =>
            Theme.Mode = Theme.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;

        private void ToggleRtl(object sender, RoutedEventArgs e) => Theme.IsRtl = !Theme.IsRtl;

        private void OpenModal(object sender, RoutedEventArgs e) => EditModal.IsOpen = true;

        private void CloseModal(object sender, RoutedEventArgs e) => EditModal.IsOpen = false;

        private bool _sortMenuWasOpen;

        private void SortButton_PreviewMouseDown(object sender, MouseButtonEventArgs e) =>
            _sortMenuWasOpen = SortMenu.IsOpen;

        private void OpenSort(object sender, RoutedEventArgs e)
        {
            if (_sortMenuWasOpen)
            {
                _sortMenuWasOpen = false;
                SortMenu.IsOpen = false;
                return;
            }
            SortMenu.IsOpen = true;
        }

        private void OpenContext(object sender, MouseButtonEventArgs e)
        {
            RowMenu.OpenAt(ContextTarget, e.GetPosition(ContextTarget));
            e.Handled = true;
        }

        private void ApplyToPageScrollbar(object sender, RoutedEventArgs e)
        {
            if (GutterSlider != null && ArrowSlider != null && GlyphSlider != null)
            {
                ScrollBarAssist.SetGutterSize(Page, GutterSlider.Value);
                ScrollBarAssist.SetArrowSize(Page, ArrowSlider.Value);
                ScrollBarAssist.SetArrowGlyphSize(Page, GlyphSlider.Value);
            }
        }

        private void ResetScrollbarSize(object sender, RoutedEventArgs e)
        {
            if (GutterSlider != null && ArrowSlider != null && GlyphSlider != null)
            {
                GutterSlider.Value = 12;
                ArrowSlider.Value = 12;
                GlyphSlider.Value = 7;
                ScrollBarAssist.SetGutterSize(Page, double.NaN);
                ScrollBarAssist.SetArrowSize(Page, double.NaN);
                ScrollBarAssist.SetArrowGlyphSize(Page, double.NaN);
            }
        }

        private void ToggleSidebarWidth(object sender, RoutedEventArgs e)
        {
            if (ShellSidebar != null) ShellSidebar.IsExpanded = !ShellSidebar.IsExpanded;
        }

        private void ToggleShellSidePanel(object sender, RoutedEventArgs e)
        {
            if (ShellSidePanel != null) ShellSidePanel.IsOpen = !ShellSidePanel.IsOpen;
        }

        private void CloseShellSidePanel(object sender, RoutedEventArgs e)
        {
            if (ShellSidePanel != null) ShellSidePanel.IsOpen = false;
        }

        private void ToggleTopBarOnline(object sender, RoutedEventArgs e)
        {
            if (DemoTopBar != null)
            {
                DemoTopBar.IsOnline = !DemoTopBar.IsOnline;
                DemoTopBar.OnlineText = DemoTopBar.IsOnline ? "Online — Cloud Synced" : "Offline — Local POS";
            }
        }

        private void AddNotification(object sender, RoutedEventArgs e)
        {
            if (DemoTopBar != null) DemoTopBar.NotificationsCount++;
        }

        private void WidenRightPanel(object sender, RoutedEventArgs e)
        {
            if (ShellSidePanel != null)
            {
                double current = ShellSidePanel.ActualWidth > 0 ? ShellSidePanel.ActualWidth : ShellSidePanel.Width;
                if (double.IsNaN(current)) current = 300;
                ShellSidePanel.Width = Math.Min(550, current + 50);
            }
        }

        private void ResetRightPanel(object sender, RoutedEventArgs e)
        {
            if (ShellSidePanel != null)
            {
                ShellSidePanel.Width = 300;
            }
        }

        /// <summary>The scrolling page content, for the capture routine.</summary>
        internal FrameworkElement PageContent => PageBody;

        internal System.Windows.Controls.ScrollViewer PageScroller => Page;

        internal Modal CaptureModal => EditModal;

        internal void OpenSortForCapture()
        {
            SortButton.BringIntoView();
            SortMenu.IsOpen = true;
        }

        internal void OpenContextForCapture()
        {
            ContextTarget.BringIntoView();
            // 40px in from the card's start edge, 20px down: where a right-click would land.
            RowMenu.OpenAt(ContextTarget, new Point(40, 20));
        }

        internal void OpenCategoryForCapture()
        {
            Category.BringIntoView();
            // The list opens the way a cashier opens it: focus, then Down.
            var input = (Input)Category.Template.FindName("PART_Input", Category);
            input.Focus();
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Down)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
        }

        /// <summary>Screen geometry of every open popup and its anchor, for the capture log.</summary>
        internal System.Collections.Generic.List<string> DescribeOpenPopups()
        {
            var lines = new System.Collections.Generic.List<string> { $"window at {Left},{Top} size {ActualWidth}x{ActualHeight} flow {FlowDirection}" };
            void Describe(string label, Menu menu)
            {
                if (!menu.IsOpen) return;
                var panel = menu.Panel;
                var target = (FrameworkElement)menu.PlacementTarget;
                var p = panel.PointToScreen(new Point(0, 0));
                var t = target.PointToScreen(new Point(0, 0));
                lines.Add($"{label}: panel at {p.X:0},{p.Y:0} size {panel.ActualWidth:0}x{panel.ActualHeight:0}; target at {t.X:0},{t.Y:0} size {target.ActualWidth:0}x{target.ActualHeight:0}");
            }
            Describe("sort", SortMenu);
            Describe("context", RowMenu);
            Describe("category", (Menu)Category.Template.FindName("PART_Menu", Category));
            return lines;
        }

        internal void ClosePopupsForCapture()
        {
            SortMenu.IsOpen = false;
            RowMenu.IsOpen = false;
            var input = (Input)Category.Template.FindName("PART_Input", Category);
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            Keyboard.ClearFocus();
        }
    }
}
