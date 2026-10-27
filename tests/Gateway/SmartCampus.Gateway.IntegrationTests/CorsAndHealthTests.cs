using System.Net;

namespace SmartCampus.Gateway.IntegrationTests;

public sealed class CorsAndHealthTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    private const string AllowedOrigin = "http://localhost:4200";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData("/api/bookings/", "GET")]
    [InlineData("/api/identity/me", "GET")]
    [InlineData("/api/rooms/", "POST")]
    public async Task Preflight_OnProtectedRoute_IsNotRejectedForMissingToken(string url, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Unauthorized_Response_StillCarriesCorsHeaders()
    {
        // Senza l'intestazione il browser nasconderebbe il 401 al frontend.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/bookings/");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_IsAnonymous(string url)
    {
        var response = await _client.SendAsync(HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
