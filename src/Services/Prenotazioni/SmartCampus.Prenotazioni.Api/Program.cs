using SmartCampus.ServiceDefaults;

// Servizio Prenotazioni: chi userà quale spazio e quando. Prenotazioni senza conflitti e calcolo della disponibilità.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/prenotazioni: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/prenotazioni");

api.MapGet("/", () => Results.Ok(new { servizio = "prenotazioni", stato = "ok" }))
   .WithName("InfoPrenotazioni");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
