using Microsoft.EntityFrameworkCore;

namespace SmartCampus.Identity.Api.Infrastructure;

// All'avvio del servizio prepara il database e carica gli utenti sintetici
public sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityContext>();

        await db.Database.MigrateAsync(cancellationToken);

        var seedPassword = configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            logger.LogInformation("Seed:DefaultPassword non impostata: utenti sintetici non caricati.");
            return;
        }

        await scope.ServiceProvider.GetRequiredService<UserSeeder>().SeedAsync(seedPassword, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
