using SmartCampus.ServiceDefaults;

// Servizio Spazi: quali spazi esistono e come sono fatti. Anagrafica di edifici, spazi, attrezzature e orari di apertura.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/spazi: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/spazi");

api.MapGet("/", () => Results.Ok(new { servizio = "spazi", stato = "ok" }))
   .WithName("InfoSpazi");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
