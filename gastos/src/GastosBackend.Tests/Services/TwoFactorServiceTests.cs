using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OtpNet;

namespace GastosBackend.Tests.Services;

public class TwoFactorServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 15, 0, 10, TimeSpan.Zero);
    private const string Password = "Demo1234!";

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRecoveryCodeRepository> _recoveryRepositoryMock = new();
    private readonly Mock<IPasswordHash> _passwordHashMock = new();
    private readonly TestClock _clock = new(Start);
    private readonly TotpService _totp;
    private readonly SecretProtector _protector = new(new EphemeralDataProtectionProvider());
    private readonly RecoveryCodeGenerator _generator = new();
    private readonly TwoFactorService _service;
    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "a@x.com", Password = "h:" + Password };

    /// <summary>The recovery codes "stored" by the fake repository (hash, used or not).</summary>
    private readonly List<RecoveryCode> _codes = new();

    public TwoFactorServiceTests()
    {
        _totp = new TotpService(_clock);
        _service = new TwoFactorService(
            _userRepositoryMock.Object,
            _recoveryRepositoryMock.Object,
            _totp,
            _protector,
            _generator,
            _passwordHashMock.Object,
            _clock);

        _userRepositoryMock.Setup(r => r.Get(_user.Id)).ReturnsAsync(_user);

        // "Hashing" that is readable: a hash is "h:" plus the text
        _passwordHashMock.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(text => "h:" + text);
        _passwordHashMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>((text, hash) => hash == "h:" + text);

        _recoveryRepositoryMock.Setup(r => r.GetUnusedByUser(_user.Id)).ReturnsAsync(() => _codes.Where(c => c.UsedAt == null).ToList());
        _recoveryRepositoryMock.Setup(r => r.ReplaceAll(_user.Id, It.IsAny<IEnumerable<string>>()))
            .Callback<Guid, IEnumerable<string>>((_, hashes) =>
            {
                _codes.Clear();
                _codes.AddRange(hashes.Select(h => new RecoveryCode { UserId = _user.Id, CodeHash = h }));
            })
            .Returns(Task.CompletedTask);
        _recoveryRepositoryMock.Setup(r => r.DeleteAllByUser(_user.Id)).Callback(() => _codes.Clear()).Returns(Task.CompletedTask);
    }

    private static string CodeAt(string secret, DateTimeOffset moment) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(moment.UtcDateTime);

    /// <summary>The secret saved by Setup, in plain text.</summary>
    private string SavedSecret() => _protector.Unprotect(_user.TwoFactorSecret!);

    /// <summary>The user already went through Setup and Enable. Returns the secret and leaves the clock one step later.</summary>
    private async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> EnabledUser()
    {
        await _service.Setup(_user.Id, Password);
        var secret = SavedSecret();
        var result = await _service.Enable(_user.Id, CodeAt(secret, _clock.GetUtcNow()));
        _clock.Advance(TimeSpan.FromSeconds(30));
        return (secret, result.Codes);
    }

    private async Task Fail(int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, "000000"));
        }
    }

    // --- Setup ---

    [Fact]
    public async Task Setup_StoresAnEncryptedSecret_WithoutTurningTheTwoFactorOn()
    {
        var response = await _service.Setup(_user.Id, Password);

        Assert.False(_user.TwoFactorEnabled);
        Assert.NotNull(_user.TwoFactorSecret);
        Assert.DoesNotContain(response.Secret, _user.TwoFactorSecret);
        Assert.Equal(response.Secret, SavedSecret());
        Assert.Contains($"secret={response.Secret}", response.Uri);
        Assert.Contains(Uri.EscapeDataString("a@x.com"), response.Uri);
        _userRepositoryMock.Verify(r => r.Update(_user), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Setup_ReplacesTheSecretOfAnActivationLeftHalfway()
    {
        var first = await _service.Setup(_user.Id, Password);
        var second = await _service.Setup(_user.Id, Password);

        Assert.NotEqual(first.Secret, second.Secret);
        Assert.Equal(second.Secret, SavedSecret());
    }

    [Fact]
    public async Task Setup_WithAWrongPassword_Fails_AndCountsAsAFailedAttempt()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.Setup(_user.Id, "wrong"));

        Assert.Null(_user.TwoFactorSecret);
        Assert.Equal(1, _user.TwoFactorFailedAttempts);
    }

    [Fact]
    public async Task Setup_WhenAlreadyEnabled_ThrowsConflict()
    {
        await EnabledUser();

        await Assert.ThrowsAsync<ConflictException>(() => _service.Setup(_user.Id, Password));
    }

    [Fact]
    public async Task Setup_WithAnEmptyPassword_ThrowsValidation_WithoutCountingAsAFailure()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.Setup(_user.Id, ""));

        Assert.Equal(0, _user.TwoFactorFailedAttempts);
    }

    [Fact]
    public async Task AnUnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.Setup(Guid.NewGuid(), Password));
    }

    // --- Enable ---

    [Fact]
    public async Task Enable_WithAValidCode_TurnsItOn_AndReturnsTenRecoveryCodes()
    {
        await _service.Setup(_user.Id, Password);
        var secret = SavedSecret();

        var result = await _service.Enable(_user.Id, CodeAt(secret, _clock.GetUtcNow()));

        Assert.True(_user.TwoFactorEnabled);
        Assert.NotNull(_user.TwoFactorLastStep);
        Assert.Equal(10, result.Codes.Count);
        Assert.Equal(10, _codes.Count);
        // Only hashes are stored, never the codes
        Assert.All(result.Codes, code => Assert.DoesNotContain(_codes, c => c.CodeHash.Contains(code)));
    }

    [Fact]
    public async Task Enable_WithAWrongCode_DoesNotTurnItOn_AndCountsAsAFailedAttempt()
    {
        await _service.Setup(_user.Id, Password);

        await Assert.ThrowsAsync<ValidationException>(() => _service.Enable(_user.Id, "000000"));

        Assert.False(_user.TwoFactorEnabled);
        Assert.Equal(1, _user.TwoFactorFailedAttempts);
        Assert.Empty(_codes);
    }

    [Fact]
    public async Task Enable_WithoutSetup_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _service.Enable(_user.Id, "123456"));
    }

    [Fact]
    public async Task Enable_WhenAlreadyEnabled_ThrowsConflict()
    {
        var (secret, _) = await EnabledUser();

        await Assert.ThrowsAsync<ConflictException>(() => _service.Enable(_user.Id, CodeAt(secret, _clock.GetUtcNow())));
    }

    [Fact]
    public async Task Enable_DoesNotAcceptARecoveryCodeInsteadOfTheAppCode()
    {
        await _service.Setup(_user.Id, Password);

        await Assert.ThrowsAsync<ValidationException>(() => _service.Enable(_user.Id, "ABCDE-FGHJK"));
        Assert.False(_user.TwoFactorEnabled);
    }

    // --- VerifyCode ---

    [Fact]
    public async Task VerifyCode_AcceptsTheAppCode_AndRemembersItsStep()
    {
        var (secret, _) = await EnabledUser();
        var before = _user.TwoFactorLastStep;

        await _service.VerifyCode(_user, CodeAt(secret, _clock.GetUtcNow()));

        Assert.True(_user.TwoFactorLastStep > before);
    }

    [Fact]
    public async Task VerifyCode_RejectsTheSameCodeTwiceInItsWindow()
    {
        var (secret, _) = await EnabledUser();
        var code = CodeAt(secret, _clock.GetUtcNow());
        await _service.VerifyCode(_user, code);

        await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, code));
    }

    [Fact]
    public async Task VerifyCode_AcceptsARecoveryCode_OnlyOnce_AndIgnoresHowItIsTyped()
    {
        var (_, recoveryCodes) = await EnabledUser();
        var typed = recoveryCodes[3].ToLowerInvariant().Replace("-", " ");

        await _service.VerifyCode(_user, typed);

        Assert.Single(_codes, c => c.UsedAt != null);
        await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, recoveryCodes[3]));
    }

    [Fact]
    public async Task VerifyCode_AfterAFailure_ASuccessSetsTheCounterBackToZero()
    {
        var (secret, _) = await EnabledUser();
        await Fail(3);
        Assert.Equal(3, _user.TwoFactorFailedAttempts);

        await _service.VerifyCode(_user, CodeAt(secret, _clock.GetUtcNow()));

        Assert.Equal(0, _user.TwoFactorFailedAttempts);
    }

    [Fact]
    public async Task VerifyCode_AfterFiveFailures_LocksForFifteenMinutes_EvenWithTheCorrectCode()
    {
        var (secret, _) = await EnabledUser();
        await Fail(5);

        var locked = await Assert.ThrowsAsync<TooManyRequestsException>(() => _service.VerifyCode(_user, CodeAt(secret, _clock.GetUtcNow())));

        Assert.Equal(15 * 60, locked.RetryAfterSeconds);
        Assert.Contains("15 minutos", locked.Message);
    }

    [Fact]
    public async Task VerifyCode_TheLockCountsDown()
    {
        var (secret, _) = await EnabledUser();
        await Fail(5);
        _clock.Advance(TimeSpan.FromMinutes(10));

        var locked = await Assert.ThrowsAsync<TooManyRequestsException>(() => _service.VerifyCode(_user, CodeAt(secret, _clock.GetUtcNow())));

        Assert.Equal(5 * 60, locked.RetryAfterSeconds);
    }

    [Fact]
    public async Task VerifyCode_AfterTheLockExpires_TheCorrectCodeWorks_AndFiveMoreFailuresAreNeededToLockAgain()
    {
        var (secret, _) = await EnabledUser();
        await Fail(5);
        _clock.Advance(TimeSpan.FromMinutes(15));

        await _service.VerifyCode(_user, CodeAt(secret, _clock.GetUtcNow()));

        Assert.Equal(0, _user.TwoFactorFailedAttempts);
        Assert.Null(_user.TwoFactorLockedUntil);
        await Fail(4); // still not locked
    }

    [Fact]
    public async Task VerifyCode_AfterTheLockExpires_FourFailuresDoNotLockAgain()
    {
        await EnabledUser();
        await Fail(5);
        _clock.Advance(TimeSpan.FromMinutes(15));

        await Fail(4);

        await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, "000000"));
    }

    [Fact]
    public async Task VerifyCode_AWrongCodeIsAlwaysTheSameMessage()
    {
        var (_, _) = await EnabledUser();

        var wrongApp = await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, "000000"));
        var wrongRecovery = await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, "ABCDE-FGHJK"));

        Assert.Equal("Código incorrecto o vencido.", wrongApp.Message);
        Assert.Equal(wrongApp.Message, wrongRecovery.Message);
    }

    // --- Disable ---

    [Fact]
    public async Task Disable_WithPasswordAndAppCode_RemovesSecretAndCodes()
    {
        var (secret, _) = await EnabledUser();

        await _service.Disable(_user.Id, Password, CodeAt(secret, _clock.GetUtcNow()));

        Assert.False(_user.TwoFactorEnabled);
        Assert.Null(_user.TwoFactorSecret);
        Assert.Null(_user.TwoFactorLastStep);
        Assert.Empty(_codes);
    }

    [Fact]
    public async Task Disable_WithARecoveryCode_Works()
    {
        var (_, recoveryCodes) = await EnabledUser();

        await _service.Disable(_user.Id, Password, recoveryCodes[0]);

        Assert.False(_user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Disable_WithAWrongPassword_Fails_AndKeepsTheTwoFactorOn()
    {
        var (secret, _) = await EnabledUser();

        await Assert.ThrowsAsync<ValidationException>(() => _service.Disable(_user.Id, "wrong", CodeAt(secret, _clock.GetUtcNow())));

        Assert.True(_user.TwoFactorEnabled);
        Assert.Equal(1, _user.TwoFactorFailedAttempts);
    }

    [Fact]
    public async Task Disable_WithAWrongCode_Fails_AndKeepsTheTwoFactorOn()
    {
        await EnabledUser();

        await Assert.ThrowsAsync<ValidationException>(() => _service.Disable(_user.Id, Password, "000000"));

        Assert.True(_user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Disable_WhenNotEnabled_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _service.Disable(_user.Id, Password, "123456"));
    }

    // --- Regenerate ---

    [Fact]
    public async Task Regenerate_ReplacesTheOldCodes_WhichStopWorking()
    {
        var (secret, oldCodes) = await EnabledUser();

        var result = await _service.RegenerateRecoveryCodes(_user.Id, Password, CodeAt(secret, _clock.GetUtcNow()));

        Assert.Equal(10, result.Codes.Count);
        Assert.Empty(result.Codes.Intersect(oldCodes));
        await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyCode(_user, oldCodes[0]));
        await _service.VerifyCode(_user, result.Codes[0]);
    }

    [Fact]
    public async Task Regenerate_WhenNotEnabled_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _service.RegenerateRecoveryCodes(_user.Id, Password, "123456"));
    }

    // --- Lock applies to every operation ---

    [Fact]
    public async Task WhileLocked_EveryOperationThrowsTooManyRequests()
    {
        var (secret, _) = await EnabledUser();
        await Fail(5);
        var code = CodeAt(secret, _clock.GetUtcNow());

        await Assert.ThrowsAsync<TooManyRequestsException>(() => _service.Disable(_user.Id, Password, code));
        await Assert.ThrowsAsync<TooManyRequestsException>(() => _service.RegenerateRecoveryCodes(_user.Id, Password, code));
        await Assert.ThrowsAsync<TooManyRequestsException>(() => _service.Setup(_user.Id, Password));
        Assert.True(_user.TwoFactorEnabled);
    }
}
