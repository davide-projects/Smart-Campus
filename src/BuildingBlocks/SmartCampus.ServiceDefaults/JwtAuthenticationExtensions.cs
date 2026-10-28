using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SmartCampus.ServiceDefaults;

public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Username = "preferred_username";
    public const string Name = "name";
    public const string Role = "role";
}

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddSmartCampusJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SmartCampusJwtOptions>()
            .Bind(configuration.GetSection(SmartCampusJwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmartCampusJwtOptions>, SmartCampusJwtOptionsValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<SmartCampusJwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Mantiene invariati i nomi dei claim, così le policy leggono il ruolo originale del token.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimNames.Name,
                    RoleClaimType = JwtClaimNames.Role
                };
            });

        services.AddAuthorization();
        return services;
    }
}

public sealed class SmartCampusJwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

public sealed class SmartCampusJwtOptionsValidator : IValidateOptions<SmartCampusJwtOptions>
{
    public ValidateOptionsResult Validate(string? name, SmartCampusJwtOptions options)
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

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < SmartCampusJwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey è obbligatoria e deve avere almeno {SmartCampusJwtOptions.MinimumSigningKeyBytes} byte " +
                "(impostala negli user-secrets o nella variabile d'ambiente Jwt__SigningKey).");
        }

        if (options.ExpirationMinutes is < 1 or > 1440)
        {
            failures.Add("Jwt:ExpirationMinutes deve essere compreso tra 1 e 1440.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
