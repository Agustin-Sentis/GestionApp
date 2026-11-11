using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GastosBackend.Application.Interfaces;

namespace GastosBackend.Tests.Integration;

/// <summary>The real JwtBearer pipeline with a validator the test controls: what a closed session looks like from outside.</summary>
public class SessionRejectionTests : IClassFixture<SessionRejectionTests.CapturingFactory>
{
    public class CapturingFactory : TestApiFactory
    {
        public CapturingValidator Validator { get; } = new();

        protected override IUserSessionValidator SessionValidator => Validator;
    }

    public class CapturingValidator : IUserSessionValidator
    {
        public bool Accepts { get; set; } = true;
        public List<(Guid UserId, long? IssuedAt)> Calls { get; } = new();

        public Task<bool> IsValid(Guid userId, long? issuedAtUnixSeconds)
        {
            lock (Calls) Calls.Add((userId, issuedAtUnixSeconds));
            return Task.FromResult(Accepts);
        }
    }

    private readonly CapturingFactory _factory;

    public SessionRejectionTests(CapturingFactory factory)
    {
        _factory = factory;
        _factory.Validator.Accepts = false;
        lock (_factory.Validator.Calls) _factory.Validator.Calls.Clear();
    }

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ARejectedSession_Gets401_OnAnAuthenticatedEndpoint()
    {
        var response = await ClientWith(TestApiFactory.CreateToken()).GetAsync("/api/category");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheValidator_ReceivesTheUserAndTheIssuedAtOfTheToken()
    {
        var userId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 11, 10, 15, 0, 7, TimeSpan.Zero);

        await ClientWith(TestApiFactory.CreateToken(userId, issuedAt)).GetAsync("/api/category");

        var call = Assert.Single(_factory.Validator.Calls);
        Assert.Equal(userId, call.UserId);
        Assert.Equal(issuedAt.ToUnixTimeSeconds(), call.IssuedAt);
    }

    [Fact]
    public async Task ATokenWithoutIssuedAt_ReachesTheValidatorWithNull()
    {
        await ClientWith(TestApiFactory.CreateToken()).GetAsync("/api/category");

        Assert.Null(Assert.Single(_factory.Validator.Calls).IssuedAt);
    }

    [Fact]
    public async Task AnAnonymousEndpoint_IsNotAffected_ByAClosedSessionInTheBrowser()
    {
        // The reset page may be opened in a browser that still has a stale session: that must not get in the way
        var response = await ClientWith(TestApiFactory.CreateToken())
            .PostAsJsonAsync("/api/auth/signin", new { email = "a@x.com", password = "Demo1234!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnAcceptedSession_ReachesTheEndpoint()
    {
        _factory.Validator.Accepts = true;

        var response = await ClientWith(TestApiFactory.CreateToken()).GetAsync("/api/category");

        // Past authentication: what follows needs a database that this factory does not have, so it is not a 401
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
