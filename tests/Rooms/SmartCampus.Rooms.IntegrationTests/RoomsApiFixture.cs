using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartCampus.Rooms.Data;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace SmartCampus.Rooms.IntegrationTests;


public sealed class RoomsApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("rooms-integration")
        .WithPassword("rooms-integration")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public RabbitMqContainer RabbitMq => _rabbitMq;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await _rabbitMq.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
                builder.UseSetting("RabbitMq:Host", "localhost");
                builder.UseSetting("RabbitMq:Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
                builder.UseSetting("RabbitMq:VirtualHost", "/");
                builder.UseSetting("RabbitMq:Username", "rooms-integration");
                builder.UseSetting("RabbitMq:Password", "rooms-integration");

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

        
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
            await db.Database.MigrateAsync();
        }

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    public async Task<int> GetSeedDispatchCountAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
        return await db.RoomSeedEventDispatches.CountAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _rabbitMq.DisposeAsync();
        await _database.DisposeAsync();
    }
}
