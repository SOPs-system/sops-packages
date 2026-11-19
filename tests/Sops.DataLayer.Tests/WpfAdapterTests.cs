using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Wpf;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class WpfAdapterTests
    {
        [Fact]
        public void ObservableRequestState_Transitions_And_Fires_PropertyChange()
        {
            var state = new ObservableRequestState<string>();
            var changedProperties = new List<string>();
            state.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
            };

            Assert.True(state.IsIdle);
            Assert.False(state.IsLoading);
            Assert.False(state.IsSuccess);

            // Set loading
            state.SetLoading();
            Assert.True(state.IsLoading);
            Assert.True(state.IsBusy);
            Assert.Contains(nameof(state.IsLoading), changedProperties);

            // Set success
            state.SetResult(DataResult<string>.Success("Product Data"));
            Assert.True(state.IsSuccess);
            Assert.False(state.IsLoading);
            Assert.Equal("Product Data", state.Data);

            // Set error
            state.SetResult(DataResult<string>.Failure(DataOutcome.NotFound, DataError.NotFound("Drug not found")));
            Assert.True(state.HasError);
            Assert.Equal("Drug not found", state.ErrorMessage);
        }

        [Fact]
        public async Task AsyncCommand_Prevents_Double_Execution_While_Busy()
        {
            int executionCount = 0;
            var tcs = new TaskCompletionSource<bool>();

            var command = new AsyncCommand(async (param, ct) =>
            {
                Interlocked.Increment(ref executionCount);
                await tcs.Task; // Hold execution
            });

            Assert.True(command.CanExecute(null));

            // Start first execution
            command.Execute(null);

            // Second execution should be blocked because CanExecute is false while busy
            Assert.False(command.CanExecute(null));
            command.Execute(null);

            Assert.Equal(1, executionCount);

            // Release
            tcs.SetResult(true);
            await Task.Delay(50); // Allow async void continuation to complete

            Assert.True(command.CanExecute(null));
        }

        [Fact]
        public void ViewModelRequestScope_Cancels_On_Dispose()
        {
            var scope = new ViewModelRequestScope();
            Assert.False(scope.IsCancellationRequested);
            Assert.False(scope.Token.IsCancellationRequested);

            scope.Dispose();

            Assert.True(scope.IsCancellationRequested);
            Assert.True(scope.Token.IsCancellationRequested);
        }
    }
}
