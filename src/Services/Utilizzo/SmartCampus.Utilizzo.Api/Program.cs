using SmartCampus.ServiceDefaults;

// Servizio Utilizzo: Utilizzo effettivo degli spazi (check-in, no-show) e report per il personale.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/utilizzo: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/utilizzo");

api.MapGet("/", () => Results.Ok(new { servizio = "utilizzo", stato = "ok" }))
   .WithName("InfoUtilizzo");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
