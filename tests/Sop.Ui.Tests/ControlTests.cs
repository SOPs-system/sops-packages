using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sop.Ui.Controls;
using Xunit;

namespace Sop.Ui.Tests
{
    // The design system reuses WPF's names on purpose; inside this namespace
    // the aliases win over the System.Windows.Controls import.
    using Button = Sop.Ui.Controls.Button;
    using TabItem = Sop.Ui.Controls.TabItem;
    using MenuItem = Sop.Ui.Controls.MenuItem;

    /// <summary>
    /// Live WPF tests: the theme is merged into a real Application and each
    /// primitive is shown in an off-screen window, so templates apply and
    /// DynamicResource lookups are exercised the way the app will use them.
    /// </summary>
    public class ControlTests
    {
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
            Theme.Attach(window);
            window.Show();
            window.UpdateLayout();
            return window;
        }

        private static Grid Root(params UIElement[] children)
        {
            var stack = new StackPanel();
            foreach (var child in children) stack.Children.Add(child);
            var grid = new Grid();
            grid.Children.Add(stack);
            grid.Children.Add(new ModalHost());
            return grid;
        }

        [WpfFact]
        public void Every_primitive_receives_its_template_from_the_theme()
        {
            EnsureApp();
            Control[] primitives =
            {
                new Button { Content = "Checkout" },
                new IconButton(),
                new Spinner(),
                new Badge { Content = "Rx" },
                new Alert { Title = "Shift open", Content = "Register opened" },
                new Divider { Label = "or" },
                new Card { Content = "Card" },
                new CardHeader { Title = "Today" },
                new CardFooter { Content = "Footer" },
                new Field { Label = "Price", Content = new Input() },
                new Input { Placeholder = "Search" },
                new Checkbox { Content = "Archived" },
                new Switch { Content = "Unit sale" },
                new Kbd { Content = "K" },
                new Tabs(),
                new Table(),
                new Toast { Title = "Done" },
                new Select { Options = new[] { new SelectOption("a", "A") } },
                new StatusIndicator { Text = "Online" },
                new GappedProgressBar { Segments = 4, Value = 2 },
                new SopsSegmentedProgressBar { Value = 45, Maximum = 100 },
                new SopsCircularProgressBar { Value = 60, Maximum = 100 },
                new SopsCircularProgress { Value = 75, Maximum = 100 },
                new MenuItem { Content = "Name" },
                new MenuSeparator(),
                new MenuPanel(),
            };
            var window = Show(Root(primitives));
            try
            {
                foreach (var control in primitives)
                {
                    Assert.True(control.Template != null, control.GetType().Name + " has no template");
                }
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Switching_to_dark_re_themes_a_live_control_without_rebuilding_it()
        {
            EnsureApp();
            var button = new Button { Content = "Checkout" };
            var window = Show(Root(button));
            try
            {
                var light = ((SolidColorBrush)button.Background).Color;
                Theme.Mode = ThemeMode.Dark;
                window.UpdateLayout();
                var dark = ((SolidColorBrush)button.Background).Color;
                Assert.NotEqual(light, dark);
                var expected = ((SolidColorBrush)Application.Current.Resources["Brush.Primary"]).Color;
                Assert.Equal(expected, dark);
            }
            finally
            {
                Theme.Mode = ThemeMode.Light;
                window.Close();
            }
        }

        [WpfFact]
        public void Arabic_mirrors_attached_windows_and_swaps_the_app_face()
        {
            EnsureApp();
            var window = Show(Root(new Button { Content = "x" }));
            try
            {
                // The direction lands on what the app DRAWS. The window itself
                // stays left to right: its own FlowDirection is what WPF turns
                // into a mirrored native caption.
                var content = (FrameworkElement)window.Content;
                Theme.IsRtl = true;
                Assert.Equal(FlowDirection.RightToLeft, content.FlowDirection);
                Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
                Assert.Same(Application.Current.Resources["Font.Ar"], Application.Current.Resources["Font.App"]);
                Theme.IsRtl = false;
                Assert.Equal(FlowDirection.LeftToRight, content.FlowDirection);
                Assert.Same(Application.Current.Resources["Font.En"], Application.Current.Resources["Font.App"]);
            }
            finally
            {
                Theme.IsRtl = false;
                window.Close();
            }
        }

        [WpfFact]
        public void A_field_with_an_error_marks_its_control_invalid_and_paints_it_red()
        {
            EnsureApp();
            var input = new Input();
            var field = new Field { Label = "Price", Content = input };
            var window = Show(Root(field));
            try
            {
                Assert.False(Field.GetIsInvalid(input));
                field.Error = "Price must be a whole number of piastres";
                window.UpdateLayout();
                Assert.True(Field.GetIsInvalid(input));
                var frame = (Border)input.Template.FindName("Frame", input);
                var danger = ((SolidColorBrush)Application.Current.Resources["Brush.Danger"]).Color;
                Assert.Equal(danger, ((SolidColorBrush)frame.BorderBrush).Color);
                field.Error = null;
                Assert.False(Field.GetIsInvalid(input));
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Loading_disables_a_button_and_icon_buttons_default_to_ghost_square()
        {
            EnsureApp();
            var button = new Button { Content = "Save", Loading = true };
            var icon = new IconButton();
            var window = Show(Root(button, icon));
            try
            {
                Assert.False(button.IsEnabled);
                button.Loading = false;
                Assert.True(button.IsEnabled);
                Assert.Equal(ButtonVariant.Ghost, icon.Variant);
                Assert.True(icon.IconOnly);
                Assert.Equal(icon.ActualWidth, icon.ActualHeight, 1);
                Assert.Equal((double)Application.Current.Resources["Size.Hit"], icon.ActualHeight, 1);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void An_icon_inside_a_button_is_drawn_in_the_buttons_own_colour()
        {
            EnsureApp();
            // The Icon style sets its own Foreground, and a style setter beats
            // an inherited value — so without the button template's override
            // every glyph came out ink-dark on a green fill.
            var onPrimary = new Icon { Glyph = "Cart", Size = 15 };
            var primary = new Button { Content = onPrimary };
            var onSecondary = new Icon { Glyph = "Pencil", Size = 15 };
            var secondary = new Button { Variant = ButtonVariant.Secondary, Content = onSecondary };
            var loose = new Icon { Glyph = "Cart", Size = 15 };
            var window = Show(Root(primary, secondary, loose));
            try
            {
                Assert.Equal(Application.Current.Resources["Brush.OnPrimary"], onPrimary.Foreground);
                Assert.Equal(Application.Current.Resources["Brush.Ink"], onSecondary.Foreground);
                // Outside a button nothing changes: an icon still defaults to ink.
                Assert.Equal(Application.Current.Resources["Brush.Ink"], loose.Foreground);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void DateInput_calendar_popup_selection_sets_date()
        {
            EnsureApp();
            var input = new DateInput();
            var window = Show(input);
            try
            {
                window.UpdateLayout();
                input.IsDropDownOpen = true;
                window.UpdateLayout();

                var target = new DateTime(2026, 11, 20);
                input.SelectedDate = target;
                Assert.Equal(target, input.SelectedDate);
                Assert.Equal("2026", ((TextBox)input.Template.FindName("PART_YearBox", input)).Text);
                Assert.Equal("11", ((TextBox)input.Template.FindName("PART_MonthBox", input)).Text);
                Assert.Equal("20", ((TextBox)input.Template.FindName("PART_DayBox", input)).Text);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Modal_presents_in_the_host_and_closes_on_escape()
        {
            EnsureApp();
            var modal = new Modal
            {
                Title = "Edit drug",
                Body = new Input(),
                Footer = new Button { Content = "Save" },
            };
            var root = Root(modal);
            var host = root.Children.OfType<ModalHost>().Single();
            var window = Show(root);
            try
            {
                Assert.Empty(host.Children);
                modal.IsOpen = true;
                window.UpdateLayout();
                Assert.Single(host.Children);
                var panel = (ModalPanel)host.Children[0];
                Assert.NotNull(panel.Template);

                var closeRequests = 0;
                modal.CloseRequested += (_, __) => closeRequests++;
                panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                });
                Assert.Equal(1, closeRequests);
                Assert.False(modal.IsOpen);
                Assert.Empty(host.Children);

                // Test close button
                modal.IsOpen = true;
                window.UpdateLayout();
                Assert.Single(host.Children);
                panel = (ModalPanel)host.Children[0];
                var closeButton = (ButtonBase)panel.Template.FindName("PART_Close", panel);
                Assert.NotNull(closeButton);
                closeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.False(modal.IsOpen);
                Assert.Empty(host.Children);

                // Test backdrop click (click at origin 0,0 which is outside the centered panel)
                modal.IsOpen = true;
                window.UpdateLayout();
                Assert.Single(host.Children);
                panel = (ModalPanel)host.Children[0];
                var backdrop = (FrameworkElement)panel.Template.FindName("PART_Backdrop", panel);
                Assert.NotNull(backdrop);
                backdrop.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent
                });
                Assert.False(modal.IsOpen);
                Assert.Empty(host.Children);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Modal_enter_invokes_the_last_footer_button_unless_it_is_disabled()
        {
            EnsureApp();
            var saved = 0;
            var save = new Button { Content = "Save" };
            save.Click += (_, __) => saved++;
            var footer = new StackPanel { Orientation = Orientation.Horizontal };
            footer.Children.Add(new Button { Content = "Cancel", Variant = ButtonVariant.Secondary });
            footer.Children.Add(save);
            var body = new Input();
            var modal = new Modal { Title = "Confirm", Body = body, Footer = footer, IsOpen = false };
            var root = Root(modal);
            var host = root.Children.OfType<ModalHost>().Single();
            var window = Show(root);
            try
            {
                modal.IsOpen = true;
                window.UpdateLayout();
                var panel = (ModalPanel)host.Children[0];
                body.Focus();
                KeyEventArgs Enter() => new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };

                panel.RaiseEvent(Enter());
                Assert.Equal(1, saved);

                save.IsEnabled = false;
                panel.RaiseEvent(Enter());
                Assert.Equal(1, saved); // a disabled primary means Enter does nothing
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Tabs_follow_selected_key_in_both_directions()
        {
            EnsureApp();
            var tabs = new Tabs();
            tabs.TabItems!.Add(new TabItem("today", "Today"));
            tabs.TabItems!.Add(new TabItem("week", "Week"));
            tabs.SelectedKey = "today";
            var window = Show(Root(tabs));
            try
            {
                TabButton Button(int i) => (TabButton)tabs.ItemContainerGenerator.ContainerFromIndex(i);
                Assert.True(Button(0).IsChecked);
                Assert.False(Button(1).IsChecked);

                tabs.SelectedKey = "week";
                Assert.False(Button(0).IsChecked);
                Assert.True(Button(1).IsChecked);

                Button(0).IsChecked = true; // a click
                Assert.Equal("today", tabs.SelectedKey);
                Assert.False(Button(1).IsChecked);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Select_shows_the_selected_label_filters_as_you_type_and_picks_with_enter()
        {
            EnsureApp();
            var select = new Select
            {
                Options = new[]
                {
                    new SelectOption("analgesics", "Analgesics"),
                    new SelectOption("antibiotics", "Antibiotics"),
                    new SelectOption("nsaids", "NSAIDs"),
                    new SelectOption("discontinued", "Discontinued", disabled: true),
                },
                Value = "analgesics",
            };
            var window = Show(Root(select));
            try
            {
                Assert.Equal("Analgesics", select.DisplayText);
                var input = (Input)select.Template.FindName("PART_Input", select);
                Assert.Equal("Analgesics", input.Text);

                input.Focus();
                input.Text = "anti";
                window.UpdateLayout();
                Assert.True(select.IsOpen);
                Assert.Single(select.Shown);
                Assert.Equal("antibiotics", select.Shown[0].Value);

                var changed = 0;
                select.ValueChanged += (_, __) => changed++;
                input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                });
                Assert.Equal("antibiotics", select.Value);
                Assert.Equal(1, changed);
                Assert.False(select.IsOpen);
                Assert.Equal("Antibiotics", input.Text);

                // Esc restores the label and drops the query.
                input.Text = "zzz";
                Assert.True(select.IsOpen);
                Assert.Empty(select.Shown);
                input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                });
                Assert.False(select.IsOpen);
                Assert.Equal("Antibiotics", input.Text);
                Assert.Equal(4, select.Shown.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_paged_dropdown_offers_the_rest_and_choosing_it_is_not_a_selection()
        {
            EnsureApp();
            var select = new Select
            {
                Width = 320,
                Options = Enumerable.Range(1, 24).Select(i => new SelectOption("v" + i, "Option " + i)).ToList(),
                Value = "v1",
            };
            var window = Show(Root(select));
            try
            {
                var more = (MenuItem)select.Template.FindName("PART_More", select);
                // Ten of twenty-four shown, so the rest is offered.
                Assert.Equal(Visibility.Visible, more.Visibility);
                Assert.False(string.IsNullOrEmpty(select.MoreLabel));

                more.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.UpdateLayout();
                Assert.Equal(Select.PageSize * 2, select.Shown.Count);
                // …and it chose nothing: the value is untouched.
                Assert.Equal("v1", select.Value);

                more.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.UpdateLayout();
                Assert.Equal(24, select.Shown.Count);
                // Nothing left to offer, so the row goes.
                Assert.Equal(Visibility.Collapsed, more.Visibility);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Scrolling_a_dropdown_to_the_bottom_loads_the_next_ten()
        {
            EnsureApp();
            var select = new Select
            {
                Width = 320,
                Options = Enumerable.Range(1, 40).Select(i => new SelectOption("v" + i, "Option " + i)).ToList(),
                Value = "v1",
            };
            var window = Show(Root(select));
            try
            {
                var menu = (Sop.Ui.Controls.Menu)select.Template.FindName("PART_Menu", select);
                menu.IsOpen = true;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                window.UpdateLayout();

                Assert.Equal(Select.PageSize, select.Shown.Count);

                var scroller = menu.Panel.Scroller;
                Assert.True(scroller != null, "the menu has no scroller to page from");
                // Ten rows must overflow the panel, or there is nothing to
                // scroll and the next ten can never be reached.
                Assert.True(scroller!.ScrollableHeight > 0,
                    $"a page of {Select.PageSize} does not overflow the menu " +
                    $"(extent {scroller.ExtentHeight}, viewport {scroller.ViewportHeight}) — scrolling can never load more");

                // Scroll to the bottom, exactly as a mouse wheel would.
                scroller.ScrollToBottom();
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                Assert.True(select.Shown.Count > Select.PageSize,
                    "scrolling to the bottom did not load the next page");
            }
            finally
            {
                var menu = select.Template.FindName("PART_Menu", select) as Sop.Ui.Controls.Menu;
                if (menu != null) menu.IsOpen = false;
                window.Close();
            }
        }

        [WpfFact]
        public void Scrolling_a_dropdown_to_the_top_loads_the_previous_ten_without_jumping()
        {
            EnsureApp();
            var select = new Select
            {
                Width = 320,
                Options = Enumerable.Range(1, 500).Select(i => new SelectOption("v" + i, "Option " + i)).ToList(),
                // Deep in the list: the page opens around it, so there is
                // something above the first row as well as below the last.
                Value = "v300",
            };
            var window = Show(Root(select));
            try
            {
                var menu = (Sop.Ui.Controls.Menu)select.Template.FindName("PART_Menu", select);
                menu.IsOpen = true;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                window.UpdateLayout();

                var scroller = menu.Panel.Scroller;
                Assert.True(scroller != null, "the menu has no scroller to page from");
                Assert.True(select.HasEarlier);

                // Down into the list, then back up to the top — as a wheel does.
                scroller!.ScrollToVerticalOffset(200);
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                scroller.ScrollToTop();
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                Assert.Equal("v280", select.Shown[0].Value);
                // The rows arrived above the viewport: the list must stay where
                // it was rather than throwing what was on screen downwards.
                Assert.True(scroller.VerticalOffset > 0,
                    "the list jumped back to the top when the earlier page loaded"
                    + $" (offset {scroller.VerticalOffset}, extent {scroller.ExtentHeight}, viewport {scroller.ViewportHeight}, rows {select.Shown.Count})");
            }
            finally
            {
                var menu = select.Template.FindName("PART_Menu", select) as Sop.Ui.Controls.Menu;
                if (menu != null) menu.IsOpen = false;
                window.Close();
            }
        }

        [WpfFact]
        public void Every_dropdown_option_is_as_wide_as_the_menu()
        {
            EnsureApp();
            var select = new Select
            {
                Width = 320,
                Options = new[]
                {
                    // Very different label lengths: if the options size to
                    // their own text, these come out ragged.
                    new SelectOption("a", "A"),
                    new SelectOption("b", "Antibiotics and antivirals"),
                },
                Value = "a",
            };
            var window = Show(Root(select));
            try
            {
                var menu = (Sop.Ui.Controls.Menu)select.Template.FindName("PART_Menu", select);
                menu.IsOpen = true;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                window.UpdateLayout();

                // Options only: the trailing "show more" row is not one, and it
                // is not bound to an option either.
                var items = Descendants(menu.Panel).OfType<MenuItem>()
                    .Where(item => item.DataContext is SelectRow)
                    .ToList();
                Assert.Equal(2, items.Count);
                // The short label's row is as wide as the long one's — the
                // highlight has to span the popup, not hug the word.
                Assert.True(items[0].ActualWidth > 0, "the menu was measured");
                Assert.Equal(items[1].ActualWidth, items[0].ActualWidth, 1);
                // A popup is its own top-level window: left open it outlives
                // this one and eats the next test's keystrokes.
                menu.IsOpen = false;
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>Every element under one, for reaching into a popup's tree.</summary>
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var deeper in Descendants(child)) yield return deeper;
            }
        }

        [WpfFact]
        public void Select_opens_on_click_and_toggles_closed_on_second_click()
        {
            EnsureApp();
            var select = new Select
            {
                Options = new[]
                {
                    new SelectOption("analgesics", "Analgesics"),
                    new SelectOption("antibiotics", "Antibiotics"),
                },
                Value = "analgesics",
            };
            var window = Show(Root(select));
            try
            {
                var input = (Input)select.Template.FindName("PART_Input", select);
                Assert.False(select.IsOpen);

                // Click 1: MouseDown then MouseUp opens the dropdown
                input.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                });
                input.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                });
                window.UpdateLayout();
                Assert.True(select.IsOpen);

                // Click 2: MouseDown then MouseUp toggles closed
                input.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                });
                input.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                });
                window.UpdateLayout();
                Assert.False(select.IsOpen);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Table_selection_scrolls_the_row_into_view_and_keeps_dragged_widths_per_key()
        {
            EnsureApp();
            var rows = Enumerable.Range(1, 200).Select(i => new { Name = "Drug " + i, Stock = i.ToString() }).ToList();
            var table = new Table { ColumnsKey = "test", Height = 150 };
            table.Columns.Add(new DataGridTextColumn { Header = "Drug", Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(120) });
            table.Columns.Add(new DataGridTextColumn { Header = "Stock", Binding = new System.Windows.Data.Binding("Stock") });
            table.ItemsSource = rows;
            var window = Show(Root(table));
            try
            {
                table.SelectedItem = rows[150];
                window.UpdateLayout();
                var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromItem(rows[150]);
                Assert.NotNull(row);
                Assert.True(row.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_selection_made_while_the_page_is_rebinding_still_scrolls_into_view()
        {
            EnsureApp();
            var page1 = Enumerable.Range(1, 25).Select(i => new { Name = "Drug " + i }).ToList();
            var page2 = Enumerable.Range(26, 25).Select(i => new { Name = "Drug " + i }).ToList();
            var table = new Table { ColumnsKey = "paging", Height = 150 };
            table.Columns.Add(new DataGridTextColumn { Header = "Drug", Binding = new System.Windows.Data.Binding("Name") });
            table.ItemsSource = page1;
            var window = Show(Root(table));
            try
            {
                // What an arrow key does at a page boundary: swap the page and
                // select a row on it in the same breath. No container exists
                // for that row yet, so the scroll has to happen again once
                // layout has caught up — otherwise the selection is off-screen.
                table.ItemsSource = page2;
                table.SelectedItem = page2[24];
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                window.UpdateLayout();

                var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromItem(page2[24]);
                Assert.NotNull(row);
                Assert.True(row.IsVisible, "the newly selected last row is on screen");
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_dropdown_does_not_outlive_its_window_going_to_the_back()
        {
            EnsureApp();
            var anchor = new Button { Content = "Sort" };
            var popup = new Popup
            {
                PlacementTarget = anchor,
                Child = new TextBlock { Text = "options" },
                StaysOpen = true,
            };
            PopupGuard.SetFollowsOwner(popup, true);
            var window = Show(Root(anchor, popup));
            try
            {
                popup.IsOpen = true;
                window.UpdateLayout();
                Assert.True(popup.IsOpen);

                // A popup is its own top-level window: without this it stays
                // painted over whatever the user switched to.
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                typeof(Window).GetMethod("OnDeactivated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, new object?[] { System.EventArgs.Empty });
                Assert.False(popup.IsOpen, "the dropdown went with the window");
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
            }
        }

        [WpfFact]
        public void A_banner_closes_itself_unless_the_caller_takes_over()
        {
            EnsureApp();
            var closable = new Alert { Title = "Something went wrong", Content = "Demo data needs an empty catalog", Dismissable = true };
            var standing = new Alert { Title = "Allergies", Content = "Confirm before dispensing" };
            var window = Show(Root(closable, standing));
            try
            {
                // A banner stating a condition that is still true offers no way
                // out; only one the reader can be finished with does.
                Assert.Equal(Visibility.Collapsed,
                    ((System.Windows.Controls.Button)standing.Template.FindName("PART_Dismiss", standing)).Visibility);

                var close = (System.Windows.Controls.Button)closable.Template.FindName("PART_Dismiss", closable);
                Assert.Equal(Visibility.Visible, close.Visibility);

                var raised = 0;
                closable.Dismiss += (_, __) => raised++;
                close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.UpdateLayout();

                Assert.Equal(1, raised);
                Assert.Equal(Visibility.Collapsed, closable.Visibility);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_banner_stays_up_when_a_caller_handles_the_dismiss()
        {
            EnsureApp();
            var alert = new Alert { Content = "Saved, but the printer refused", Dismissable = true };
            var window = Show(Root(alert));
            try
            {
                // Handling it takes over what "close" means; the banner stays.
                alert.Dismiss += (_, e) => e.Handled = true;
                var close = (System.Windows.Controls.Button)alert.Template.FindName("PART_Dismiss", alert);
                close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.UpdateLayout();
                Assert.Equal(Visibility.Visible, alert.Visibility);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// The window always has a caption, and it is the system's.
        ///
        /// It drew its own once, for a bar in the register's own colours. That
        /// is gone at the owner's request; what must never go with it is the
        /// ability to close the window, which is the failure this guards.
        /// </summary>
        [WpfFact]
        public void The_window_keeps_the_systems_caption()
        {
            EnsureApp();
            var window = new AppWindow
            {
                Left = -20000, Top = -20000, Width = 800, Height = 600,
                ShowInTaskbar = false, ShowActivated = false,
                Title = "Smart Operating Pharmacy",
                Content = new Grid(),
            };
            window.Show();
            window.UpdateLayout();
            try
            {
                Assert.False(window.UsesOwnChrome);
                Assert.True(window.WindowStyle != WindowStyle.None,
                    "no caption bar of either kind — the register cannot be closed");
                var bar = (Border)window.Template.FindName("TitleBar", window);
                Assert.Equal(Visibility.Collapsed, bar.Visibility);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// The app draws no caption bar of its own, in either language.
        ///
        /// It used to draw one, for a title bar in the register's own colours.
        /// That cost more than it bought — it had to be held off the screen
        /// edges by hand when maximised, its buttons swapped sides with the
        /// language, and a desktop with no composition drew over it — and once
        /// the mark and title came off it at the owner's request it was a
        /// blank strip above the app. Windows draws the caption now.
        /// </summary>
        [WpfFact]
        public void The_app_draws_no_caption_bar_of_its_own()
        {
            EnsureApp();
            foreach (var rtl in new[] { false, true })
            {
                Theme.IsRtl = rtl;
                var window = new AppWindow
                {
                    Left = -20000, Top = -20000, Width = 800, Height = 600,
                    ShowInTaskbar = false, ShowActivated = false,
                    Title = "صيدلية",
                    Content = new Grid(),
                };
                Theme.Attach(window);
                window.Show();
                window.UpdateLayout();
                try
                {
                    Assert.False(window.UsesOwnChrome, $"the app drew its own caption (rtl={rtl})");

                    // Nothing of the old bar is on screen: no strip, no mark.
                    var bar = (FrameworkElement)window.Template.FindName("TitleBar", window);
                    Assert.Equal(Visibility.Collapsed, bar.Visibility);
                    Assert.Empty(Descendants(window).OfType<BrandLogo>().Where(logo => logo.IsVisible));

                    // …and nothing is held back from the screen edges for it.
                    Assert.Equal(default(Thickness), window.MaximisedPad);
                }
                finally
                {
                    Theme.IsRtl = false;
                    window.Close();
                }
            }
        }

        [WpfFact]
        public void A_window_never_opens_bigger_than_the_screen_it_opens_on()
        {
            EnsureApp();
            var work = SystemParameters.WorkArea;
            // The register asks for more than a small till has; centred, the
            // caption bar and its buttons would hang above the top edge.
            var window = new AppWindow
            {
                Left = -20000, Top = -20000,
                Width = work.Width + 400, Height = work.Height + 400,
                MinWidth = 320, MinHeight = 240,
                ShowInTaskbar = false, ShowActivated = false,
                Content = new Grid(),
            };
            try
            {
                Assert.True(window.Width <= work.Width, $"width {window.Width} exceeds the work area {work.Width}");
                Assert.True(window.Height <= work.Height, $"height {window.Height} exceeds the work area {work.Height}");
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// The bar behaves the same on every desktop now.
        ///
        /// It did not before: a machine with no composition drew the system's
        /// caption over the app's own, and `UsesOwnChrome` existed to hide the
        /// app's there. With the app drawing none anywhere, a Windows 7 till
        /// and a Windows 11 one get the same window — which is the whole
        /// reason that fallback is no longer a fallback.
        /// </summary>
        [WpfFact]
        public void Every_desktop_gets_the_same_window()
        {
            EnsureApp();
            var window = new AppWindow
            {
                Left = -20000, Top = -20000, Width = 800, Height = 600,
                ShowInTaskbar = false, ShowActivated = false,
                Content = new Grid(),
            };
            window.Show();
            window.UpdateLayout();
            try
            {
                // Composited or not, the answer is the same.
                Assert.False(window.UsesOwnChrome);
                var bar = (Border)window.Template.FindName("TitleBar", window);
                Assert.Equal(Visibility.Collapsed, bar.Visibility);
                Assert.True(window.WindowStyle != WindowStyle.None,
                    "no caption bar of either kind — the register cannot be closed");
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_maximised_window_leaves_room_for_the_frame_Windows_adds()
        {
            EnsureApp();
            var window = new AppWindow
            {
                Left = -20000, Top = -20000, Width = 800, Height = 600,
                ShowInTaskbar = false, ShowActivated = false,
                Content = new Grid(),
            };
            window.Show();
            window.UpdateLayout();
            try
            {
                Assert.Equal(new Thickness(0), window.MaximisedPad);
                window.WindowState = WindowState.Maximized;
                window.UpdateLayout();
                if (window.UsesOwnChrome)
                {
                    // Whatever the theme, the pad has to be a real distance:
                    // zero puts the caption buttons above the top of the screen.
                    Assert.True(window.MaximisedPad.Top > 0, "a maximised window would lose its caption bar");
                    Assert.True(window.MaximisedPad.Top < 32, $"pad of {window.MaximisedPad.Top} is not a window frame");
                }
                else
                {
                    Assert.Equal(new Thickness(0), window.MaximisedPad);
                }
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_long_dropdown_builds_ten_options_at_a_time()
        {
            EnsureApp();
            var many = Enumerable.Range(1, 34)
                .Select(i => new SelectOption("v" + i, "Option " + i))
                .ToList();
            var select = new Select { Options = many, Value = "v1" };
            var window = Show(Root(select));
            try
            {
                // A catalog of hundreds must not build every row to show ten.
                Assert.Equal(Select.PageSize, select.Shown.Count);
                Assert.True(select.HasMore);

                select.ShowMore();
                Assert.Equal(Select.PageSize * 2, select.Shown.Count);

                select.ShowMore();
                select.ShowMore();
                // The last page is short: 34 options, not 40.
                Assert.Equal(34, select.Shown.Count);
                Assert.False(select.HasMore);

                // A search is a new list, so it starts at the first page again.
                select.SearchForTest("option");
                window.UpdateLayout();
                Assert.Equal(Select.PageSize, select.Shown.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_list_opened_on_a_selection_starts_around_it_and_pages_both_ways()
        {
            EnsureApp();
            var many = Enumerable.Range(1, 500)
                .Select(i => new SelectOption("v" + i, "Option " + i))
                .ToList();
            // Chosen deep in the catalog: building every row above it to reach
            // it is what made a long list slow to open.
            var select = new Select { Options = many, Value = "v300" };
            var window = Show(Root(select));
            try
            {
                Assert.Contains(select.Shown, row => row.Value == "v300" && row.IsSelected);
                // The ten before it are there to scroll back through, and the
                // rest of the catalog is not built at all.
                Assert.Equal("v290", select.Shown[0].Value);
                Assert.Equal(Select.PageSize * 2, select.Shown.Count);
                Assert.True(select.HasEarlier);
                Assert.True(select.HasMore);

                select.ShowEarlier();
                Assert.Equal("v280", select.Shown[0].Value);
                Assert.Equal(Select.PageSize * 3, select.Shown.Count);
                // Still the same option, still selected — a page above it does
                // not disturb what was picked.
                Assert.Contains(select.Shown, row => row.Value == "v300" && row.IsSelected);

                select.ShowMore();
                Assert.Equal("v319", select.Shown[select.Shown.Count - 1].Value);

                // Both ends run out where the list does.
                while (select.HasEarlier) select.ShowEarlier();
                Assert.Equal("v1", select.Shown[0].Value);
                Assert.False(select.HasEarlier);

                // A form hands over its options first and the value second;
                // the window has to follow the value, not the assignment order.
                select.Options = many.ToList();
                select.Value = "v450";
                Assert.Equal("v440", select.Shown[0].Value);
                Assert.Contains(select.Shown, row => row.Value == "v450" && row.IsSelected);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void A_selected_option_past_the_first_page_is_still_reachable()
        {
            EnsureApp();
            var many = Enumerable.Range(1, 40)
                .Select(i => new SelectOption("v" + i, "Option " + i))
                .ToList();
            // Chosen well beyond the first ten: opening the list has to be able
            // to land on it, so the page must reach that far.
            var select = new Select { Options = many, Value = "v27" };
            var window = Show(Root(select));
            try
            {
                Assert.Contains(select.Shown, row => row.Value == "v27");
                Assert.Contains(select.Shown, row => row.Value == "v27" && row.IsSelected);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Toast_dismiss_raises_the_event()
        {
            EnsureApp();
            var toast = new Toast { Title = "Sale completed", Dismissable = true };
            var dismissed = 0;
            toast.Dismiss += (_, __) => dismissed++;
            var window = Show(Root(toast));
            try
            {
                var button = (System.Windows.Controls.Button)toast.Template.FindName("PART_Dismiss", toast);
                Assert.Equal(Visibility.Visible, button.Visibility);
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(1, dismissed);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Breadcrumbs_updates_item_current_state_and_raises_click()
        {
            EnsureApp();
            var breadcrumbs = new Breadcrumbs
            {
                ItemsSource = new[] { "Inventory", "Antibiotics", "Amoxicillin 500mg" }
            };
            object? clicked = null;
            breadcrumbs.ItemClick += (_, e) => clicked = e.NewValue;

            var window = Show(breadcrumbs);
            try
            {
                window.UpdateLayout();
                breadcrumbs.UpdateItemStates();

                var item0 = (BreadcrumbItem)breadcrumbs.ItemContainerGenerator.ContainerFromIndex(0);
                var item1 = (BreadcrumbItem)breadcrumbs.ItemContainerGenerator.ContainerFromIndex(1);
                var item2 = (BreadcrumbItem)breadcrumbs.ItemContainerGenerator.ContainerFromIndex(2);

                Assert.NotNull(item0);
                Assert.NotNull(item2);
                Assert.False(item0.IsCurrent);
                Assert.True(item0.HasSeparator);
                Assert.True(item0.IsEnabled);

                Assert.False(item1.IsCurrent);
                Assert.True(item1.HasSeparator);

                Assert.True(item2.IsCurrent);
                Assert.False(item2.HasSeparator);
                Assert.False(item2.IsEnabled);

                item0.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal("Inventory", clicked);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void FilterChip_toggles_and_removes()
        {
            EnsureApp();
            var chip = new FilterChip
            {
                Label = "Category:",
                Content = "Antibiotics",
                Count = 5,
                IsRemovable = true
            };
            var removed = false;
            chip.Removed += (_, __) => removed = true;

            var window = Show(chip);
            try
            {
                window.UpdateLayout();
                Assert.False(chip.IsSelected);

                // Body click toggles selected
                chip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent
                });
                Assert.True(chip.IsSelected);

                var removeBtn = (System.Windows.Controls.Button)chip.Template.FindName("PART_RemoveButton", chip);
                Assert.NotNull(removeBtn);
                removeBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.True(removed);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void SplitButton_primary_action_and_dropdown()
        {
            EnsureApp();
            var splitButton = new SplitButton
            {
                Content = "Complete Sale",
                Variant = ButtonVariant.Primary
            };
            var clicked = false;
            var opened = false;
            splitButton.Click += (_, __) => clicked = true;
            splitButton.DropDownOpened += (_, __) => opened = true;

            var window = Show(splitButton);
            try
            {
                window.UpdateLayout();
                var primaryBtn = (System.Windows.Controls.Button)splitButton.Template.FindName("PART_PrimaryButton", splitButton);
                Assert.NotNull(primaryBtn);

                primaryBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.True(clicked);

                splitButton.IsOpen = true;
                Assert.True(opened);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void CalendarRangePicker_presets_and_formatting()
        {
            EnsureApp();
            var picker = new CalendarRangePicker();
            var window = Show(picker);
            try
            {
                window.UpdateLayout();
                Assert.Equal("Select date range", picker.DisplayText);

                picker.ApplyPreset(CalendarRangePreset.Last30Days);
                Assert.NotNull(picker.StartDate);
                Assert.NotNull(picker.EndDate);
                Assert.Equal(DateTime.Today, picker.EndDate.Value.Date);
                Assert.Equal(DateTime.Today.AddDays(-29), picker.StartDate.Value.Date);

                Assert.Contains(picker.StartDate.Value.ToString("yyyy-MM-dd"), picker.DisplayText);
                Assert.Contains(picker.EndDate.Value.ToString("yyyy-MM-dd"), picker.DisplayText);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
