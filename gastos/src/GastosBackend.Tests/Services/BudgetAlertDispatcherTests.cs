using GastosBackend.Application.Interfaces;
using GastosBackend.Presentation.Services;
using Moq;

namespace GastosBackend.Tests.Services;

public class BudgetAlertDispatcherTests
{
    [Fact]
    public async Task Dispatch_AsksTheRunnerToCheckTheAlert_WithTheSameArguments()
    {
        Func<IBudgetAlertService, Task>? work = null;
        var runner = new Mock<IBackgroundRunner>();
        runner.Setup(r => r.Run(It.IsAny<Func<IBudgetAlertService, Task>>()))
            .Callback<Func<IBudgetAlertService, Task>>(w => work = w);
        var service = new Mock<IBudgetAlertService>();
        var user = Guid.NewGuid();
        var category = Guid.NewGuid();

        new BudgetAlertDispatcher(runner.Object).Dispatch(user, category, new DateOnly(2026, 11, 3));

        Assert.NotNull(work);
        service.Verify(s => s.CheckAndNotify(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>()), Times.Never); // nothing ran yet
        await work!(service.Object);
        service.Verify(s => s.CheckAndNotify(user, category, new DateOnly(2026, 11, 3)), Times.Once);
    }
}
