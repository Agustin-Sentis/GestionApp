using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Tests.Helpers;
using Moq;

namespace GastosBackend.Tests.Services;

public class BudgetAlertServiceTests
{
    // 15:00 UTC is noon in Argentina: 10 November 2026
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ThisMonth = new(2026, 11, 3);

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IReportService> _reportServiceMock = new();
    private readonly Mock<IBudgetAlertRepository> _alertRepositoryMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly TestClock _clock = new(Now);
    private readonly BudgetAlertService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly User _user;

    /// <summary>The marks "saved" by the fake repository: a mark that exists makes TryAdd say no, as the unique index does.</summary>
    private readonly List<BudgetAlert> _marks = new();

    private BudgetProgressResponse? _progress;

    public BudgetAlertServiceTests()
    {
        _service = new BudgetAlertService(
            _userRepositoryMock.Object,
            _reportServiceMock.Object,
            _alertRepositoryMock.Object,
            _emailSenderMock.Object,
            new EmailOptions { AppBaseUrl = "https://dualbalance.example" },
            _clock);

        _user = new User
        {
            Id = _userId, Email = "ana@example.com", BudgetEmailAlerts = true, EmailConfirmedAt = Now.AddDays(-1)
        };
        _userRepositoryMock.Setup(r => r.Get(_userId)).ReturnsAsync(_user);

        _reportServiceMock.Setup(s => s.GetMonthly(It.IsAny<int>(), It.IsAny<int>(), _userId))
            .ReturnsAsync(() => new MonthlyReportResponse
            {
                Budgets = _progress == null ? new() : new() { _progress }
            });

        _alertRepositoryMock.Setup(r => r.TryAdd(It.IsAny<BudgetAlert>()))
            .ReturnsAsync((BudgetAlert a) =>
            {
                if (_marks.Any(m => SameMark(m, a))) return false;
                _marks.Add(a);
                return true;
            });
        _alertRepositoryMock.Setup(r => r.Exists(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BudgetStatus>()))
            .ReturnsAsync((Guid u, Guid c, int y, int m, BudgetStatus l) =>
                _marks.Any(x => x.UserId == u && x.CategoryId == c && x.Year == y && x.Month == m && x.Level == l));
        _alertRepositoryMock.Setup(r => r.Delete(It.IsAny<Guid>()))
            .Returns((Guid id) =>
            {
                _marks.RemoveAll(m => m.Id == id);
                return Task.CompletedTask;
            });

        Report(BudgetStatus.Warning, 85000m);
    }

    private static bool SameMark(BudgetAlert a, BudgetAlert b) =>
        a.UserId == b.UserId && a.CategoryId == b.CategoryId && a.Year == b.Year && a.Month == b.Month && a.Level == b.Level;

    /// <summary>What the month's report says about the category: a budget of 100.000 with this much spent.</summary>
    private void Report(BudgetStatus status, decimal spent)
    {
        _progress = new BudgetProgressResponse
        {
            CategoryId = _categoryId,
            Name = "Comida",
            MonthlyBudget = 100000m,
            Spent = spent,
            UsedPercent = Math.Round(spent / 100000m * 100, 1),
            Status = status
        };
    }

    private IEnumerable<EmailMessage> Sent() =>
        _emailSenderMock.Invocations.Where(i => i.Method.Name == nameof(IEmailSender.Send)).Select(i => (EmailMessage)i.Arguments[0]);

    [Fact]
    public async Task SendsOneMail_WhenTheMonthCrossesTheWarning()
    {
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        var mail = Assert.Single(Sent());
        Assert.Equal("ana@example.com", mail.To);
        Assert.Equal("Cerca del límite en Comida", mail.Subject);
        Assert.Contains("(85%)", mail.TextBody);
        Assert.Contains("https://dualbalance.example", mail.TextBody);
        var mark = Assert.Single(_marks);
        Assert.Equal((2026, 11, BudgetStatus.Warning), (mark.Year, mark.Month, mark.Level));
    }

    [Fact]
    public async Task SendsOnlyTheExceededOne_WhenItJumpsFromOkToExceeded()
    {
        Report(BudgetStatus.Exceeded, 130000m);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        var mail = Assert.Single(Sent());
        Assert.Equal("Superaste el presupuesto de Comida", mail.Subject);
        Assert.Equal(BudgetStatus.Exceeded, Assert.Single(_marks).Level);
    }

    [Fact]
    public async Task DoesNotRepeat_AnAlertAlreadySent()
    {
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);
        await _service.CheckAndNotify(_userId, _categoryId, new DateOnly(2026, 11, 9)); // another day of the same month

        Assert.Single(Sent());
    }

    [Fact]
    public async Task SendsExceeded_AfterWarningWasSent()
    {
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);
        Report(BudgetStatus.Exceeded, 105000m);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Equal(new[] { "Cerca del límite en Comida", "Superaste el presupuesto de Comida" }, Sent().Select(m => m.Subject));
    }

    [Fact]
    public async Task SkipsWarning_WhenExceededWasAlreadySent()
    {
        Report(BudgetStatus.Exceeded, 105000m);
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);
        // Some expenses were deleted and the month is back under the budget, but over 80%
        Report(BudgetStatus.Warning, 90000m);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Single(Sent());
    }

    [Fact]
    public async Task SendsAgain_InTheNextMonth()
    {
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);
        _clock.Advance(TimeSpan.FromDays(25)); // 5 December in Argentina

        await _service.CheckAndNotify(_userId, _categoryId, new DateOnly(2026, 12, 4));

        Assert.Equal(2, Sent().Count());
    }

    [Fact]
    public async Task DoesNothing_WhenTheCategoryIsUnderTheWarning()
    {
        Report(BudgetStatus.Ok, 50000m);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
        Assert.Empty(_marks);
    }

    [Fact]
    public async Task DoesNothing_WhenTheCategoryHasNoBudget()
    {
        _progress = null;

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task DoesNothing_ForAnotherCategoryThanTheOneThatChanged()
    {
        await _service.CheckAndNotify(_userId, Guid.NewGuid(), ThisMonth);

        Assert.Empty(Sent());
    }

    [Fact]
    public async Task DoesNothing_WhenTheSwitchIsOff()
    {
        _user.BudgetEmailAlerts = false;

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
        _reportServiceMock.Verify(s => s.GetMonthly(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DoesNothing_WhenTheEmailIsNotConfirmed()
    {
        _user.EmailConfirmedAt = null;

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
        _reportServiceMock.Verify(s => s.GetMonthly(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DoesNothing_WhenTheUserDoesNotExistOrWasDeleted()
    {
        await _service.CheckAndNotify(Guid.NewGuid(), _categoryId, ThisMonth);
        _user.IsDeleted = true;
        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
    }

    [Theory]
    [InlineData("2026-10-31")] // last month
    [InlineData("2025-11-10")] // the same month of last year
    [InlineData("2026-12-01")] // next month
    public async Task DoesNothing_ForAMonthThatIsNotTheCurrentOne(string date)
    {
        await _service.CheckAndNotify(_userId, _categoryId, DateOnly.Parse(date));

        Assert.Empty(Sent());
        _reportServiceMock.Verify(s => s.GetMonthly(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TheMonthChangesAtMidnightInArgentina_NotAtMidnightUtc()
    {
        // 01:00 UTC on 1 December is still 22:00 of 30 November in Argentina
        _clock.Advance(new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero) - Now);

        await _service.CheckAndNotify(_userId, _categoryId, new DateOnly(2026, 11, 30));
        Assert.Single(Sent());

        await _service.CheckAndNotify(_userId, _categoryId, new DateOnly(2026, 12, 1));
        Assert.Single(Sent()); // December has not started there yet
    }

    [Fact]
    public async Task DeletesTheMark_AndThrows_WhenTheSendFails_ThenTheNextExpenseTriesAgain()
    {
        _emailSenderMock.SetupSequence(s => s.Send(It.IsAny<EmailMessage>()))
            .ThrowsAsync(new ExternalServiceException("down"))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<ExternalServiceException>(() => _service.CheckAndNotify(_userId, _categoryId, ThisMonth));
        Assert.Empty(_marks);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Equal(2, Sent().Count());
        Assert.Single(_marks);
    }

    [Fact]
    public async Task DoesNotSend_WhenAnotherRequestMarkedTheSameAlertFirst()
    {
        _alertRepositoryMock.Setup(r => r.TryAdd(It.IsAny<BudgetAlert>())).ReturnsAsync(false);

        await _service.CheckAndNotify(_userId, _categoryId, ThisMonth);

        Assert.Empty(Sent());
    }
}
