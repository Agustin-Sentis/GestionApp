using GastosBackend.Application.Interfaces;
using GastosBackend.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastosBackend.Tests.Services;

public class BudgetAlertDispatcherTests
{
    private sealed class FakeService : IBudgetAlertService
    {
        private readonly TaskCompletionSource<(Guid User, Guid Category, DateOnly Date)> _called = new();
        private readonly Exception? _failure;

        public FakeService(Exception? failure = null)
        {
            _failure = failure;
        }

        public Task<(Guid User, Guid Category, DateOnly Date)> Called => _called.Task;

        public Task CheckAndNotify(Guid userId, Guid categoryId, DateOnly date)
        {
            _called.SetResult((userId, categoryId, date));
            return _failure == null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }

    private sealed class CountingLogger : ILogger<BudgetAlertDispatcher>
    {
        public TaskCompletionSource<LogLevel> Warned { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) Warned.TrySetResult(logLevel);
        }
    }

    private static (BudgetAlertDispatcher Dispatcher, FakeService Service, CountingLogger Log) Create(Exception? failure = null)
    {
        var service = new FakeService(failure);
        var services = new ServiceCollection();
        services.AddScoped<IBudgetAlertService>(_ => service);
        var log = new CountingLogger();
        return (new BudgetAlertDispatcher(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), log), service, log);
    }

    private static async Task<T> Within<T>(Task<T> task)
    {
        // A background task has no other way to be waited for: give it a generous time before calling it lost
        return await task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Dispatch_ReturnsAtOnce_AndRunsTheCheckInTheBackgroundWithTheSameArguments()
    {
        var (dispatcher, service, _) = Create();
        var user = Guid.NewGuid();
        var category = Guid.NewGuid();

        dispatcher.Dispatch(user, category, new DateOnly(2026, 11, 3));

        Assert.Equal((user, category, new DateOnly(2026, 11, 3)), await Within(service.Called));
    }

    [Fact]
    public async Task Dispatch_DoesNotThrow_AndLogsAWarning_WhenTheCheckFails()
    {
        var (dispatcher, service, log) = Create(new InvalidOperationException("boom"));

        dispatcher.Dispatch(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 3));

        await Within(service.Called);
        Assert.Equal(LogLevel.Warning, await Within(log.Warned.Task));
    }
}
