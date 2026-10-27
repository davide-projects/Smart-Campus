using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SmartCampus.Gateway.Auth;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registra la validazione dei JWT emessi da Identity e le policy di accesso usate dalle route.
    /// Le opzioni sono validate all'avvio (il Gateway non parte senza una chiave valida) e i parametri
    /// di validazione sono letti in modo differito, così la configurazione è quella finale dell'host.
    /// </summary>
    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GatewayJwtOptions>()
            .Bind(configuration.GetSection(GatewayJwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GatewayJwtOptions>, GatewayJwtOptionsValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<GatewayJwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Mantiene i nomi dei claim così come sono nel token ("sub", "role", ...).
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimNames.Name,
                    RoleClaimType = JwtClaimNames.Role
                };
            });

        services.AddAuthorization(options =>
        {
            // "Default" (utente autenticato) e "Anonymous" sono policy riservate di YARP: qui si definisce solo quella per ruolo.
            options.AddPolicy(AccessPolicies.Staff, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(Roles.Staff));
        });

        return services;
    }
}
