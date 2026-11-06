using GastosBackend.Presentation.Middlewares;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GastosBackend.Tests.Middlewares;

public class ModelStateErrorMessageTests
{
    private static ModelStateDictionary With(params string[] keys)
    {
        var modelState = new ModelStateDictionary();
        foreach (var key in keys)
        {
            modelState.AddModelError(key, "framework message that must never reach the client");
        }
        return modelState;
    }

    private static int CountOccurrences(string text, string value)
        => text.Split(value).Length - 1;

    [Fact]
    public void ANamedField_ProducesAFieldMessage()
    {
        Assert.Equal("El valor del campo 'Type' no es válido.", ModelStateErrorMessage.Build(With("Type")));
    }

    [Fact]
    public void AJsonPath_UsesTheFieldNameWithoutThePrefix()
    {
        Assert.Equal("El valor del campo 'currency' no es válido.", ModelStateErrorMessage.Build(With("$.currency")));
    }

    [Theory]
    [InlineData("$")]
    [InlineData("")]
    [InlineData("request")]
    public void ABodyLevelError_ProducesTheBodyMessage(string key)
    {
        Assert.Equal(ModelStateErrorMessage.BodyMessage, ModelStateErrorMessage.Build(With(key)));
    }

    [Fact]
    public void TheRequestEntryThatAccompaniesAFieldError_IsNotReportedAsABodyError()
    {
        // This is what ASP.NET really produces for { "name": "x", "type": "Foo" }
        var message = ModelStateErrorMessage.Build(With("$.type", "request"));

        Assert.Equal("El valor del campo 'type' no es válido.", message);
    }

    [Fact]
    public void SeveralFields_AreAllNamedOnce()
    {
        // "Type" and "$.Type" are the same field. ModelStateDictionary does not keep insertion order, so the order is not asserted.
        var message = ModelStateErrorMessage.Build(With("Type", "Page", "$.Type"));

        Assert.Equal(1, CountOccurrences(message, "'Type'"));
        Assert.Equal(1, CountOccurrences(message, "'Page'"));
        Assert.DoesNotContain(ModelStateErrorMessage.BodyMessage, message);
    }

    [Fact]
    public void FieldsAndABodyError_AreReportedTogether()
    {
        var message = ModelStateErrorMessage.Build(With("Page", "$"));

        Assert.Contains("'Page'", message);
        Assert.Contains(ModelStateErrorMessage.BodyMessage, message);
    }

    [Fact]
    public void WithoutAnyError_FallsBackToTheBodyMessage()
    {
        Assert.Equal(ModelStateErrorMessage.BodyMessage, ModelStateErrorMessage.Build(new ModelStateDictionary()));
    }

    [Fact]
    public void TheFrameworkMessage_IsNeverExposed()
    {
        Assert.DoesNotContain("framework message", ModelStateErrorMessage.Build(With("Type")));
    }
}
