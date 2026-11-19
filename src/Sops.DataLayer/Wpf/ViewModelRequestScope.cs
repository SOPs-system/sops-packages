using System;
using System.Collections.Generic;
using System.Threading;

namespace Sops.DataLayer.Wpf
{
    /// <summary>
    /// Scopes in-flight data operations and event bus subscriptions to a ViewModel or View lifecycle.
    /// Automatically cancels all pending requests and releases all subscriptions when the screen is navigated away from or closed.
    /// </summary>
    public sealed class ViewModelRequestScope : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly CancellationToken _token;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly object _lock = new object();
        private bool _disposed;
        private bool _isCancelled;

        public CancellationToken Token => _isCancelled ? new CancellationToken(true) : _token;

        public bool IsCancellationRequested => _isCancelled || _token.IsCancellationRequested;

        public ViewModelRequestScope()
        {
            _cts = new CancellationTokenSource();
            _token = _cts.Token;
        }

        public CancellationTokenSource CreateLinkedSource(CancellationToken externalToken = default)
        {
            if (externalToken == default)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(_token);
            }
            return CancellationTokenSource.CreateLinkedTokenSource(_token, externalToken);
        }

        /// <summary>
        /// Registers an event bus subscription to be disposed automatically when this ViewModel scope ends.
        /// </summary>
        public void RegisterSubscription(IDisposable subscription)
        {
            if (subscription == null) return;
            lock (_lock)
            {
                if (_disposed)
                {
                    subscription.Dispose();
                    return;
                }
                _subscriptions.Add(subscription);
            }
        }

        public void CancelAll()
        {
            _isCancelled = true;
            if (!_cts.IsCancellationRequested)
            {
                try { _cts.Cancel(); } catch { }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                CancelAll();

                lock (_lock)
                {
                    foreach (var sub in _subscriptions)
                    {
                        try { sub.Dispose(); } catch { }
                    }
                    _subscriptions.Clear();
                }

                try { _cts.Dispose(); } catch { }
            }
        }
    }
}
