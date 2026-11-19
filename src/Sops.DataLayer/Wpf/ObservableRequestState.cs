using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Wpf
{
    /// <summary>
    /// Complete set of UI-bound lifecycle and synchronization states for WPF MVVM presentation.
    /// </summary>
    public enum RequestUiStatus
    {
        Idle = 0,
        Loading = 1,
        Refreshing = 2,
        Success = 3,
        Empty = 4,
        Stale = 5,
        Offline = 6,
        Syncing = 7,
        Failed = 8,
        Cancelled = 9,
        // Error alias for backward compatibility:
        Error = Failed
    }

    /// <summary>
    /// Thread-safe observable state container designed for direct data binding in WPF MVVM views.
    /// Marshals all property notifications to the UI thread via IUiDispatcher.
    /// Protects against search query race conditions via sequence tracking.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public sealed class ObservableRequestState<T> : INotifyPropertyChanged
    {
        private readonly IUiDispatcher _dispatcher;
        private RequestUiStatus _status = RequestUiStatus.Idle;
        private T? _data;
        private string? _errorMessage;
        private string? _errorCode;
        private bool _isStale;
        private DateTimeOffset? _lastUpdated;
        private long _currentSequence = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableRequestState(IUiDispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher ?? new WpfDispatcherAdapter();
        }

        public RequestUiStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(IsRefreshing));
                    OnPropertyChanged(nameof(IsSuccess));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsStale));
                    OnPropertyChanged(nameof(IsOffline));
                    OnPropertyChanged(nameof(IsSyncing));
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(IsCancelled));
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        public bool IsIdle => _status == RequestUiStatus.Idle;
        public bool IsLoading => _status == RequestUiStatus.Loading;
        public bool IsRefreshing => _status == RequestUiStatus.Refreshing;
        public bool IsSuccess => _status == RequestUiStatus.Success;
        public bool IsEmpty => _status == RequestUiStatus.Empty;
        public bool IsStale => _status == RequestUiStatus.Stale || _isStale;
        public bool IsOffline => _status == RequestUiStatus.Offline;
        public bool IsSyncing => _status == RequestUiStatus.Syncing;
        public bool HasError => _status == RequestUiStatus.Failed || _status == RequestUiStatus.Error;
        public bool IsFailed => HasError;
        public bool IsCancelled => _status == RequestUiStatus.Cancelled;
        public bool IsBusy => _status == RequestUiStatus.Loading || _status == RequestUiStatus.Refreshing;

        public T? Data
        {
            get => _data;
            private set
            {
                _data = value;
                OnPropertyChanged();
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string? ErrorCode
        {
            get => _errorCode;
            private set
            {
                _errorCode = value;
                OnPropertyChanged();
            }
        }

        public DateTimeOffset? LastUpdated
        {
            get => _lastUpdated;
            private set
            {
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initiates a new operation sequence, transitioning state to Loading or Refreshing.
        /// Returns a unique sequence token to prevent older asynchronous queries from overwriting newer results.
        /// </summary>
        public long BeginRequest(bool isRefreshing = false)
        {
            long token = Interlocked.Increment(ref _currentSequence);
            SetLoading(isRefreshing);
            return token;
        }

        public void SetLoading(bool isRefreshing = false)
        {
            _dispatcher.Invoke(() =>
            {
                Status = isRefreshing ? RequestUiStatus.Refreshing : RequestUiStatus.Loading;
                ErrorMessage = null;
                ErrorCode = null;
            });
        }

        /// <summary>
        /// Applies an operation result only if it matches the current request sequence token,
        /// discarding stale search results from superseded queries.
        /// </summary>
        public bool SetResult(DataResult<T>? result, long sequenceToken)
        {
            if (sequenceToken < Interlocked.Read(ref _currentSequence))
            {
                return false;
            }

            SetResult(result);
            return true;
        }

        public void SetResult(DataResult<T>? result)
        {
            _dispatcher.Invoke(() =>
            {
                if (result == null)
                {
                    Status = RequestUiStatus.Failed;
                    ErrorMessage = "Null result received";
                    ErrorCode = "NULL_RESULT";
                    return;
                }

                if (result.IsSuccess)
                {
                    Data = result.Data;
                    ErrorMessage = null;
                    ErrorCode = null;
                    _isStale = result.Metadata?.IsStale ?? false;
                    LastUpdated = result.Metadata?.ServerTimestampUtc ?? DateTimeOffset.UtcNow;

                    if (result.Data == null)
                    {
                        Status = RequestUiStatus.Empty;
                    }
                    else if (result.Data is IEnumerable enumerable && !enumerable.GetEnumerator().MoveNext())
                    {
                        Status = RequestUiStatus.Empty;
                    }
                    else if (_isStale)
                    {
                        Status = RequestUiStatus.Stale;
                    }
                    else
                    {
                        Status = RequestUiStatus.Success;
                    }
                }
                else
                {
                    _isStale = result.Metadata?.IsStale ?? false;
                    ErrorMessage = result.Error?.Message ?? "Request failed";
                    ErrorCode = result.Error?.Code ?? result.Outcome.ToString();

                    if (result.Outcome == DataOutcome.ServerUnavailable || result.Outcome == DataOutcome.NetworkUnavailable)
                    {
                        Status = RequestUiStatus.Offline;
                    }
                    else if (result.Outcome == DataOutcome.PendingSynchronization)
                    {
                        Status = RequestUiStatus.Syncing;
                    }
                    else if (result.Outcome == DataOutcome.Cancelled)
                    {
                        Status = RequestUiStatus.Cancelled;
                    }
                    else
                    {
                        Status = RequestUiStatus.Failed;
                    }
                }
            });
        }

        /// <summary>Backward-compatible alias for SetResult.</summary>
        public void ApplyResult(DataResult<T>? result) => SetResult(result);

        public void SetCancelled()
        {
            _dispatcher.Invoke(() =>
            {
                Status = RequestUiStatus.Cancelled;
            });
        }

        public void SetSyncing()
        {
            _dispatcher.Invoke(() =>
            {
                Status = RequestUiStatus.Syncing;
            });
        }

        public void Reset()
        {
            _dispatcher.Invoke(() =>
            {
                Status = RequestUiStatus.Idle;
                Data = default;
                ErrorMessage = null;
                ErrorCode = null;
                _isStale = false;
                LastUpdated = null;
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_dispatcher.CheckAccess())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }
    }
}
