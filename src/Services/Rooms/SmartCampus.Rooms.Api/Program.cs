using SmartCampus.ServiceDefaults;

// Servizio Rooms: quali spazi esistono e come sono fatti. Anagrafica di edifici, spazi, attrezzature e orari di apertura.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/rooms: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/rooms");

api.MapGet("/", () => Results.Ok(new { service = "rooms", status = "ok" }))
   .WithName("InfoRooms");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
