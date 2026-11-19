using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Data table: sticky header, drag-to-resize columns, single selection
    /// that scrolls itself into view. A DataGrid under the hood, locked to
    /// the register's needs (read-only rows, no add row, no reorder/sort UI).
    ///
    /// Column widths the user has dragged are kept keyed by
    /// <see cref="ColumnsKey"/>. The design system never touches storage
    /// itself, so the app installs <see cref="LoadWidths"/> and
    /// <see cref="StoreWidths"/> at startup (they land in the per-device UI
    /// prefs); with nothing installed — the gallery, a test — the widths last
    /// the session and no longer.
    /// </summary>
    public class Table : DataGrid
    {
        private static readonly Dictionary<string, double[]> SessionWidths = new Dictionary<string, double[]>();

        /// <summary>Whether a column of this table has been dragged to a width.</summary>
        private bool _dragged;

        /// <summary>Read the widths saved for a columns key; null when none are.</summary>
        public static Func<string, double[]?>? LoadWidths { get; set; }

        /// <summary>Persist the widths for a columns key.</summary>
        public static Action<string, double[]>? StoreWidths { get; set; }

        static Table()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Table), new FrameworkPropertyMetadata(typeof(Table)));
        }

        public Table()
        {
            AutoGenerateColumns = false;
            CanUserAddRows = false;
            CanUserDeleteRows = false;
            CanUserReorderColumns = false;
            CanUserSortColumns = false;
            CanUserResizeRows = false;
            CanUserResizeColumns = true;
            IsReadOnly = true;
            HeadersVisibility = DataGridHeadersVisibility.Column;
            GridLinesVisibility = DataGridGridLinesVisibility.None;
            SelectionMode = DataGridSelectionMode.Single;
            SelectionUnit = DataGridSelectionUnit.FullRow;
            RowHeaderWidth = 0;
            EnableRowVirtualization = true;
            // Pixel offsets (still virtualized) so the wheel eases instead of
            // jumping a row at a time.
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Pixel);
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(this, true);
            Loaded += (_, __) => RestoreWidths();
            Unloaded += (_, __) => SaveWidths();
            // The gripper between two column headers, let go of: this is the
            // one thing that makes a width the user's rather than the layout's.
            AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, __) =>
            {
                _dragged = true;
                SaveWidths();
            }), true);
        }

        public static readonly DependencyProperty DenseProperty = DependencyProperty.Register(
            nameof(Dense), typeof(bool), typeof(Table), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty ColumnsKeyProperty = DependencyProperty.Register(
            nameof(ColumnsKey), typeof(string), typeof(Table), new FrameworkPropertyMetadata(null));

        /// <summary>Tabular figures for a column of amounts. Attach to a DataGridColumn.</summary>
        public static readonly DependencyProperty NumericProperty = DependencyProperty.RegisterAttached(
            "Numeric", typeof(bool), typeof(Table), new FrameworkPropertyMetadata(false));

        public static bool GetNumeric(DependencyObject element) => (bool)element.GetValue(NumericProperty);
        public static void SetNumeric(DependencyObject element, bool value) => element.SetValue(NumericProperty, value);

        public bool Dense
        {
            get => (bool)GetValue(DenseProperty);
            set => SetValue(DenseProperty, value);
        }

        public string? ColumnsKey
        {
            get => (string?)GetValue(ColumnsKeyProperty);
            set => SetValue(ColumnsKeyProperty, value);
        }

        /// <summary>
        /// Selection moves without the pointer (arrow keys, a pager jump,
        /// arriving from another screen), so the row brings itself on screen.
        /// </summary>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            var target = SelectedItem;
            if (target == null) return;
            ScrollIntoView(target);
            // …and again after layout. An arrow key can re-page the list, so
            // this fires while the new page is still being generated: rows are
            // virtualized, the container does not exist yet, and the call
            // above silently does nothing. The second one is a no-op when the
            // row is already on screen.
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    if (ReferenceEquals(SelectedItem, target)) ScrollIntoView(target);
                }));
        }

        private void RestoreWidths()
        {
            if (ColumnsKey == null) return;
            var widths = LoadWidths?.Invoke(ColumnsKey)
                         ?? (SessionWidths.TryGetValue(ColumnsKey, out var session) ? session : null);
            if (widths == null) return;
            // Drop restored widths that no longer match the table (a screen
            // whose column set changed since they were saved).
            if (widths.Length != Columns.Count)
            {
                SessionWidths.Remove(ColumnsKey);
                StoreWidths?.Invoke(ColumnsKey, new double[0]);
                return;
            }
            // The last column keeps its star so the table still fills its
            // space — the widths before it are what the drag settled.
            for (var i = 0; i < widths.Length - 1; i++)
            {
                if (widths[i] > 0) Columns[i].Width = new DataGridLength(widths[i]);
            }
        }

        private void SaveWidths()
        {
            // Only what the user dragged. Restoring widths turns those columns
            // from auto into fixed pixels, so anything measured afterwards
            // would look dragged too — and a table laid out narrow (beside an
            // open detail panel) would then save that narrowness as if it had
            // been asked for, and carry it back to the full-width screen.
            if (!_dragged || ColumnsKey == null || Columns.Count == 0) return;
            var widths = Columns.Select(c => c.ActualWidth).ToArray();
            SessionWidths[ColumnsKey] = widths;
            StoreWidths?.Invoke(ColumnsKey, widths);
        }
    }
}
