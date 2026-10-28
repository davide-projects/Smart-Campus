using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.ServiceDefaults;
using MassTransit;
using SmartCampus.Bookings.Api.Consumers;
using SmartCampus.Bookings.Api.Features.Reservations;

// Servizio Bookings: chi userà quale spazio e quando, e se lo ha usato davvero. Prenotazioni singole e di gruppo senza conflitti, disponibilità, check-in, mancate presentazioni e report.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Verifica firma, issuer, audience e scadenza del JWT emesso da Identity (stessa configurazione "Jwt" del Gateway).
builder.Services.AddSmartCampusJwtBearer(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

// Gli enum viaggiano come stringa ("Confirmed") e non come numero, come in Rooms.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Il DbContext legge la connection string "Database" dalla configurazione (user-secrets, variabili d'ambiente).
// La si legge dal provider e non da una variabile catturata, così i test di integrazione possono sostituirla.
builder.Services.AddDbContext<BookingDbContext>((provider, options) =>
    options.UseSqlServer(
        provider.GetRequiredService<IConfiguration>().GetConnectionString("Database"),
        sql => sql.EnableRetryOnFailure()));

// /health/ready resta "unhealthy" finché il database non risponde.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookingDbContext>("database");

// Messaggistica: in locale RabbitMQ, in cloud si sostituirà con Azure Service Bus cambiando solo il trasporto.
builder.Services.AddMassTransit(bus =>
{
// Un messaggio alla volta: gli eventi dello stesso spazio vengono applicati nell'ordine di arrivo.
    bus.AddConsumer<RoomEventsConsumer>()
        .Endpoint(endpoint => endpoint.ConcurrentMessageLimit = 1);

    // Nomi delle code leggibili, es. "room-events" per RoomEventsConsumer.
    bus.SetKebabCaseEndpointNameFormatter();

    bus.UsingRabbitMq((context, rabbit) =>
    {
        var config = context.GetRequiredService<IConfiguration>();

        rabbit.Host(config["RabbitMq:Host"] ?? "localhost", "/", host =>
        {
            host.Username(config["RabbitMq:Username"] ?? "guest");
            host.Password(config["RabbitMq:Password"] ?? "guest");
        });

        // Se il database è giù per un attimo, riprova prima di considerare il messaggio fallito.
        rabbit.UseMessageRetry(retry => retry.Intervals(100, 500, 1000, 5000));

        rabbit.ConfigureEndpoints(context);
    });
});

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

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/bookings: il gateway le inoltra senza riscriverle.
// Richiedono un token valido anche se qualcuno chiama Bookings direttamente, senza passare dal Gateway.
// Gli endpoint di salute (/health/*) restano fuori dal gruppo e quindi anonimi.
var api = app.MapGroup("/api/bookings").RequireAuthorization();
api.MapReservationEndpoints();
api.MapReservationListEndpoints();
api.MapAvailabilityEndpoints();
api.MapAvailabilitySearchEndpoints();

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
