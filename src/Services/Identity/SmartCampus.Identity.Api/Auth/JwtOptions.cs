namespace SmartCampus.Identity.Api.Auth;

// Configurazione del JWT (sezione "Jwt"). La chiave di firma è un segreto: user-secrets o variabile d'ambiente.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}
