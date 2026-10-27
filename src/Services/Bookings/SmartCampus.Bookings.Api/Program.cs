using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.ServiceDefaults;

// Servizio Bookings: chi userà quale spazio e quando, e se lo ha usato davvero. Prenotazioni singole e di gruppo senza conflitti, disponibilità, check-in, mancate presentazioni e report.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Il DbContext legge la connection string "Database" dalla configurazione (user-secrets, variabili d'ambiente).
// La si legge dal provider e non da una variabile catturata, così i test di integrazione possono sostituirla.
builder.Services.AddDbContext<BookingDbContext>((provider, options) =>
    options.UseSqlServer(
        provider.GetRequiredService<IConfiguration>().GetConnectionString("Database"),
        sql => sql.EnableRetryOnFailure()));

// /health/ready resta "unhealthy" finché il database non risponde.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookingDbContext>("database");

var app = builder.Build();

// Migrazioni all'avvio: il database del servizio è dedicato, quindi è sicuro applicarle qui.
var connectionString = app.Configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogWarning("ConnectionStrings:Database non configurata: migrazioni non applicate.");
}
else
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database Bookings allineato con le migrazioni.");
}

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/bookings: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/bookings");

api.MapGet("/", () => Results.Ok(new { service = "bookings", status = "ok" }))
    .WithName("InfoBookings");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;