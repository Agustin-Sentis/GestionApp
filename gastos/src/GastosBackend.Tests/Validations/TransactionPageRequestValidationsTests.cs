using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Tests.Validations;

public class TransactionPageRequestValidationsTests
{
    private readonly TransactionPageRequestValidations _validator = new TransactionPageRequestValidations();

    [Fact]
    public void Validate_Succeeds_WithNoFilters()
    {
        Assert.True(_validator.Validate(new TransactionPageRequest()).IsValid);
    }

    [Fact]
    public void Validate_Succeeds_WithAYearAndMonth()
    {
        Assert.True(_validator.Validate(new TransactionPageRequest { Year = 2026, Month = 11 }).IsValid);
    }

    [Fact]
    public void Validate_Succeeds_WithADateRangeCategoryAndType()
    {
        var request = new TransactionPageRequest
        {
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 30),
            CategoryId = Guid.NewGuid(),
            Type = TransactionType.Income,
            Page = 2,
            PageSize = TransactionPageRequest.MaxPageSize
        };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_Succeeds_WhenFromAndToAreTheSameDay()
    {
        var day = new DateOnly(2026, 11, 3);

        Assert.True(_validator.Validate(new TransactionPageRequest { From = day, To = day }).IsValid);
    }

    [Fact]
    public void Validate_Succeeds_WithOnlyFromOrOnlyTo()
    {
        Assert.True(_validator.Validate(new TransactionPageRequest { From = new DateOnly(2026, 11, 1) }).IsValid);
        Assert.True(_validator.Validate(new TransactionPageRequest { To = new DateOnly(2026, 11, 30) }).IsValid);
    }

    [Theory]
    [InlineData(2000, 1)]
    [InlineData(2100, 12)]
    public void Validate_Succeeds_AtTheLimitsOfYearAndMonth(int year, int month)
    {
        Assert.True(_validator.Validate(new TransactionPageRequest { Year = year, Month = month }).IsValid);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(int.MaxValue, 1)]
    public void Validate_Succeeds_AtTheLimitsOfPageAndPageSize(int page, int pageSize)
    {
        Assert.True(_validator.Validate(new TransactionPageRequest { Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenAYearAloneOrAMonthAloneIsCombinedWithADateRange()
    {
        Assert.False(_validator.Validate(
            new TransactionPageRequest { Year = 2026, To = new DateOnly(2026, 11, 30) }).IsValid);
        Assert.False(_validator.Validate(
            new TransactionPageRequest { Month = 11, From = new DateOnly(2026, 11, 1) }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenThePageIsBelowOne(int page)
    {
        Assert.False(_validator.Validate(new TransactionPageRequest { Page = page }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(TransactionPageRequest.MaxPageSize + 1)]
    public void Validate_Fails_WhenThePageSizeIsOutOfRange(int pageSize)
    {
        Assert.False(_validator.Validate(new TransactionPageRequest { PageSize = pageSize }).IsValid);
    }

    [Theory]
    [InlineData(2026, null)]
    [InlineData(null, 11)]
    public void Validate_Fails_WhenOnlyOneOfYearAndMonthIsGiven(int? year, int? month)
    {
        Assert.False(_validator.Validate(new TransactionPageRequest { Year = year, Month = month }).IsValid);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    [InlineData(2101, 5)]
    public void Validate_Fails_WhenYearOrMonthIsOutOfRange(int year, int month)
    {
        Assert.False(_validator.Validate(new TransactionPageRequest { Year = year, Month = month }).IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenAMonthIsCombinedWithADateRange()
    {
        var request = new TransactionPageRequest { Year = 2026, Month = 11, From = new DateOnly(2026, 11, 1) };

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenFromIsAfterTo()
    {
        var request = new TransactionPageRequest { From = new DateOnly(2026, 11, 30), To = new DateOnly(2026, 11, 1) };

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_Succeeds_WithASearchOfExactlyTheMaximumLength()
    {
        var request = new TransactionPageRequest { Search = new string('a', TransactionPageRequest.MaxSearchLength) };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenTheSearchIsLongerThanTheMaximum()
    {
        var request = new TransactionPageRequest { Search = new string('a', TransactionPageRequest.MaxSearchLength + 1) };

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenTheTypeIsNotAnEnumValue()
    {
        Assert.False(_validator.Validate(new TransactionPageRequest { Type = (TransactionType)99 }).IsValid);
    }
}
