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
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public TransactionControllerTests()
    {
        _transactionServiceMock = new Mock<ITransactionService>();
        _controller = new TransactionController(_transactionServiceMock.Object);
        ControllerTestHelper.SetUserClaim(_controller, UserId.ToString());
    }

    [Fact]
    public async Task GetPage_ReturnsOkWithThePage()
    {
        var request = new TransactionPageRequest { Page = 2, PageSize = 10 };
        _transactionServiceMock.Setup(s => s.GetPage(request, UserId))
            .ReturnsAsync(new PagedResponse<TransactionResponse>
            {
                Items = new List<TransactionResponse> { new TransactionResponse { Id = Guid.NewGuid() } },
                Total = 11,
                Page = 2,
                PageSize = 10
            });

        var result = await _controller.GetPage(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResponse<TransactionResponse>>(ok.Value);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task Export_ReturnsACsvFileWithTheTransactions()
    {
        var request = new TransactionPageRequest { Year = 2026, Month = 11 };
        _transactionServiceMock.Setup(s => s.GetForExport(request, UserId))
            .ReturnsAsync(new List<TransactionResponse>
            {
                new TransactionResponse { Id = Guid.NewGuid(), Amount = 100, CategoryName = "Comida", Date = new DateOnly(2026, 11, 3) }
            });

        var result = await _controller.Export(request);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("movimientos.csv", file.FileDownloadName);
        Assert.Contains("2026-11-03", System.Text.Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task GetById_ReturnsOkWithTheTransaction()
    {
        var id = Guid.NewGuid();
        _transactionServiceMock.Setup(s => s.GetById(id, UserId))
            .ReturnsAsync(new TransactionResponse { Id = id });

        var result = await _controller.GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(id, Assert.IsType<TransactionResponse>(ok.Value).Id);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithALocationThatPointsToTheNewTransaction()
    {
        var request = new TransactionRequest
        {
            Amount = 100, Currency = Currency.ARS, Date = new DateOnly(2026, 11, 3), CategoryId = Guid.NewGuid()
        };
        var createdId = Guid.NewGuid();
        _transactionServiceMock.Setup(s => s.Create(request, UserId))
            .ReturnsAsync(new TransactionResponse { Id = createdId, Amount = 100 });

        var result = await _controller.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TransactionController.GetById), created.ActionName);
        Assert.Equal(createdId, created.RouteValues!["id"]);
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
