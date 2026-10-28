using Microsoft.Extensions.Options;
using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.Api.Auth;

public static class AuthenticationExtensions
{
    // Registra opzioni JWT validate all'avvio, emissione dei token e validazione dei token in ingresso.
    public static IServiceCollection AddSmartCampusJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddSmartCampusJwtBearer(configuration);
        return services;
    }
}
