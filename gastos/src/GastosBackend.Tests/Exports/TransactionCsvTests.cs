using System.Text;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exports;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Tests.Exports;

public class TransactionCsvTests
{
    private const string HeaderLine = "Fecha;Tipo;Categoría;Nota;Monto;Moneda;Dólar;Cotización;Monto en ARS";

    private static TransactionResponse Row(Action<TransactionResponse>? change = null)
    {
        var row = new TransactionResponse
        {
            Id = Guid.NewGuid(),
            Amount = 1500m,
            Currency = Currency.ARS,
            ArsAmount = 1500m,
            Date = new DateOnly(2026, 11, 5),
            Note = "almuerzo",
            CategoryName = "Comida",
            Type = TransactionType.Expense
        };
        change?.Invoke(row);
        return row;
    }

    /// <summary>The file as text, without the byte order mark, split into lines.</summary>
    private static string[] Lines(params TransactionResponse[] rows)
    {
        var text = new UTF8Encoding(false).GetString(TransactionCsv.Build(rows)).TrimStart('﻿');
        return text.Split("\r\n", StringSplitOptions.None);
    }

    [Fact]
    public void StartsWithAUtf8ByteOrderMark_SoExcelReadsTheAccents()
    {
        var bytes = TransactionCsv.Build(new List<TransactionResponse>());

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
    }

    [Fact]
    public void WithNoRows_HasOnlyTheHeader()
    {
        var lines = Lines();

        Assert.Equal(new[] { HeaderLine, "" }, lines); // the header line, ended by its line break
    }

    [Theory]
    [InlineData(DollarType.Official, "oficial")]
    [InlineData(DollarType.Blue, "blue")]
    [InlineData(DollarType.Mep, "MEP")]
    public void SaysInEachUsdRowWhichDollarItsRateBelongsTo(DollarType type, string name)
    {
        var lines = Lines(Row(r =>
        {
            r.Currency = Currency.USD;
            r.DollarType = type;
            r.ExchangeRate = 1500m;
            r.ArsAmount = 15000m;
        }));

        Assert.Equal($"2026-11-05;Gasto;Comida;almuerzo;1500,00;USD;{name};1500,00;15000,00", lines[1]);
    }

    [Fact]
    public void CanMixDollarsInOneFile_BecauseEachTransactionKeepsItsOwn()
    {
        var lines = Lines(
            Row(r => { r.Currency = Currency.USD; r.DollarType = DollarType.Blue; r.ExchangeRate = 1555m; r.ArsAmount = 1m; }),
            Row(r => { r.Currency = Currency.USD; r.DollarType = DollarType.Official; r.ExchangeRate = 1570m; r.ArsAmount = 2m; }),
            Row(r => { r.Currency = Currency.USD; r.DollarType = DollarType.Mep; r.ExchangeRate = 1582.2m; r.ArsAmount = 3m; }));

        Assert.Contains(";USD;blue;1555,00;", lines[1]);
        Assert.Contains(";USD;oficial;1570,00;", lines[2]);
        Assert.Contains(";USD;MEP;1582,20;", lines[3]);
    }

    [Fact]
    public void LeavesTheDollarEmptyOnARowInPesos_EvenIfItCarriesOne()
    {
        var lines = Lines(Row(r => r.DollarType = DollarType.Blue)); // a peso transaction has no dollar to name

        Assert.Equal("2026-11-05;Gasto;Comida;almuerzo;1500,00;ARS;;;1500,00", lines[1]);
    }

    [Fact]
    public void HasTheSameHeader_WhateverTheRows()
    {
        Assert.Equal(HeaderLine, Lines()[0]);
        Assert.Equal(HeaderLine, Lines(Row(r => r.DollarType = DollarType.Mep))[0]);
    }

    [Fact]
    public void WritesAnArsExpenseOnOneLine()
    {
        var lines = Lines(Row());

        Assert.Equal("2026-11-05;Gasto;Comida;almuerzo;1500,00;ARS;;;1500,00", lines[1]);
    }

    [Fact]
    public void WritesAnIncomeLabel_AndTheDollarRateAndPesoAmountOfAUsdRow()
    {
        var lines = Lines(Row(r =>
        {
            r.Type = TransactionType.Income;
            r.Currency = Currency.USD;
            r.Amount = 10.5m;
            r.ExchangeRate = 1570.25m;
            r.ArsAmount = 16487.625m;
        }));

        Assert.Equal("2026-11-05;Ingreso;Comida;almuerzo;10,50;USD;oficial;1570,25;16487,63", lines[1]);
    }

    [Fact]
    public void LeavesTheRateAndPesoAmountEmpty_ButStillNamesTheDollar_WhenAUsdRowHasNoRateYet()
    {
        var lines = Lines(Row(r =>
        {
            r.Currency = Currency.USD;
            r.DollarType = DollarType.Blue;
            r.ExchangeRate = null;
            r.ArsAmount = null;
        }));

        Assert.EndsWith(";USD;blue;;", lines[1]); // which rate is missing is still worth knowing
    }

    [Fact]
    public void UsesADecimalCommaAndNoThousandsSeparator_WhateverTheServerCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            var lines = Lines(Row(r => r.Amount = 1234567.8m));

            Assert.Contains(";1234567,80;", lines[1]);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void QuotesANoteThatHoldsTheSeparatorQuotesOrLineBreaks()
    {
        var lines = Lines(Row(r => r.Note = "pan; leche y \"vino\""));

        Assert.Equal("2026-11-05;Gasto;Comida;\"pan; leche y \"\"vino\"\"\";1500,00;ARS;;;1500,00", lines[1]);
    }

    [Fact]
    public void KeepsALineBreakInsideANoteInsideItsQuotes()
    {
        var text = new UTF8Encoding(false).GetString(TransactionCsv.Build(new[] { Row(r => r.Note = "uno\ndos") }));

        Assert.Contains("\"uno\ndos\"", text);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void KeepsANoteThatLooksLikeAFormulaAsText(string note)
    {
        var lines = Lines(Row(r => r.Note = note));

        // Past the date, type and category cells, the note cell must begin with an apostrophe (or a quote and an apostrophe)
        var noteCell = lines[1].Split(';')[3];
        Assert.StartsWith("'", noteCell.TrimStart('"'));
    }

    [Fact]
    public void LeavesAnEmptyNoteAsAnEmptyCell()
    {
        var lines = Lines(Row(r => r.Note = null));

        Assert.Equal("2026-11-05;Gasto;Comida;;1500,00;ARS;;;1500,00", lines[1]);
    }

    [Fact]
    public void KeepsTheOrderOfTheRows()
    {
        var lines = Lines(Row(r => r.Date = new DateOnly(2026, 11, 20)), Row(r => r.Date = new DateOnly(2026, 11, 3)));

        Assert.StartsWith("2026-11-20;", lines[1]);
        Assert.StartsWith("2026-11-03;", lines[2]);
    }
}
