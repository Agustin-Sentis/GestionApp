using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;
using GastosBackend.Presentation.Controllers;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GastosBackend.Tests.Controllers;

public class TransactionControllerTests
{
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly TransactionController _controller;
    private const string UserId = "11111111-1111-1111-1111-111111111111";

    public TransactionControllerTests()
    {
        _transactionServiceMock = new Mock<ITransactionService>();
        _controller = new TransactionController(_transactionServiceMock.Object);
        ControllerTestHelper.SetUserClaim(_controller, UserId);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithTransactions()
    {
        var filter = new TransactionFilterRequest { Year = 2026, Month = 11 };
        _transactionServiceMock.Setup(s => s.GetAll(filter, UserId))
            .ReturnsAsync(new List<TransactionResponse> { new TransactionResponse { Id = Guid.NewGuid() } });

        var result = await _controller.GetAll(filter);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsType<List<TransactionResponse>>(ok.Value));
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var request = new TransactionRequest
        {
            Amount = 100, Currency = Currency.ARS, Date = new DateOnly(2026, 11, 3), CategoryId = Guid.NewGuid()
        };
        _transactionServiceMock.Setup(s => s.Create(request, UserId))
            .ReturnsAsync(new TransactionResponse { Id = Guid.NewGuid(), Amount = 100 });

        var result = await _controller.Create(request);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var request = new TransactionRequest { Amount = 100, Currency = Currency.USD };

        var result = await _controller.Update(request, id);

        Assert.IsType<NoContentResult>(result);
        _transactionServiceMock.Verify(s => s.Update(request, id, UserId), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var id = Guid.NewGuid();

        var result = await _controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        _transactionServiceMock.Verify(s => s.Delete(id, UserId), Times.Once);
    }
}
