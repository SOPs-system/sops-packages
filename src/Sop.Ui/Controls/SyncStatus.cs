using System;
using System.Windows;
using System.Windows.Controls;

namespace Sop.Ui.Controls
{
    public enum SyncState
    {
        Synced,
        Syncing,
        Pending,
        Offline,
        Conflict
    }

    /// <summary>
    /// Visual indicator and action panel for offline-first data synchronization.
    /// Communicates network connection, pending outbound changes, conflicts, and sync progress.
    /// </summary>
    public class SyncStatus : Control
    {
        static SyncStatus()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SyncStatus), new FrameworkPropertyMetadata(typeof(SyncStatus)));
        }

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(SyncState), typeof(SyncStatus),
                new PropertyMetadata(SyncState.Synced));

        public static readonly DependencyProperty PendingCountProperty =
            DependencyProperty.Register(nameof(PendingCount), typeof(int), typeof(SyncStatus),
                new PropertyMetadata(0));

        public static readonly DependencyProperty ConflictCountProperty =
            DependencyProperty.Register(nameof(ConflictCount), typeof(int), typeof(SyncStatus),
                new PropertyMetadata(0));

        public static readonly DependencyProperty LastSyncedTextProperty =
            DependencyProperty.Register(nameof(LastSyncedText), typeof(string), typeof(SyncStatus),
                new PropertyMetadata("Just now"));

        public static readonly DependencyProperty ShowActionProperty =
            DependencyProperty.Register(nameof(ShowAction), typeof(bool), typeof(SyncStatus),
                new PropertyMetadata(true));

        public static readonly RoutedEvent SyncNowClickedEvent =
            EventManager.RegisterRoutedEvent(nameof(SyncNowClicked), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(SyncStatus));

        public static readonly RoutedEvent ReviewConflictsClickedEvent =
            EventManager.RegisterRoutedEvent(nameof(ReviewConflictsClicked), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(SyncStatus));

        public SyncState State
        {
            get => (SyncState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public int PendingCount
        {
            get => (int)GetValue(PendingCountProperty);
            set => SetValue(PendingCountProperty, value);
        }

        public int ConflictCount
        {
            get => (int)GetValue(ConflictCountProperty);
            set => SetValue(ConflictCountProperty, value);
        }

        public string LastSyncedText
        {
            get => (string)GetValue(LastSyncedTextProperty);
            set => SetValue(LastSyncedTextProperty, value);
        }

        public bool ShowAction
        {
            get => (bool)GetValue(ShowActionProperty);
            set => SetValue(ShowActionProperty, value);
        }

        public event RoutedEventHandler SyncNowClicked
        {
            add => AddHandler(SyncNowClickedEvent, value);
            remove => RemoveHandler(SyncNowClickedEvent, value);
        }

        public event RoutedEventHandler ReviewConflictsClicked
        {
            add => AddHandler(ReviewConflictsClickedEvent, value);
            remove => RemoveHandler(ReviewConflictsClickedEvent, value);
        }

        public SyncStatus()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is FrameworkElement el && el.Tag is string tag)
            {
                if (tag == "SYNC_NOW")
                {
                    RaiseEvent(new RoutedEventArgs(SyncNowClickedEvent));
                    e.Handled = true;
                }
                else if (tag == "REVIEW_CONFLICTS")
                {
                    RaiseEvent(new RoutedEventArgs(ReviewConflictsClickedEvent));
                    e.Handled = true;
                }
            }
        }
    }
}
