extern alias Gateway;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SmartCampus.Rooms.Data;

using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

using Yarp.ReverseProxy.Forwarder;

namespace SmartCampus.EndToEnd.Tests;

// Prova il percorso reale dell'utente: client -> gateway -> servizio Rooms -> SQL Server.
// Il gateway non apre porte: le sue destinazioni vengono dirottate sul TestServer di Rooms
public sealed class GatewayRoomsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("rooms-end-to-end")
        .WithPassword("rooms-end-to-end")
        .Build();

    private WebApplicationFactory<Program>? _roomsFactory;
    private WebApplicationFactory<Gateway::Program>? _gatewayFactory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await _rabbitMq.StartAsync();

        _roomsFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Jwt:Issuer", "SmartCampus.Identity");
                builder.UseSetting("Jwt:Audience", "SmartCampus");
                builder.UseSetting("Jwt:SigningKey", "rooms-end-to-end-signing-key-with-at-least-32-bytes");
                builder.UseSetting("RabbitMq:Host", "localhost");
                builder.UseSetting("RabbitMq:Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
                builder.UseSetting("RabbitMq:Username", "rooms-end-to-end");
                builder.UseSetting("RabbitMq:Password", "rooms-end-to-end");

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
                // Il gateway valida i JWT; per il catalogo anonimo basta una chiave di prova valida.
                builder.UseSetting("Jwt:SigningKey", "chiave-di-test-e2e-almeno-32-byte-0123456789");

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

        await _rabbitMq.DisposeAsync();
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
