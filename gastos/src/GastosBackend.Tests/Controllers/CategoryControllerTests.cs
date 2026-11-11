using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;
using GastosBackend.Presentation.Controllers;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GastosBackend.Tests.Controllers;

public class CategoryControllerTests
{
    private readonly Mock<ICategoryService> _categoryServiceMock;
    private readonly CategoryController _controller;
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public CategoryControllerTests()
    {
        _categoryServiceMock = new Mock<ICategoryService>();
        _controller = new CategoryController(_categoryServiceMock.Object);
        ControllerTestHelper.SetUserClaim(_controller, UserId.ToString());
    }

    [Fact]
    public async Task GetAll_PassesTheAuthenticatedUserToTheService()
    {
        _categoryServiceMock.Setup(s => s.GetAll(UserId)).ReturnsAsync(new List<CategoryResponse>
        {
            new CategoryResponse { Id = Guid.NewGuid(), Name = "Comida", Type = TransactionType.Expense }
        });

        var result = await _controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var categories = Assert.IsType<List<CategoryResponse>>(ok.Value);
        Assert.Single(categories);
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var request = new CategoryRequest { Name = "Comida", Type = TransactionType.Expense };
        _categoryServiceMock.Setup(s => s.Create(request, UserId))
            .ReturnsAsync(new CategoryResponse { Id = Guid.NewGuid(), Name = "Comida", Type = TransactionType.Expense });

        var result = await _controller.Create(request);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task SetBudget_PassesTheAuthenticatedUserToTheService()
    {
        var id = Guid.NewGuid();
        var request = new CategoryBudgetRequest { MonthlyBudget = 300000m };
        _categoryServiceMock.Setup(s => s.SetBudget(id, request, UserId))
            .ReturnsAsync(new CategoryResponse { Id = id, Name = "Comida", Type = TransactionType.Expense, MonthlyBudget = 300000m });

        var result = await _controller.SetBudget(id, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(300000m, Assert.IsType<CategoryResponse>(ok.Value).MonthlyBudget);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var id = Guid.NewGuid();

        var result = await _controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        _categoryServiceMock.Verify(s => s.Delete(id, UserId), Times.Once);
    }
}
