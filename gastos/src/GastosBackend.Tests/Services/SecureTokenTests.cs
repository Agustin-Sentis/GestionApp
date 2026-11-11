using GastosBackend.Application.Services;

namespace GastosBackend.Tests.Services;

public class SecureTokenTests
{
    [Fact]
    public void Generate_ReturnsDifferentTokens_WhoseHashMatchesHash()
    {
        var first = SecureToken.Generate();
        var second = SecureToken.Generate();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.Equal(SecureToken.Hash(first.Token), first.Hash);
    }

    [Fact]
    public void Generate_ReturnsATokenThatIsSafeInAUrl_AndLongEnoughToNotBeGuessed()
    {
        var token = SecureToken.Generate().Token;

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.Equal(43, token.Length); // 32 random bytes in base64url, no padding
    }

    [Fact]
    public void Hash_IsStable_Lowercase64Hex_AndDoesNotContainTheToken()
    {
        var (token, hash) = SecureToken.Generate();

        Assert.Equal(hash, SecureToken.Hash(token));
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.DoesNotContain(token, hash);
        // A known value, so a change of algorithm is noticed: SHA-256("abc")
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", SecureToken.Hash("abc"));
    }
}
