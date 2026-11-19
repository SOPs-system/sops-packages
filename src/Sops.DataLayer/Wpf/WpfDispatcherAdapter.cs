using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.DataLayer.Wpf
{
    /// <summary>
    /// Thread-safe abstraction over UI dispatcher to marshal updates to the WPF UI thread.
    /// </summary>
    public interface IUiDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
    }

    /// <summary>
    /// Dispatcher adapter compatible with WPF Dispatcher on .NET 4.8 and SynchronizationContext on netstandard2.0.
    /// </summary>
    public sealed class WpfDispatcherAdapter : IUiDispatcher
    {
#if NETFRAMEWORK
        private readonly System.Windows.Threading.Dispatcher? _dispatcher;

        public WpfDispatcherAdapter(System.Windows.Threading.Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher ?? (System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : System.Windows.Threading.Dispatcher.CurrentDispatcher);
        }

        public bool CheckAccess()
        {
            return _dispatcher == null || _dispatcher.CheckAccess();
        }

        public void Invoke(Action action)
        {
            if (action == null) return;
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (action == null) return Task.FromResult(0);
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
                return Task.FromResult(0);
            }

            var tcs = new TaskCompletionSource<bool>();
            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));
            return tcs.Task;
        }
#else
        private readonly SynchronizationContext? _syncContext;

        public WpfDispatcherAdapter(SynchronizationContext? syncContext = null)
        {
            _syncContext = syncContext ?? SynchronizationContext.Current;
        }

        public bool CheckAccess()
        {
            return _syncContext == null || SynchronizationContext.Current == _syncContext;
        }

        public void Invoke(Action action)
        {
            if (action == null) return;
            if (_syncContext == null || SynchronizationContext.Current == _syncContext)
            {
                action();
            }
            else
            {
                _syncContext.Send(_ => action(), state: null);
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (action == null) return Task.FromResult(0);
            if (_syncContext == null || SynchronizationContext.Current == _syncContext)
            {
                action();
                return Task.FromResult(0);
            }

            var tcs = new TaskCompletionSource<bool>();
            _syncContext.Post(_ =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, state: null);
            return tcs.Task;
        }
#endif
    }
}
