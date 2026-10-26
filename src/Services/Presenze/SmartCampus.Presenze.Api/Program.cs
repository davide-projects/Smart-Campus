using SmartCampus.ServiceDefaults;

// Servizio Presenze: lo spazio prenotato è stato usato davvero? Check-in, mancate presentazioni e report per il personale.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/presenze: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/presenze");

api.MapGet("/", () => Results.Ok(new { servizio = "presenze", stato = "ok" }))
   .WithName("InfoPresenze");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
