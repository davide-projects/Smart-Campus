using SmartCampus.ServiceDefaults;

// Servizio Autenticazione: chi sei e cosa puoi fare. Login, ruoli (Studente, Personale), emissione dei JWT.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/autenticazione: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/autenticazione");

api.MapGet("/", () => Results.Ok(new { servizio = "autenticazione", stato = "ok" }))
   .WithName("InfoAutenticazione");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
