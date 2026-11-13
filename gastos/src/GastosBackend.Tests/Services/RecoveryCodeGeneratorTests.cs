using GastosBackend.Infrastructure.ExternalService;

namespace GastosBackend.Tests.Services;

public class RecoveryCodeGeneratorTests
{
    private readonly RecoveryCodeGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsTenUniqueCodesWithTheAgreedFormat()
    {
        var codes = _generator.Generate();

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$", code));
    }

    [Fact]
    public void Generate_NeverUsesCharactersThatLookAlike()
    {
        var all = string.Concat(Enumerable.Range(0, 50).SelectMany(_ => _generator.Generate()));

        Assert.DoesNotContain('0', all);
        Assert.DoesNotContain('O', all);
        Assert.DoesNotContain('1', all);
        Assert.DoesNotContain('I', all);
    }

    [Theory]
    [InlineData("abcde-fghjk", "ABCDEFGHJK")]
    [InlineData("  ABCDE FGHJK ", "ABCDEFGHJK")]
    [InlineData("abcdefghjk", "ABCDEFGHJK")]
    public void Normalize_IgnoresCaseDashesAndSpaces(string typed, string expected)
    {
        Assert.Equal(expected, _generator.Normalize(typed));
    }
}
