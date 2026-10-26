using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
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

// /health/ready resta "unhealthy" finché il database non risponde: docker compose aspetta questo.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RoomsDbContext>("database");

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

    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database Rooms allineato con le migrazioni.");
}

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/rooms: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/rooms");

api.MapRoomEndpoints();

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
