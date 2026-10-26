using SmartCampus.ServiceDefaults;

// Servizio Catalogo: Anagrafica di edifici, spazi, attrezzature e orari di apertura.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/catalogo: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/catalogo");

api.MapGet("/", () => Results.Ok(new { servizio = "catalogo", stato = "ok" }))
   .WithName("InfoCatalogo");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
