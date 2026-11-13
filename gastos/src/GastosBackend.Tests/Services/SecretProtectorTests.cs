using GastosBackend.Infrastructure.ExternalService;
using Microsoft.AspNetCore.DataProtection;

namespace GastosBackend.Tests.Services;

public class SecretProtectorTests
{
    private readonly SecretProtector _protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_RoundTrips()
    {
        var protectedText = _protector.Protect("JBSWY3DPEHPK3PXP");

        Assert.Equal("JBSWY3DPEHPK3PXP", _protector.Unprotect(protectedText));
    }

    [Fact]
    public void Protect_DoesNotContainThePlainText()
    {
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", _protector.Protect("JBSWY3DPEHPK3PXP"));
    }

    [Fact]
    public void Unprotect_RejectsTextThatWasNotProtectedByUs()
    {
        Assert.ThrowsAny<Exception>(() => _protector.Unprotect("JBSWY3DPEHPK3PXP"));
    }
}
