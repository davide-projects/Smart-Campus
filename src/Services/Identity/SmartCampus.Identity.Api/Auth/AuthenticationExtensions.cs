using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.Api.Auth;

public static class AuthenticationExtensions
{
    // Registra opzioni JWT validate all'avvio, emissione dei token e validazione dei token in ingresso.
    public static IServiceCollection AddSmartCampusJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddSmartCampusJwtBearer(configuration);
        return services;
    }
}
