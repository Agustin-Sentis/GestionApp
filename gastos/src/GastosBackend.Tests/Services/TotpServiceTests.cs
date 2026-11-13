using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Tests.Helpers;
using OtpNet;

namespace GastosBackend.Tests.Services;

public class TotpServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 15, 0, 10, TimeSpan.Zero);

    private readonly TestClock _clock = new(Now);
    private readonly TotpService _totp;

    public TotpServiceTests()
    {
        _totp = new TotpService(_clock);
    }

    private static string CodeAt(string secret, DateTimeOffset moment) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(moment.UtcDateTime);

    private static long StepOf(DateTimeOffset moment) => moment.ToUnixTimeSeconds() / 30;

    [Fact]
    public void GenerateSecret_ReturnsADifferentBase32SecretEachTime()
    {
        var first = _totp.GenerateSecret();
        var second = _totp.GenerateSecret();

        Assert.NotEqual(first, second);
        Assert.Equal(32, first.Length); // 20 bytes in Base32
        Assert.Matches("^[A-Z2-7]+$", first);
    }

    [Fact]
    public void BuildUri_CarriesTheSecretAndTheIssuer()
    {
        var uri = _totp.BuildUri("ana+test@example.com", "JBSWY3DPEHPK3PXP");

        Assert.StartsWith("otpauth://totp/DualBalance:ana%2Btest%40example.com?", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=DualBalance", uri);
    }

    [Fact]
    public void Verify_AcceptsTheCurrentStep_AndReportsIt()
    {
        var secret = _totp.GenerateSecret();

        Assert.True(_totp.Verify(secret, CodeAt(secret, Now), null, out var step));
        Assert.Equal(StepOf(Now), step);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public void Verify_AcceptsOneStepEachSide(int seconds)
    {
        var secret = _totp.GenerateSecret();

        Assert.True(_totp.Verify(secret, CodeAt(secret, Now.AddSeconds(seconds)), null, out _));
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(60)]
    public void Verify_RejectsTwoStepsOff(int seconds)
    {
        var secret = _totp.GenerateSecret();

        Assert.False(_totp.Verify(secret, CodeAt(secret, Now.AddSeconds(seconds)), null, out _));
    }

    [Fact]
    public void Verify_RejectsTheSameStepTwice()
    {
        var secret = _totp.GenerateSecret();
        var code = CodeAt(secret, Now);
        Assert.True(_totp.Verify(secret, code, null, out var step));

        Assert.False(_totp.Verify(secret, code, step, out _));
    }

    [Fact]
    public void Verify_RejectsAnOlderStepThanTheLastUsed()
    {
        var secret = _totp.GenerateSecret();
        var previous = CodeAt(secret, Now.AddSeconds(-30));

        Assert.False(_totp.Verify(secret, previous, StepOf(Now), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("      ")]
    public void Verify_RejectsAnythingThatIsNotSixDigits(string code)
    {
        Assert.False(_totp.Verify(_totp.GenerateSecret(), code, null, out _));
    }
}
