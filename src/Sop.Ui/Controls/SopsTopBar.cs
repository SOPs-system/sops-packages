using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Canonical top bar header component from SOPs Shell architecture (sops-backup/wpf-beta).
    /// Displays store details, connection status with 8px dot gap, search, notifications, and user avatar.
    /// </summary>
    public class SopsTopBar : Control
    {
        static SopsTopBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SopsTopBar), new FrameworkPropertyMetadata(typeof(SopsTopBar)));
        }

        public static readonly DependencyProperty StoreNameProperty =
            DependencyProperty.Register(nameof(StoreName), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Smart Operating Pharmacy"));

        public string StoreName
        {
            get => (string)GetValue(StoreNameProperty);
            set => SetValue(StoreNameProperty, value);
        }

        public static readonly DependencyProperty StoreBranchProperty =
            DependencyProperty.Register(nameof(StoreBranch), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Branch 104 — Downtown Cairo"));

        public string StoreBranch
        {
            get => (string)GetValue(StoreBranchProperty);
            set => SetValue(StoreBranchProperty, value);
        }

        public static readonly DependencyProperty IsOnlineProperty =
            DependencyProperty.Register(nameof(IsOnline), typeof(bool), typeof(SopsTopBar), new PropertyMetadata(true));

        public bool IsOnline
        {
            get => (bool)GetValue(IsOnlineProperty);
            set => SetValue(IsOnlineProperty, value);
        }

        public static readonly DependencyProperty OnlineTextProperty =
            DependencyProperty.Register(nameof(OnlineText), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Online"));

        public string OnlineText
        {
            get => (string)GetValue(OnlineTextProperty);
            set => SetValue(OnlineTextProperty, value);
        }

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(SopsTopBar), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public static readonly DependencyProperty SearchPlaceholderProperty =
            DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Quick search drugs, barcodes, customers..."));

        public string SearchPlaceholder
        {
            get => (string)GetValue(SearchPlaceholderProperty);
            set => SetValue(SearchPlaceholderProperty, value);
        }

        public static readonly DependencyProperty NotificationsCountProperty =
            DependencyProperty.Register(nameof(NotificationsCount), typeof(int), typeof(SopsTopBar), new PropertyMetadata(3));

        public int NotificationsCount
        {
            get => (int)GetValue(NotificationsCountProperty);
            set => SetValue(NotificationsCountProperty, value);
        }

        public static readonly DependencyProperty UserNameProperty =
            DependencyProperty.Register(nameof(UserName), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Ahmed M."));

        public string UserName
        {
            get => (string)GetValue(UserNameProperty);
            set => SetValue(UserNameProperty, value);
        }

        public static readonly DependencyProperty UserRoleProperty =
            DependencyProperty.Register(nameof(UserRole), typeof(string), typeof(SopsTopBar), new PropertyMetadata("Cashier"));

        public string UserRole
        {
            get => (string)GetValue(UserRoleProperty);
            set => SetValue(UserRoleProperty, value);
        }

        public static readonly DependencyProperty UserInitialsProperty =
            DependencyProperty.Register(nameof(UserInitials), typeof(string), typeof(SopsTopBar), new PropertyMetadata("AM"));

        public string UserInitials
        {
            get => (string)GetValue(UserInitialsProperty);
            set => SetValue(UserInitialsProperty, value);
        }

        public static readonly RoutedEvent NotificationsClickEvent =
            EventManager.RegisterRoutedEvent(nameof(NotificationsClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsTopBar));

        public event RoutedEventHandler NotificationsClick
        {
            add => AddHandler(NotificationsClickEvent, value);
            remove => RemoveHandler(NotificationsClickEvent, value);
        }

        public static readonly RoutedEvent HelpClickEvent =
            EventManager.RegisterRoutedEvent(nameof(HelpClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsTopBar));

        public event RoutedEventHandler HelpClick
        {
            add => AddHandler(HelpClickEvent, value);
            remove => RemoveHandler(HelpClickEvent, value);
        }

        public static readonly RoutedEvent ToggleSidePanelClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ToggleSidePanelClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SopsTopBar));

        public event RoutedEventHandler ToggleSidePanelClick
        {
            add => AddHandler(ToggleSidePanelClickEvent, value);
            remove => RemoveHandler(ToggleSidePanelClickEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_NotificationsButton") is ButtonBase notifBtn)
            {
                notifBtn.Click += (s, e) =>
                {
                    RaiseEvent(new RoutedEventArgs(NotificationsClickEvent, this));
                    RaiseEvent(new RoutedEventArgs(ToggleSidePanelClickEvent, this));
                };
            }
            if (GetTemplateChild("PART_HelpButton") is ButtonBase helpBtn)
            {
                helpBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(HelpClickEvent, this));
            }
        }
    }
}
