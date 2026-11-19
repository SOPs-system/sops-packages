using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sops.DataLayer.Wpf
{
    /// <summary>
    /// Production-grade asynchronous ICommand implementation for WPF ViewModels with automatic busy tracking and cancellation.
    /// </summary>
    public sealed class AsyncCommand : ICommand
    {
        private readonly Func<object?, CancellationToken, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private readonly IUiDispatcher _dispatcher;

        private CancellationTokenSource? _cts;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged;

        public bool IsExecuting => _isExecuting;

        public AsyncCommand(
            Func<object?, CancellationToken, Task> executeAsync,
            Predicate<object?>? canExecute = null,
            IUiDispatcher? dispatcher = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _dispatcher = dispatcher ?? new WpfDispatcherAdapter();
        }

        public AsyncCommand(
            Func<Task> executeAsync,
            Func<bool>? canExecute = null,
            IUiDispatcher? dispatcher = null)
            : this((_, ct) => executeAsync(), canExecute != null ? _ => canExecute() : null, dispatcher)
        {
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _ = ExecuteAsync(parameter);
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            _cts = new CancellationTokenSource();

            try
            {
                await _executeAsync(parameter, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AsyncCommand execution failed: {ex}");
            }
            finally
            {
                _isExecuting = false;
                _cts?.Dispose();
                _cts = null;
                RaiseCanExecuteChanged();
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void RaiseCanExecuteChanged()
        {
            _dispatcher.Invoke(() =>
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}
