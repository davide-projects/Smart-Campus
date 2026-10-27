using System.Text;

using Microsoft.Extensions.Options;

namespace SmartCampus.Identity.Api.Auth;

// Nomi dei claim presenti nel token. Il Gateway e gli altri servizi leggono gli stessi nomi (#30).
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Username = "preferred_username";
    public const string Name = "name";
    public const string Role = "role";
}

// Configurazione del JWT (sezione "Jwt"). La chiave di firma è un segreto: user-secrets o variabile d'ambiente.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
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

        if (Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) < JwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey è obbligatoria e deve avere almeno {JwtOptions.MinimumSigningKeyBytes} byte " +
                "(impostala negli user-secrets o nella variabile d'ambiente Jwt__SigningKey).");
        }

        if (options.ExpirationMinutes is < 1 or > 1440)
        {
            failures.Add("Jwt:ExpirationMinutes deve essere compreso tra 1 e 1440.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
