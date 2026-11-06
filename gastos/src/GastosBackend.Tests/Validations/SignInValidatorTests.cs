using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Validations;

namespace GastosBackend.Tests.Validations;

public class SignInValidatorTests
{
    private readonly SignInValidator _validator = new SignInValidator();

    [Fact]
    public void Validate_Succeeds_WithCredentials()
    {
        var result = _validator.Validate(new SignInRequest { Email = "demo@demo.com", Password = "Demo1234!" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DoesNotApplyThePasswordStrengthRules()
    {
        // Users created before the rules existed must still be able to sign in
        var result = _validator.Validate(new SignInRequest { Email = "demo@demo.com", Password = "weak" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Demo1234!")]
    [InlineData("   ", "Demo1234!")]
    [InlineData("demo@demo.com", "")]
    [InlineData("demo@demo.com", "   ")]
    public void Validate_Fails_WhenACredentialIsMissing(string email, string password)
    {
        var result = _validator.Validate(new SignInRequest { Email = email, Password = password });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenThePasswordIsTooLong()
    {
        var result = _validator.Validate(new SignInRequest { Email = "demo@demo.com", Password = new string('a', 101) });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenTheEmailIsTooLong()
    {
        var result = _validator.Validate(new SignInRequest { Email = new string('a', 250) + "@x.com", Password = "Demo1234!" });

        Assert.False(result.IsValid);
    }
}
