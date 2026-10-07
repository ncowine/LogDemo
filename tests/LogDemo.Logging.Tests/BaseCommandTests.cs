using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace LogDemo.Logging.Tests;

public sealed class BaseCommandTests
{
    private readonly FakeLogger logger = new FakeLogger();

    [Fact]
    public async Task Success_is_logged_with_elapsed_time_inside_a_command_scope()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, _ => Task.CompletedTask);

        await command.ExecuteAsync();

        FakeLogRecord completed = this.logger.LatestRecord;
        Assert.Equal(2001, completed.Id.Id);
        Assert.Equal(LogLevel.Debug, completed.Level);
        Assert.Contains(completed.Scopes, s => s is IEnumerable<KeyValuePair<string, object?>> pairs
            && pairs.Any(p => p.Key == "CommandName" && Equals(p.Value, nameof(TestAsyncCommand))));
    }

    [Fact]
    public async Task Handled_failure_is_logged_as_warning_and_not_rethrown()
    {
        TestAsyncCommand command = new TestAsyncCommand(
            this.logger,
            _ => throw new TimeoutException("backend down"),
            handle: ex => ex is TimeoutException);

        await command.ExecuteAsync();

        Assert.Equal(LogLevel.Warning, this.logger.LatestRecord.Level);
        Assert.IsType<TimeoutException>(this.logger.LatestRecord.Exception);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task Unexpected_failure_is_logged_as_error_marked_and_rethrown()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, _ => throw new InvalidOperationException("bug"));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.Equal(LogLevel.Error, this.logger.LatestRecord.Level);
        Assert.True(thrown.IsLogged()); // the global handler won't log the stack trace a second time
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task Cancellation_is_logged_as_information_not_as_error()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, token => Task.Delay(TimeSpan.FromSeconds(10), token));

        Task running = command.ExecuteAsync();
        command.Cancel();
        await running;

        Assert.Equal(2003, this.logger.LatestRecord.Id.Id);
        Assert.Equal(LogLevel.Information, this.logger.LatestRecord.Level);
    }

    [Fact]
    public async Task Async_command_cannot_run_twice_at_the_same_time()
    {
        TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();
        TestAsyncCommand command = new TestAsyncCommand(this.logger, _ => gate.Task);

        Task running = command.ExecuteAsync();

        Assert.True(command.IsExecuting);
        Assert.False(command.CanExecute());

        gate.SetResult(true);
        await running;
        Assert.True(command.CanExecute());
    }

    [Fact]
    public void Throwing_CanExecute_is_logged_and_treated_as_disabled()
    {
        TestSyncCommand command = new TestSyncCommand(this.logger, canExecute: () => throw new InvalidOperationException("bad state"));

        Assert.False(command.CanExecute());
        Assert.Equal(2006, this.logger.LatestRecord.Id.Id);
    }

    [Fact]
    public void Sync_command_failure_is_logged_once_and_rethrown()
    {
        TestSyncCommand command = new TestSyncCommand(this.logger, execute: () => throw new InvalidOperationException("bug"));

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => command.Execute());

        Assert.Equal(LogLevel.Error, this.logger.LatestRecord.Level);
        Assert.True(thrown.IsLogged());
    }

    [Fact]
    public async Task Typed_parameter_reaches_the_command()
    {
        int received = 0;
        TestTypedAsyncCommand command = new TestTypedAsyncCommand(this.logger, value => received = value);

        command.Execute(7);
        await command.ExecuteAsync(42);

        Assert.Equal(42, received);
        Assert.True(command.CanExecute(1));
    }

    [Fact]
    public void Wrong_parameter_type_disables_the_command_instead_of_throwing()
    {
        TestTypedAsyncCommand command = new TestTypedAsyncCommand(this.logger, _ => { });

        Assert.False(command.CanExecute("not an int"));
        Assert.False(command.CanExecute(null)); // int cannot be null

        command.Execute("not an int");
        Assert.Equal(2002, this.logger.LatestRecord.Id.Id);
    }

    private sealed class TestTypedAsyncCommand : DelegateBaseAsyncCommand<int>
    {
        private readonly Action<int> body;

        public TestTypedAsyncCommand(ILogger logger, Action<int> body)
            : base(logger)
        {
            this.body = body;
        }

        protected override bool CanInvoke(int parameter) => parameter > 0;

        protected override Task InvokeAsync(int parameter, CancellationToken cancellationToken)
        {
            this.body(parameter);
            return Task.CompletedTask;
        }
    }

    private sealed class TestAsyncCommand : DelegateBaseAsyncCommand
    {
        private readonly Func<CancellationToken, Task> body;
        private readonly Func<Exception, bool>? handle;

        public TestAsyncCommand(ILogger logger, Func<CancellationToken, Task> body, Func<Exception, bool>? handle = null)
            : base(logger)
        {
            this.body = body;
            this.handle = handle;
        }

        protected override Task InvokeAsync(CancellationToken cancellationToken) => this.body(cancellationToken);

        protected override bool TryHandleFailure(Exception exception) => this.handle?.Invoke(exception) ?? false;
    }

    private sealed class TestSyncCommand : DelegateBaseCommand
    {
        private readonly Action? execute;
        private readonly Func<bool>? canExecute;

        public TestSyncCommand(ILogger logger, Action? execute = null, Func<bool>? canExecute = null)
            : base(logger)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        protected override bool CanInvoke() => this.canExecute?.Invoke() ?? true;

        protected override void Invoke() => this.execute?.Invoke();
    }
}
