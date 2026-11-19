using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using Sop.Ui;

namespace Sops.Pharmacy.Server.Management.ViewModels
{
    public class NavItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly ManagementClient _client;
        private readonly DispatcherTimer _pollTimer;

        private string _activeViewKey = "Overview";
        private bool _isServerRunning;
        private string _uptime = "00:00:00";
        private int _activeTerminalsCount;
        private long _databaseSizeBytes;
        private long _memoryUsageMb;
        private int _pendingOutboxCount;
        private int _pendingInboxCount;
        private string _serverStatusText = "Connecting...";
        private bool _isBusy;
        private string _lastUpdatedText = "Just now";

        public ManagementClient Client => _client;

        public string ActiveViewKey
        {
            get => _activeViewKey;
            set => SetField(ref _activeViewKey, value);
        }

        public bool IsServerRunning
        {
            get => _isServerRunning;
            set => SetField(ref _isServerRunning, value);
        }

        public string Uptime
        {
            get => _uptime;
            set => SetField(ref _uptime, value);
        }

        public int ActiveTerminalsCount
        {
            get => _activeTerminalsCount;
            set => SetField(ref _activeTerminalsCount, value);
        }

        public long DatabaseSizeBytes
        {
            get => _databaseSizeBytes;
            set => SetField(ref _databaseSizeBytes, value);
        }

        public string DatabaseSizeFormatted => $"{_databaseSizeBytes / 1024.0:N1} KB";

        public long MemoryUsageMb
        {
            get => _memoryUsageMb;
            set => SetField(ref _memoryUsageMb, value);
        }

        public int PendingOutboxCount
        {
            get => _pendingOutboxCount;
            set => SetField(ref _pendingOutboxCount, value);
        }

        public int PendingInboxCount
        {
            get => _pendingInboxCount;
            set => SetField(ref _pendingInboxCount, value);
        }

        public string ServerStatusText
        {
            get => _serverStatusText;
            set => SetField(ref _serverStatusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value);
        }

        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set => SetField(ref _lastUpdatedText, value);
        }

        public ObservableCollection<NavItem> NavigationItems { get; } = new ObservableCollection<NavItem>
        {
            new NavItem { Key = "Overview", Title = "Overview", TitleAr = "نظرة عامة", Icon = "Dashboard" },
            new NavItem { Key = "ServerControl", Title = "Server Control", TitleAr = "إدارة السيرفر", Icon = "Server" },
            new NavItem { Key = "Devices", Title = "Terminals & Devices", TitleAr = "الأجهزة ونقاط البيع", Icon = "Devices" },
            new NavItem { Key = "Database", Title = "SQLCipher Database", TitleAr = "قاعدة البيانات", Icon = "Database" },
            new NavItem { Key = "Sync", Title = "Cloud Sync & Outbox", TitleAr = "المزامنة السحابية", Icon = "Sync" },
            new NavItem { Key = "BackupRestore", Title = "Backup & Restore", TitleAr = "النسخ الاحتياطي", Icon = "Backup" },
            new NavItem { Key = "Approvals", Title = "Change Approvals", TitleAr = "طلبات التعديل", Icon = "CheckCircle" },
            new NavItem { Key = "Licensing", Title = "Licensing & Seats", TitleAr = "الترخيص والمقاعد", Icon = "Key" },
            new NavItem { Key = "Users", Title = "Users & Shifts", TitleAr = "المستخدمين والورديات", Icon = "Users" },
            new NavItem { Key = "Logs", Title = "Audit Trail & Logs", TitleAr = "سجل العمليات", Icon = "List" },
            new NavItem { Key = "Settings", Title = "Settings", TitleAr = "الإعدادات", Icon = "Settings" }
        };

        public MainViewModel(ManagementClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _pollTimer.Tick += async (s, e) => await RefreshStatusAsync();
            _pollTimer.Start();

            _ = Task.Run(async () =>
            {
                await _client.EnsureServerRunningAsync();
                await RefreshStatusAsync();
            });
        }

        public async Task RefreshStatusAsync()
        {
            try
            {
                var summary = await _client.GetStatusAsync();
                IsServerRunning = summary.IsRunning;
                Uptime = summary.Uptime;
                ActiveTerminalsCount = summary.ActiveTerminalsCount;
                DatabaseSizeBytes = summary.DatabaseSizeBytes;
                MemoryUsageMb = summary.MemoryUsageMb;
                PendingOutboxCount = summary.PendingOutbox;
                PendingInboxCount = summary.PendingInbox;
                ServerStatusText = summary.IsRunning ? $"Running on Port {summary.Port}" : "Service Disconnected";
                LastUpdatedText = DateTime.Now.ToString("T");
                OnPropertyChanged(nameof(DatabaseSizeFormatted));
            }
            catch (Exception ex)
            {
                IsServerRunning = false;
                ServerStatusText = $"Error: {ex.Message}";
            }
        }

        public void Navigate(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                ActiveViewKey = key;
            }
        }

        public void ToggleTheme()
        {
            Theme.Mode = Theme.Mode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        }

        public void ToggleRtl()
        {
            Theme.IsRtl = !Theme.IsRtl;
        }

        public void Dispose()
        {
            _pollTimer.Stop();
        }
    }
}
