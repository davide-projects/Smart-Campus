using System.Net;
using System.Net.Http.Json;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>
/// Route della prenotazione di gruppo (US-05): la verifica interna degli studenti non è instradata,
/// la ricerca degli studenti passa dalla route <c>identity</c> esistente.
/// </summary>
public sealed class StudentRoutesTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    private const string SearchUrl = "/api/identity/students?search=bia";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InternalStudentsCheck_Returns404AndDoesNotReachDownstream(bool withToken)
    {
        var token = withToken ? TestTokens.Student() : null;
        var receivedBefore = fixture.Downstream.ReceivedPaths.Count;

        var response = await _client.SendAsync(HttpMethod.Post, "/internal/users/students/check", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(receivedBefore, fixture.Downstream.ReceivedPaths.Count);
        Assert.DoesNotContain(fixture.Downstream.ReceivedPaths, path => path.StartsWith("/internal"));
    }

    [Fact]
    public async Task StudentSearch_WithoutToken_Returns401AndDoesNotReachDownstream()
    {
        // I test della classe girano uno alla volta: si confronta il numero di richieste prima e dopo.
        var receivedBefore = fixture.Downstream.ReceivedPaths.Count;

        var response = await _client.SendAsync(HttpMethod.Get, SearchUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(receivedBefore, fixture.Downstream.ReceivedPaths.Count);
    }

    // Il Gateway inoltra anche lo Staff: il 403 «Ricerca non consentita» lo restituisce Identity.
    [Theory]
    [InlineData("Student")]
    [InlineData("Staff")]
    public async Task StudentSearch_WithValidToken_IsForwardedToIdentity(string role)
    {
        var token = TestTokens.Create(role);

        var response = await _client.SendAsync(HttpMethod.Get, SearchUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal("GET", echo!.Method);
        Assert.Equal("/api/identity/students", echo.Path);
        Assert.Equal($"Bearer {token}", echo.Authorization);
    }
}
