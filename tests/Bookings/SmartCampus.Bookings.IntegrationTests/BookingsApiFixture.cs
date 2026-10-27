using MassTransit;
using MassTransit.Testing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Consumers;
using Testcontainers.MsSql;

namespace SmartCampus.Bookings.IntegrationTests;

// Prepara un SQL Server reale (Testcontainers) e il servizio Bookings con un bus in memoria
// al posto di RabbitMQ. Condivisa da tutti i test della classe che la usa.
public sealed class BookingsApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                // Il servizio legge questa connection string: le migrazioni all'avvio
                // creano le tabelle nel database del container.
                builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());

                // Sostituisce RabbitMQ con il bus in memoria di MassTransit: niente broker nei test.
                builder.ConfigureTestServices(services =>
                    services.AddMassTransitTestHarness(bus => bus.AddConsumer<RoomEventsConsumer>()));
            });

        // Accedere ai servizi avvia l'applicazione (migrazioni + bus).
        await Factory.Services.GetTestHarness().Start();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}