using GastosBackend.Application.Services;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Tests.Services;

public class EmailTemplatesTests
{
    private const string AppUrl = "https://dualbalance.example/";

    [Fact]
    public void BudgetAlert_EncodesTheCategoryNameInTheHtml()
    {
        const string name = "<script>alert(1)</script> & \"x\"";

        var mail = EmailTemplates.BudgetAlert("ana@example.com", name, 90m, 100m, 90m, BudgetStatus.Warning, AppUrl);

        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody);
        Assert.Contains("&amp;", mail.HtmlBody);
        // The plain text part is not HTML: the name goes in as written
        Assert.Contains(name, mail.TextBody);
    }

    [Fact]
    public void BudgetAlert_ForWarning_SaysCloseToTheLimit_WithTheAmountsInArgentineFormat()
    {
        var mail = EmailTemplates.BudgetAlert("ana@example.com", "Comida", 83300.5m, 100000m, 83.3m, BudgetStatus.Warning, AppUrl);

        Assert.Equal("Cerca del límite en Comida", mail.Subject);
        Assert.Equal("ana@example.com", mail.To);
        Assert.Contains("Llevás gastado $ 83.300,50 de $ 100.000 en Comida este mes (83,3%).", mail.TextBody);
        Assert.Contains("Estás cerca del límite.", mail.TextBody);
        Assert.DoesNotContain("Te pasaste", mail.TextBody);
    }

    [Fact]
    public void BudgetAlert_ForExceeded_SaysByHowMuchItWasExceeded()
    {
        var mail = EmailTemplates.BudgetAlert("ana@example.com", "Comida", 120000m, 100000m, 120m, BudgetStatus.Exceeded, AppUrl);

        Assert.Equal("Superaste el presupuesto de Comida", mail.Subject);
        Assert.Contains("Te pasaste por $ 20.000.", mail.TextBody);
        Assert.Contains("(120%)", mail.TextBody);
        Assert.DoesNotContain("Estás cerca", mail.TextBody);
    }

    [Fact]
    public void BudgetAlert_LinksToTheApp_AndSaysHowToTurnTheAlertsOff()
    {
        var mail = EmailTemplates.BudgetAlert("ana@example.com", "Comida", 90m, 100m, 90m, BudgetStatus.Warning, AppUrl);

        Assert.Contains(AppUrl, mail.TextBody);
        Assert.Contains($"href=\"{AppUrl}\"", mail.HtmlBody);
        Assert.Contains("desactivalos en la tarjeta Presupuestos", mail.TextBody);
        Assert.Contains("desactivalos en la tarjeta Presupuestos", mail.HtmlBody);
    }

    [Fact]
    public void Confirmation_CarriesTheLink_InBothParts_AndSaysItIsValidFor24Hours()
    {
        const string url = "https://dualbalance.example/confirmar-correo?token=abc_-123";

        var mail = EmailTemplates.Confirmation("ana@example.com", url);

        Assert.Equal("Confirmá tu correo en DualBalance", mail.Subject);
        Assert.Contains(url, mail.TextBody);
        Assert.Contains($"href=\"{url}\"", mail.HtmlBody);
        Assert.Contains("24 horas", mail.TextBody);
        Assert.Contains("ignorá este mensaje", mail.TextBody);
    }

    [Fact]
    public void PasswordReset_CarriesTheLink_InBothParts_AndSaysItLastsOneHourAndWorksOnce()
    {
        const string url = "https://dualbalance.example/restablecer-contrasena?token=abc_-123";

        var mail = EmailTemplates.PasswordReset("ana@example.com", url);

        Assert.Equal("Restablecé tu contraseña en DualBalance", mail.Subject);
        Assert.Equal("ana@example.com", mail.To);
        Assert.Contains(url, mail.TextBody);
        Assert.Contains($"href=\"{url}\"", mail.HtmlBody);
        Assert.Contains("1 hora", mail.TextBody);
        Assert.Contains("una sola vez", mail.TextBody);
        Assert.Contains("ignorá este mensaje", mail.TextBody);
        Assert.Contains("tu contraseña no cambia", mail.TextBody);
    }

    [Fact]
    public void PasswordReset_EncodesTheUrlInTheHtml()
    {
        var mail = EmailTemplates.PasswordReset("ana@example.com", "https://x.test/?a=1&b=\"2\"");

        Assert.Contains("href=\"https://x.test/?a=1&amp;b=&quot;2&quot;\"", mail.HtmlBody);
    }

    [Theory]
    [InlineData("https://dualbalance.example")]
    [InlineData("https://dualbalance.example/")]
    public void PasswordChanged_SaysWhatHappened_AndHowToRecoverTheAccount_WhateverTheTrailingSlash(string appUrl)
    {
        var mail = EmailTemplates.PasswordChanged("ana@example.com", appUrl);

        Assert.Equal("Tu contraseña de DualBalance fue cambiada", mail.Subject);
        Assert.Contains("se cerraron todas las sesiones", mail.TextBody);
        Assert.Contains("Si no fuiste vos", mail.TextBody);
        Assert.Contains("https://dualbalance.example/login?modo=recuperar", mail.TextBody);
        Assert.Contains("href=\"https://dualbalance.example/login?modo=recuperar\"", mail.HtmlBody);
        Assert.DoesNotContain("//login", mail.TextBody);
    }

    [Fact]
    public void Confirmation_EncodesTheUrlInTheHtml()
    {
        var mail = EmailTemplates.Confirmation("ana@example.com", "https://x.test/?a=1&b=\"2\"");

        Assert.Contains("href=\"https://x.test/?a=1&amp;b=&quot;2&quot;\"", mail.HtmlBody);
    }
}
