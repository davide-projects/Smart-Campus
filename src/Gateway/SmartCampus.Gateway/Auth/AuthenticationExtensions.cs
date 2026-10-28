using SmartCampus.ServiceDefaults;

namespace SmartCampus.Gateway.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSmartCampusJwtBearer(configuration);
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
