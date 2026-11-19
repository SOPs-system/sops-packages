using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Wpf;

namespace Sops.DataLayer.Events
{
    /// <summary>
    /// In-process thread-safe event bus with exception isolation and UI dispatcher compatibility.
    /// </summary>
    public sealed class DataLayerEventBus : IDataLayerEventBus
    {
        private sealed class SubscriptionToken : IDisposable
        {
            private readonly Action _unsubscribeAction;
            private bool _disposed;

            public SubscriptionToken(Action unsubscribeAction)
            {
                _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _unsubscribeAction();
                }
            }
        }

        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Action<object>>> _subscriptions
            = new ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Action<object>>>();

        private readonly IUiDispatcher _dispatcher;
        private readonly ILogger<DataLayerEventBus>? _logger;

        public DataLayerEventBus(IUiDispatcher? dispatcher = null, ILogger<DataLayerEventBus>? logger = null)
        {
            _dispatcher = dispatcher ?? new WpfDispatcherAdapter();
            _logger = logger;
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            if (eventData == null) return;

            var eventType = typeof(TEvent);
            if (_subscriptions.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers.Values)
                {
                    try
                    {
                        handler(eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception in subscriber for event {EventType}", eventType.Name);
                    }
                }
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var id = Guid.NewGuid();
            var eventType = typeof(TEvent);
            var handlers = _subscriptions.GetOrAdd(eventType, _ => new ConcurrentDictionary<Guid, Action<object>>());

            handlers.TryAdd(id, obj => handler((TEvent)obj));

            return new SubscriptionToken(() =>
            {
                if (_subscriptions.TryGetValue(eventType, out var dict))
                {
                    dict.TryRemove(id, out _);
                }
            });
        }

        public IDisposable SubscribeOnUi<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return Subscribe<TEvent>(evt =>
            {
                _dispatcher.Invoke(() => handler(evt));
            });
        }

        public void ClearSubscriptions()
        {
            _subscriptions.Clear();
            _logger?.LogDebug("Cleared all event bus subscriptions");
        }
    }
}
