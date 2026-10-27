using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using SmartCampus.Identity.Api.Auth;
using SmartCampus.Identity.Api.Domain;
using SmartCampus.Identity.Api.Endpoints;
using SmartCampus.Identity.Api.Infrastructure;

namespace SmartCampus.Identity.IntegrationTests;

public class IdentityApiTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthLive_Returns200()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_WithDatabaseAvailable_Returns200()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    // ---- Seed ----

    [Fact]
    public async Task Seed_LoadsFiveStudentsAndTwoStaff()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityContext>();

        Assert.Equal(5, await db.Users.CountAsync(user => user.Role == UserRole.Student));
        Assert.Equal(2, await db.Users.CountAsync(user => user.Role == UserRole.Staff));
    }

    [Fact]
    public async Task Seed_DoesNotStorePlainPasswords()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityContext>();

        var hashes = await db.Users.Select(user => user.PasswordHash).ToListAsync();

        Assert.All(hashes, hash => Assert.DoesNotContain(IdentityApiFactory.SeedPassword, hash));
    }

    // ---- POST /api/identity/login ----

    [Fact]
    public async Task Login_ValidStudentCredentials_ReturnsTokenAndProfile()
    {
        var response = await LoginAsync("student1", IdentityApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(SeedUsers.Student1Id, body.User.Id);
        Assert.Equal("Mario Rossi", body.User.DisplayName);
        Assert.Equal("Student", body.User.Role);
    }

    [Fact]
    public async Task Login_ValidStaffCredentials_ReturnsStaffRole()
    {
        var response = await LoginAsync("staff1", IdentityApiFactory.SeedPassword);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Staff", body!.User.Role);
    }

    [Fact]
    public async Task Login_ResponseDoesNotExposePasswordHash()
    {
        var response = await LoginAsync("student1", IdentityApiFactory.SeedPassword);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithGenericMessage()
    {
        var response = await LoginAsync("student1", "password-sbagliata");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Nome utente o password non corretti.", text);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsSameResponseAsWrongPassword()
    {
        var wrongPassword = await LoginAsync("student1", "password-sbagliata");
        var unknownUser = await LoginAsync("utente-inesistente", "password-sbagliata");

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Contains("Nome utente o password non corretti.", await unknownUser.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "qualcosa")]
    [InlineData("student1", "")]
    [InlineData("   ", "qualcosa")]
    public async Task Login_MissingFields_Returns400(string username, string password)
    {
        var response = await LoginAsync(username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- GET /api/identity/me ----

    [Fact]
    public async Task Me_WithValidToken_ReturnsOwnProfile()
    {
        var token = await GetTokenAsync("student2");

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        Assert.Equal(SeedUsers.Student2Id, profile!.Id);
        Assert.Equal("student2", profile.Username);
        Assert.Equal("Anna Bianchi", profile.DisplayName);
        Assert.Equal("Student", profile.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await GetMeAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenSignedWithAnotherKey_Returns401()
    {
        var forged = CreateToken("un-altra-chiave-di-test-di-32-byte-9876543210", DateTime.UtcNow.AddHours(1));

        var response = await GetMeAsync(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var expired = CreateToken(IdentityApiFactory.SigningKey, DateTime.UtcNow.AddMinutes(-10));

        var response = await GetMeAsync(expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        var response = await GetMeAsync("non.un.token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Utilità ----

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        _client.PostAsJsonAsync("/api/identity/login", new LoginRequest(username, password));

    private async Task<string> GetTokenAsync(string username)
    {
        var response = await LoginAsync(username, IdentityApiFactory.SeedPassword);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/identity/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private static string CreateToken(string signingKey, DateTime expiresUtc)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "SmartCampus.Identity",
            Audience = "SmartCampus",
            NotBefore = expiresUtc.AddHours(-2),
            Expires = expiresUtc,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = SeedUsers.Student1Id.ToString(),
                [JwtClaimNames.Role] = "Student"
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
