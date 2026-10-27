using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>
/// Crea JWT di prova con lo stesso contratto di Identity (claim sub, preferred_username, name, role).
/// Issuer e audience sono quelli di appsettings.json del Gateway: se cambiano lì, questi test lo segnalano.
/// </summary>
public static class TestTokens
{
    public const string SigningKey = "chiave-di-test-del-gateway-almeno-32-byte-0123";
    public const string OtherSigningKey = "un-altra-chiave-di-test-diversa-dalla-prima-0123";
    public const string Issuer = "SmartCampus.Identity";
    public const string Audience = "SmartCampus";

    public static string Create(
        string? role,
        string signingKey = SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var remaining = lifetime ?? TimeSpan.FromMinutes(30);

        var claims = new List<Claim>
        {
            new("sub", "a0000000-0000-0000-0000-000000000001"),
            new("preferred_username", "utente.di.prova"),
            new("name", "Utente di Prova")
        };

        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            // Con una durata negativa il token è già scaduto (oltre la tolleranza di 30 secondi del Gateway).
            NotBefore = remaining < TimeSpan.Zero ? now - TimeSpan.FromHours(2) : now,
            Expires = now + remaining,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static string Student() => Create("Student");

    public static string Staff() => Create("Staff");
}
