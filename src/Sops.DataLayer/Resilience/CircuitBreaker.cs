using System;
using System.Threading;

namespace Sops.DataLayer.Resilience
{
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Thread-safe circuit breaker preventing cascading failures when a backend is unreachable.
    /// </summary>
    public sealed class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _breakDuration;
        private readonly object _lock = new object();

        private int _consecutiveFailures;
        private DateTimeOffset _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;

        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    if (_state == CircuitState.Open && (DateTimeOffset.UtcNow - _lastFailureTime) >= _breakDuration)
                    {
                        _state = CircuitState.HalfOpen;
                    }
                    return _state;
                }
            }
        }

        public CircuitBreaker(int failureThreshold = 5, TimeSpan? breakDuration = null)
        {
            _failureThreshold = Math.Max(1, failureThreshold);
            _breakDuration = breakDuration ?? TimeSpan.FromSeconds(15);
        }

        public bool AllowExecution()
        {
            lock (_lock)
            {
                if (_state == CircuitState.Closed) return true;

                if (_state == CircuitState.Open)
                {
                    if ((DateTimeOffset.UtcNow - _lastFailureTime) >= _breakDuration)
                    {
                        _state = CircuitState.HalfOpen;
                        return true;
                    }
                    return false;
                }

                // HalfOpen allows probe execution
                return true;
            }
        }

        public void RecordSuccess()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _state = CircuitState.Closed;
            }
        }

        public void RecordFailure()
        {
            lock (_lock)
            {
                _consecutiveFailures++;
                _lastFailureTime = DateTimeOffset.UtcNow;

                if (_consecutiveFailures >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _state = CircuitState.Closed;
            }
        }
    }
}
