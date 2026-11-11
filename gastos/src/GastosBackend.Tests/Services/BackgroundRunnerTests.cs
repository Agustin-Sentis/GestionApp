using GastosBackend.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GastosBackend.Tests.Services;

public class BackgroundRunnerTests
{
    public interface IFakeWork
    {
        Task Do(string what);
    }

    /// <summary>Hands the work its argument and tells the test when it ran, and in which scope it was resolved.</summary>
    private sealed class FakeWork : IFakeWork
    {
        private readonly TaskCompletionSource<string> _ran = new();
        private readonly Exception? _failure;

        public FakeWork(Exception? failure = null)
        {
            _failure = failure;
        }

        public Task<string> Ran => _ran.Task;

        public Task Do(string what)
        {
            _ran.SetResult(what);
            return _failure == null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }

    private sealed class CountingLogger : ILogger<BackgroundRunner>
    {
        public TaskCompletionSource<LogLevel> Warned { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) Warned.TrySetResult(logLevel);
        }
    }

    private static (BackgroundRunner Runner, FakeWork Work, CountingLogger Log) Create(Exception? failure = null)
    {
        var work = new FakeWork(failure);
        var services = new ServiceCollection();
        services.AddScoped<IFakeWork>(_ => work);
        var log = new CountingLogger();
        return (new BackgroundRunner(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), log), work, log);
    }

    private static async Task<T> Within<T>(Task<T> task)
    {
        // A background task has no other way to be waited for: give it a generous time before calling it lost
        return await task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Run_ReturnsAtOnce_AndRunsTheWorkInTheBackgroundWithAServiceOfItsOwnScope()
    {
        var (runner, work, _) = Create();

        runner.Run<IFakeWork>(service => service.Do("hola"));

        Assert.Equal("hola", await Within(work.Ran));
    }

    [Fact]
    public async Task Run_DoesNotThrow_AndLogsAWarning_WhenTheWorkFails()
    {
        var (runner, work, log) = Create(new InvalidOperationException("boom"));

        runner.Run<IFakeWork>(service => service.Do("x"));

        await Within(work.Ran);
        Assert.Equal(LogLevel.Warning, await Within(log.Warned.Task));
    }

    [Fact]
    public async Task Run_LogsAWarning_WhenTheServiceCannotBeResolved()
    {
        var log = new CountingLogger();
        var runner = new BackgroundRunner(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), log);

        runner.Run<IFakeWork>(service => service.Do("x"));

        Assert.Equal(LogLevel.Warning, await Within(log.Warned.Task));
    }
}
