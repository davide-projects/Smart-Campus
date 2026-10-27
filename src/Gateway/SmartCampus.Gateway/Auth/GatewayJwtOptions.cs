using System.Text;
using Microsoft.Extensions.Options;

namespace SmartCampus.Gateway.Auth;

/// <summary>
/// Nomi dei claim del token emesso da Identity (vedi docs/identity.md, "Contratto del token").
/// Sono copiati qui di proposito: il Gateway non deve dipendere dal progetto Identity.
/// </summary>
public static class JwtClaimNames
{
    public const string Name = "name";
    public const string Role = "role";
}

/// <summary>Ruoli presenti nel claim <c>role</c>.</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Staff = "Staff";
}

/// <summary>
/// Nomi delle policy usate in <c>AuthorizationPolicy</c> nelle route YARP (appsettings.json).
/// <c>Default</c> (utente autenticato) e <c>Anonymous</c> (nessun controllo) sono nomi riservati da YARP.
/// </summary>
public static class AccessPolicies
{
    public const string Staff = "Staff";
}

/// <summary>Configurazione del JWT (sezione "Jwt"): deve coincidere con quella di Identity. La chiave è un segreto.</summary>
public sealed class GatewayJwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;
}

public sealed class GatewayJwtOptionsValidator : IValidateOptions<GatewayJwtOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayJwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer è obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience è obbligatorio.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) < GatewayJwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey è obbligatoria e deve avere almeno {GatewayJwtOptions.MinimumSigningKeyBytes} byte " +
                "(è la stessa chiave di Identity: impostala negli user-secrets o nella variabile d'ambiente Jwt__SigningKey).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
