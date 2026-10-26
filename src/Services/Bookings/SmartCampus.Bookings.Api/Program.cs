using SmartCampus.ServiceDefaults;

// Servizio Bookings: chi userà quale spazio e quando. Prenotazioni senza conflitti e calcolo della disponibilità.
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
