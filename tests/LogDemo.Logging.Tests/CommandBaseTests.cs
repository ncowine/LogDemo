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

public sealed class CommandBaseTests
{
    private readonly FakeLogger logger = new FakeLogger();

    [Fact]
    public async Task Success_is_logged_with_elapsed_time_inside_a_command_scope()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, _ => Task.CompletedTask);

        await command.ExecuteAsync(null);

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

        await command.ExecuteAsync(null);

        Assert.Equal(LogLevel.Warning, this.logger.LatestRecord.Level);
        Assert.IsType<TimeoutException>(this.logger.LatestRecord.Exception);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task Unexpected_failure_is_logged_as_error_marked_and_rethrown()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, _ => throw new InvalidOperationException("bug"));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(null));

        Assert.Equal(LogLevel.Error, this.logger.LatestRecord.Level);
        Assert.True(thrown.IsLogged()); // the global handler won't log the stack trace a second time
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task Cancellation_is_logged_as_information_not_as_error()
    {
        TestAsyncCommand command = new TestAsyncCommand(this.logger, token => Task.Delay(TimeSpan.FromSeconds(10), token));

        Task running = command.ExecuteAsync(null);
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

        Task running = command.ExecuteAsync(null);

        Assert.True(command.IsExecuting);
        Assert.False(command.CanExecute(null));

        gate.SetResult(true);
        await running;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void Throwing_CanExecute_is_logged_and_treated_as_disabled()
    {
        TestSyncCommand command = new TestSyncCommand(this.logger, canExecute: () => throw new InvalidOperationException("bad state"));

        Assert.False(command.CanExecute(null));
        Assert.Equal(2006, this.logger.LatestRecord.Id.Id);
    }

    [Fact]
    public void Sync_command_failure_is_logged_once_and_rethrown()
    {
        TestSyncCommand command = new TestSyncCommand(this.logger, execute: () => throw new InvalidOperationException("bug"));

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => command.Execute(null));

        Assert.Equal(LogLevel.Error, this.logger.LatestRecord.Level);
        Assert.True(thrown.IsLogged());
    }

    private sealed class TestAsyncCommand : AsyncCommandBase
    {
        private readonly Func<CancellationToken, Task> body;
        private readonly Func<Exception, bool>? handle;

        public TestAsyncCommand(ILogger logger, Func<CancellationToken, Task> body, Func<Exception, bool>? handle = null)
            : base(logger)
        {
            this.body = body;
            this.handle = handle;
        }

        protected override Task ExecuteCoreAsync(object? parameter, CancellationToken cancellationToken) => this.body(cancellationToken);

        protected override bool TryHandleFailure(Exception exception) => this.handle?.Invoke(exception) ?? false;
    }

    private sealed class TestSyncCommand : CommandBase
    {
        private readonly Action? execute;
        private readonly Func<bool>? canExecute;

        public TestSyncCommand(ILogger logger, Action? execute = null, Func<bool>? canExecute = null)
            : base(logger)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        protected override bool CanExecuteCore(object? parameter) => this.canExecute?.Invoke() ?? true;

        protected override void ExecuteCore(object? parameter) => this.execute?.Invoke();
    }
}
