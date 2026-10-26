using SmartCampus.ServiceDefaults;

// Servizio Identity: Autenticazione e ruoli (Studente, Personale); emette i JWT.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/identity: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/identity");

api.MapGet("/", () => Results.Ok(new { servizio = "identity", stato = "ok" }))
   .WithName("InfoIdentity");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
