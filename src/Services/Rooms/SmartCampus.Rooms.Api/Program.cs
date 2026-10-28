using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MongoDB.Driver;
using SmartCampus.Rooms.Data;
using SmartCampus.Rooms.Features.Rooms;
using SmartCampus.ServiceDefaults;

// Servizio Rooms: quali spazi esistono e come sono fatti. Anagrafica di edifici, spazi, attrezzature e orari di apertura.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Il connection string serve anche a "dotnet ef" e ai test di integrazione, che sostituiscono
// la registrazione del DbContext: per questo si legge dal provider e non da una variabile catturata.
builder.Services.AddDbContext<RoomsDbContext>((provider, options) =>
    options.UseSqlServer(
        provider.GetRequiredService<IConfiguration>().GetConnectionString("Database"),
        sql => sql.EnableRetryOnFailure()));

var mongoConnectionString = builder.Configuration["Mongo:ConnectionString"]
    ?? throw new InvalidOperationException("Mongo:ConnectionString non configurata.");
var mongoDatabaseName = builder.Configuration["Mongo:DatabaseName"]
    ?? throw new InvalidOperationException("Mongo:DatabaseName non configurato.");
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));
builder.Services.AddSingleton<EquipmentStore>();
builder.Services.AddScoped<LegacyEquipmentDataImporter>();

builder.Services.AddSmartCampusJwtBearer(builder.Configuration);

var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitMqPort = builder.Configuration.GetValue<ushort?>("RabbitMq:Port") ?? 5672;
var rabbitMqVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitMqUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitMqPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddMassTransit(configure =>
{
    configure.AddEntityFrameworkOutbox<RoomsDbContext>(outbox =>
    {
        outbox.UseSqlServer();
        outbox.UseBusOutbox();
    });

    configure.UsingRabbitMq((context, rabbitMq) =>
    {
        rabbitMq.Host(rabbitMqHost, rabbitMqPort, rabbitMqVirtualHost, host =>
        {
            host.Username(rabbitMqUsername);
            host.Password(rabbitMqPassword);
        });

        rabbitMq.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<RoomSeedEventPublisher>();

// La readiness include la disponibilità del database, per le probe di orchestrazione.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RoomsDbContext>("database")
    .AddCheck<MongoDbHealthCheck>("mongodb");

// Il tipo di spazio viaggia come stringa ("StudyRoom") e non come numero: più leggibile per il frontend.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Migrazioni all'avvio: in sviluppo e in docker il database del servizio è dedicato,
// quindi non c'è rischio di applicare migrazioni di altri servizi.
var connectionString = app.Configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogWarning("ConnectionStrings:Database non configurata: migrazioni non applicate.");
}
else
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
    var migrationIds = db.Database.GetMigrations().ToArray();
    var equipmentMigrationIndex = Array.FindIndex(
        migrationIds,
        migrationId => migrationId.EndsWith("_MoveEquipmentToMongoDb", StringComparison.Ordinal));
    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToHashSet(StringComparer.Ordinal);

    // Migrazione in passi per le attrezzature (storia #76): la migrazione MoveEquipmentToMongoDb
    // elimina le tabelle SQL Equipment e RoomEquipment, quindi l'import verso MongoDB deve
    // avvenire PRIMA che venga applicata. Si migra così fino alla migrazione precedente, si
    // copiano i dati legacy e solo dopo si completa la migrazione dello schema.
    if (equipmentMigrationIndex > 0 && pendingMigrations.Contains(migrationIds[equipmentMigrationIndex]))
    {
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(migrationIds[equipmentMigrationIndex - 1], CancellationToken.None);
        await scope.ServiceProvider
            .GetRequiredService<LegacyEquipmentDataImporter>()
            .ImportAsync(CancellationToken.None);
    }

    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<EquipmentStore>().EnsureSeededAsync(CancellationToken.None);
    await scope.ServiceProvider
        .GetRequiredService<RoomSeedEventPublisher>()
        .PublishOnceAsync(CancellationToken.None);
    app.Logger.LogInformation("Database Rooms e raccolte MongoDB allineati.");
}

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/rooms: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/rooms");

api.MapRoomEndpoints();

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
