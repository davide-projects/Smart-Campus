using System.Net;
using System.Net.Http.Json;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>Chi può chiamare cosa: anonimo, Student e Staff su ogni gruppo di route.</summary>
public sealed class AccessRulesTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Login_IsAnonymous_AndReachesIdentity()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/identity/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal("POST", echo!.Method);
        Assert.Equal("/api/identity/login", echo.Path);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.SendAsync(HttpMethod.Get, "/api/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Staff")]
    public async Task Me_WithValidToken_IsForwardedWithTheAuthorizationHeader(string role)
    {
        var token = TestTokens.Create(role);

        var response = await _client.SendAsync(HttpMethod.Get, "/api/identity/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal("/api/identity/me", echo!.Path);
        Assert.Equal($"Bearer {token}", echo.Authorization);
    }

    [Theory]
    [InlineData("/api/rooms/")]
    [InlineData("/api/rooms/11111111-1111-1111-1111-111111111111")]
    [InlineData("/api/rooms/?type=Classroom&minCapacity=30")]
    public async Task RoomsCatalog_IsAnonymous(string url)
    {
        var response = await _client.SendAsync(HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal("GET", echo!.Method);
        Assert.StartsWith("/api/rooms/", echo.Path);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task RoomsManagement_WithoutToken_Returns401(string method)
    {
        var response = await _client.SendAsync(new HttpMethod(method), "/api/rooms/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task RoomsManagement_AsStudent_Returns403(string method)
    {
        var response = await _client.SendAsync(new HttpMethod(method), "/api/rooms/", TestTokens.Student());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task RoomsManagement_AsStaff_ReachesRooms(string method)
    {
        var response = await _client.SendAsync(new HttpMethod(method), "/api/rooms/", TestTokens.Staff());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal(method, echo!.Method);
    }

    [Fact]
    public async Task RoomsManagement_WithUnknownRole_Returns403()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/rooms/", TestTokens.Create("Admin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoomsManagement_WithTokenWithoutRole_Returns403()
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/rooms/", TestTokens.Create(role: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task Bookings_WithoutToken_Returns401(string method)
    {
        var response = await _client.SendAsync(new HttpMethod(method), "/api/bookings/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Staff")]
    public async Task Bookings_WithValidToken_ReachesBookings(string role)
    {
        var response = await _client.SendAsync(HttpMethod.Get, "/api/bookings/", TestTokens.Create(role));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal("/api/bookings/", echo!.Path);
    }

    private const string FutureCountUrl =
        "/api/bookings/rooms/11111111-1111-1111-1111-111111111111/future-reservations-count";

    [Fact]
    public async Task FutureReservationsCount_WithoutToken_Returns401()
    {
        var response = await _client.SendAsync(HttpMethod.Get, FutureCountUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FutureReservationsCount_AsStudent_Returns403()
    {
        var response = await _client.SendAsync(HttpMethod.Get, FutureCountUrl, TestTokens.Student());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FutureReservationsCount_AsStaff_ReachesBookings()
    {
        var response = await _client.SendAsync(HttpMethod.Get, FutureCountUrl, TestTokens.Staff());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal(FutureCountUrl, echo!.Path);
    }
}
