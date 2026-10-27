extern alias Gateway;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartCampus.Rooms.Data;
using Testcontainers.MsSql;
using Yarp.ReverseProxy.Forwarder;

namespace SmartCampus.EndToEnd.Tests;

// Prova il percorso reale dell'utente: client -> gateway -> servizio Rooms -> SQL Server.
// Il gateway non apre porte: le sue destinazioni vengono dirottate sul TestServer di Rooms,
// così si esercita la pipeline YARP (rotte, inoltro) senza dipendere da processi esterni.
public sealed class GatewayRoomsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private WebApplicationFactory<Program>? _roomsFactory;
    private WebApplicationFactory<Gateway::Program>? _gatewayFactory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _roomsFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<RoomsDbContext>>();
                    services.RemoveAll<RoomsDbContext>();

                    services.AddDbContext<RoomsDbContext>(options =>
                        options.UseSqlServer(
                            _database.GetConnectionString(),
                            sql => sql.EnableRetryOnFailure()));
                });
            });

        await using (var scope = _roomsFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
            await db.Database.MigrateAsync();
        }

        var roomsInvoker = new HttpMessageInvoker(
            _roomsFactory.Server.CreateHandler(),
            disposeHandler: false);

        _gatewayFactory = new WebApplicationFactory<Gateway::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IForwarderHttpClientFactory>();
                    services.AddSingleton<IForwarderHttpClientFactory>(
                        new RoomsForwarderHttpClientFactory(roomsInvoker));
                });
            });

        Client = _gatewayFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    public async Task DisposeAsync()
    {
        if (_gatewayFactory is not null)
        {
            await _gatewayFactory.DisposeAsync();
        }

        if (_roomsFactory is not null)
        {
            await _roomsFactory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    // Tutte le destinazioni del gateway finiscono sul TestServer di Rooms:
    // identity e bookings non sono coinvolti da US-01.
    private sealed class RoomsForwarderHttpClientFactory(HttpMessageInvoker invoker)
        : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => invoker;
    }
}
