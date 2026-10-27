using System.Net;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>Token non validi: il Gateway li rifiuta con 401 sulle route protette.</summary>
public sealed class TokenValidationTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    private const string ProtectedUrl = "/api/bookings/";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    public static TheoryData<string> InvalidTokens() => new()
    {
        { TestTokens.Create("Student", lifetime: TimeSpan.FromMinutes(-10)) },
        { TestTokens.Create("Student", signingKey: TestTokens.OtherSigningKey) },
        { TestTokens.Create("Student", issuer: "Qualcun.Altro") },
        { TestTokens.Create("Student", audience: "AltroPubblico") },
        { "non-e-un-jwt" }
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task ProtectedRoute_WithInvalidToken_Returns401(string token)
    {
        var response = await _client.SendAsync(HttpMethod.Get, ProtectedUrl, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithInvalidToken_ChallengesWithBearer()
    {
        var response = await _client.SendAsync(
            HttpMethod.Get, ProtectedUrl, TestTokens.Create("Student", lifetime: TimeSpan.FromMinutes(-10)));

        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task ProtectedRoute_WithNonBearerScheme_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "Basic dXRlbnRlOnBhc3N3b3Jk");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRoute_WithExpiredToken_IsStillServed()
    {
        // Il catalogo è pubblico: un token scaduto non lo blocca, viene semplicemente ignorato.
        var response = await _client.SendAsync(
            HttpMethod.Get, "/api/rooms/", TestTokens.Create("Student", lifetime: TimeSpan.FromMinutes(-10)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
