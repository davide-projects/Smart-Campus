using SmartCampus.ServiceDefaults;

// Servizio Bookings: chi userà quale spazio e quando, e se lo ha usato davvero. Prenotazioni singole e di gruppo senza conflitti, disponibilità, check-in, mancate presentazioni e report.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/bookings: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/bookings");

api.MapGet("/", () => Results.Ok(new { service = "bookings", status = "ok" }))
   .WithName("InfoBookings");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
