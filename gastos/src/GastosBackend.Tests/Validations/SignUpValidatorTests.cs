using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Validations;

namespace GastosBackend.Tests.Validations;

public class SignUpValidatorTests
{
    private readonly SignUpValidator _validator = new SignUpValidator();

    [Fact]
    public void Validate_Succeeds_WithValidData()
    {
        var result = _validator.Validate(new SignUpRequest { Email = "demo@demo.com", Password = "Demo1234!" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("demo1234!")]   // sin mayúscula
    [InlineData("DEMO1234!")]   // sin minúscula
    [InlineData("Demodemo!")]   // sin número
    [InlineData("Demo12345")]   // sin carácter especial
    [InlineData("De1!")]        // muy corta
    public void Validate_Fails_WithWeakPassword(string password)
    {
        var result = _validator.Validate(new SignUpRequest { Email = "demo@demo.com", Password = password });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    public void Validate_Fails_WithInvalidEmail(string email)
    {
        var result = _validator.Validate(new SignUpRequest { Email = email, Password = "Demo1234!" });

        Assert.False(result.IsValid);
    }
}
