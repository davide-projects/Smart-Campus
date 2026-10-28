using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;

using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using SmartCampus.Contracts;
using SmartCampus.Rooms.Data;

using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace SmartCampus.Rooms.IntegrationTests;

public sealed class RoomsApiFixture : IAsyncLifetime
{
    private const string JwtSigningKey = "rooms-integration-signing-key-with-at-least-32-bytes";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("rooms-integration")
        .WithPassword("rooms-integration")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    private ServiceProvider? _seedReceiverProvider;

    public HttpClient Client { get; private set; } = null!;

    // RoomCreated ricevuti dal momento in cui il fixture si collega al broker, prima dell'avvio
    // di Rooms: comprendono quindi quelli della pubblicazione iniziale degli spazi seed.
    public Channel<RoomCreated> SeedRoomCreatedEvents { get; } = Channel.CreateUnbounded<RoomCreated>();

    public RabbitMqContainer RabbitMq => _rabbitMq;

    public void AuthorizeAsRole(string role)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: "SmartCampus.Identity",
            audience: "SmartCampus",
            claims: [new Claim("role", role)],
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    // Collega un bus di test al broker del fixture, con le stesse credenziali di Rooms.
    public void ConfigureRabbitMqHost(IRabbitMqBusFactoryConfigurator rabbitMq) =>
        rabbitMq.Host(
            "localhost",
            _rabbitMq.GetMappedPublicPort(5672),
            "/",
            host =>
            {
                host.Username("rooms-integration");
                host.Password("rooms-integration");
            });

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await _rabbitMq.StartAsync();
        await StartSeedReceiverAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
                builder.UseSetting("Jwt:Issuer", "SmartCampus.Identity");
                builder.UseSetting("Jwt:Audience", "SmartCampus");
                builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
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

    private async Task StartSeedReceiverAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(SeedRoomCreatedEvents);
        services.AddMassTransit(configure =>
        {
            configure.AddConsumer<SeedRoomCreatedConsumer>();
            configure.UsingRabbitMq((context, rabbitMq) =>
            {
                ConfigureRabbitMqHost(rabbitMq);
                rabbitMq.ReceiveEndpoint($"rooms-seed-events-{Guid.NewGuid():N}", endpoint =>
                    endpoint.ConfigureConsumer<SeedRoomCreatedConsumer>(context));
            });
        });

        _seedReceiverProvider = services.BuildServiceProvider();
        await _seedReceiverProvider.GetRequiredService<IBusControl>().StartAsync();
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

        if (_seedReceiverProvider is not null)
        {
            await _seedReceiverProvider.GetRequiredService<IBusControl>().StopAsync();
            await _seedReceiverProvider.DisposeAsync();
        }

        await _rabbitMq.DisposeAsync();
        await _database.DisposeAsync();
    }
}

internal sealed class SeedRoomCreatedConsumer(Channel<RoomCreated> events) : IConsumer<RoomCreated>
{
    public Task Consume(ConsumeContext<RoomCreated> context)
    {
        events.Writer.TryWrite(context.Message);
        return Task.CompletedTask;
    }
}
