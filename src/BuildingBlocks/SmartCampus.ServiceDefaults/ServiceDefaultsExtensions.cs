using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace SmartCampus.ServiceDefaults;

/// <summary>
/// Configurazione comune a tutti i servizi: health check, ProblemDetails, OpenAPI.
/// Ogni servizio la attiva con <c>builder.AddServiceDefaults()</c> e <c>app.MapDefaultEndpoints()</c>.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        // "live" = il processo risponde. I check "ready" (es. database) li aggiunge ogni servizio.
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // Endpoint usati da docker compose e, in cloud, dalle probe di Container Apps / Kubernetes.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag)
        });
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
