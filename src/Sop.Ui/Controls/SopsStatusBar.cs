using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Canonical bottom status bar from SOPs Shell architecture (sops-backup/wpf-beta).
    /// Displays database connectivity status, cashier shift timer, live clock, and shortcut keys.
    /// </summary>
    public class SopsStatusBar : Control
    {
        private DispatcherTimer? _clockTimer;

        static SopsStatusBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsStatusBar), new FrameworkPropertyMetadata(typeof(SopsStatusBar)));
        }

        public SopsStatusBar()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty StorageEngineProperty =
            DependencyProperty.Register(nameof(StorageEngine), typeof(string), typeof(SopsStatusBar), new PropertyMetadata("Cloud Database: Online"));

        public string StorageEngine
        {
            get => (string)GetValue(StorageEngineProperty);
            set => SetValue(StorageEngineProperty, value);
        }

        public static readonly DependencyProperty IsStorageConnectedProperty =
            DependencyProperty.Register(nameof(IsStorageConnected), typeof(bool), typeof(SopsStatusBar), new PropertyMetadata(true));

        public bool IsStorageConnected
        {
            get => (bool)GetValue(IsStorageConnectedProperty);
            set => SetValue(IsStorageConnectedProperty, value);
        }

        public static readonly DependencyProperty ShiftNameProperty =
            DependencyProperty.Register(nameof(ShiftName), typeof(string), typeof(SopsStatusBar), new PropertyMetadata("Morning Shift 104"));

        public string ShiftName
        {
            get => (string)GetValue(ShiftNameProperty);
            set => SetValue(ShiftNameProperty, value);
        }

        public static readonly DependencyProperty SessionDurationProperty =
            DependencyProperty.Register(nameof(SessionDuration), typeof(string), typeof(SopsStatusBar), new PropertyMetadata("04:15:22"));

        public string SessionDuration
        {
            get => (string)GetValue(SessionDurationProperty);
            set => SetValue(SessionDurationProperty, value);
        }

        public static readonly DependencyProperty SystemClockProperty =
            DependencyProperty.Register(nameof(SystemClock), typeof(string), typeof(SopsStatusBar), new PropertyMetadata(DateTime.Now.ToString("HH:mm:ss")));

        public string SystemClock
        {
            get => (string)GetValue(SystemClockProperty);
            set => SetValue(SystemClockProperty, value);
        }

        public static readonly DependencyProperty ShortcutsHintProperty =
            DependencyProperty.Register(nameof(ShortcutsHint), typeof(string), typeof(SopsStatusBar), new PropertyMetadata("Shortcuts (F1)"));

        public string ShortcutsHint
        {
            get => (string)GetValue(ShortcutsHintProperty);
            set => SetValue(ShortcutsHintProperty, value);
        }

        public static readonly RoutedEvent ShiftClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ShiftClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsStatusBar));

        public event RoutedEventHandler ShiftClick
        {
            add => AddHandler(ShiftClickEvent, value);
            remove => RemoveHandler(ShiftClickEvent, value);
        }

        public static readonly RoutedEvent ShortcutsClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ShortcutsClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsStatusBar));

        public event RoutedEventHandler ShortcutsClick
        {
            add => AddHandler(ShortcutsClickEvent, value);
            remove => RemoveHandler(ShortcutsClickEvent, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_clockTimer == null)
            {
                _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _clockTimer.Tick += (s, args) => SystemClock = DateTime.Now.ToString("HH:mm:ss");
                _clockTimer.Start();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_ShiftButton") is ButtonBase shiftBtn)
            {
                shiftBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(ShiftClickEvent, this));
            }
            if (GetTemplateChild("PART_ShortcutsButton") is ButtonBase shortcutsBtn)
            {
                shortcutsBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(ShortcutsClickEvent, this));
            }
        }
    }
}
