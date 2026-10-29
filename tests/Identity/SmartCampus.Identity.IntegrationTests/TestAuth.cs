using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using SmartCampus.Identity.Api.Endpoints;
using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.IntegrationTests;

/// <summary>Token degli utenti di seed e richieste autenticate per i test di integrazione.</summary>
public static class TestAuth
{
    public const string OtherSigningKey = "un-altra-chiave-di-test-di-32-byte-9876543210";

    /// <summary>Esegue il login con la password di seed e restituisce il token.</summary>
    public static async Task<string> GetTokenAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/identity/login", new LoginRequest(username, IdentityApiFactory.SeedPassword));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    /// <summary>Crea una richiesta con <c>Authorization: Bearer</c> se il token è presente.</summary>
    public static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    /// <summary>Token con emittente e destinatario corretti, firmato con la chiave indicata.</summary>
    public static string CreateToken(string signingKey, Guid subject, string role, DateTime expiresUtc)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "SmartCampus.Identity",
            Audience = "SmartCampus",
            NotBefore = expiresUtc.AddHours(-2),
            Expires = expiresUtc,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = subject.ToString(),
                [JwtClaimNames.Role] = role
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
