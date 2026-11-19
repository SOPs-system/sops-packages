using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sop.Ui.Controls
{
    public class ShortcutEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public ShortcutEntry() { }

        public ShortcutEntry(string key, string label, string category = "General")
        {
            Key = key;
            Label = label;
            Category = category;
        }
    }

    /// <summary>
    /// Modal overlay displaying categorized keyboard shortcuts (F1-F12, modifiers, navigation).
    /// Essential for cashier speed and desktop pharmacy operations.
    /// </summary>
    public class ShortcutOverlay : Control
    {
        static ShortcutOverlay()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShortcutOverlay), new FrameworkPropertyMetadata(typeof(ShortcutOverlay)));
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ShortcutOverlay),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ShortcutOverlay),
                new PropertyMetadata("Keyboard Shortcuts"));

        public static readonly DependencyProperty ShortcutsSourceProperty =
            DependencyProperty.Register(nameof(ShortcutsSource), typeof(IEnumerable), typeof(ShortcutOverlay),
                new PropertyMetadata(null, OnShortcutsSourceChanged));

        public static readonly DependencyProperty FilterTextProperty =
            DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(ShortcutOverlay),
                new PropertyMetadata(string.Empty, OnFilterTextChanged));

        public static readonly DependencyProperty FilteredShortcutsProperty =
            DependencyProperty.Register(nameof(FilteredShortcuts), typeof(IEnumerable), typeof(ShortcutOverlay),
                new PropertyMetadata(null));

        public static readonly RoutedEvent ClosedEvent =
            EventManager.RegisterRoutedEvent(nameof(Closed), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ShortcutOverlay));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public IEnumerable? ShortcutsSource
        {
            get => (IEnumerable?)GetValue(ShortcutsSourceProperty);
            set => SetValue(ShortcutsSourceProperty, value);
        }

        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        public IEnumerable? FilteredShortcuts
        {
            get => (IEnumerable?)GetValue(FilteredShortcutsProperty);
            private set => SetValue(FilteredShortcutsProperty, value);
        }

        public event RoutedEventHandler Closed
        {
            add => AddHandler(ClosedEvent, value);
            remove => RemoveHandler(ClosedEvent, value);
        }

        public ShortcutOverlay()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
            Loaded += (s, e) => ApplyFilter();
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is FrameworkElement el && (string)el.Tag == "CLOSE")
            {
                Close();
                e.Handled = true;
            }
        }

        public void Close()
        {
            IsOpen = false;
            RaiseEvent(new RoutedEventArgs(ClosedEvent));
        }

        private static void OnShortcutsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShortcutOverlay overlay)
            {
                overlay.ApplyFilter();
            }
        }

        private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShortcutOverlay overlay)
            {
                overlay.ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            if (ShortcutsSource == null)
            {
                FilteredShortcuts = null;
                return;
            }

            var list = ShortcutsSource.OfType<ShortcutEntry>();
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var query = FilterText.Trim();
                list = list.Where(s =>
                    s.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            FilteredShortcuts = list.GroupBy(s => s.Category).ToList();
        }
    }
}
