using SmartCampus.ServiceDefaults;

// Servizio Attendance: lo spazio prenotato è stato usato davvero? Check-in, mancate presentazioni e report per il personale.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/attendance: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/attendance");

api.MapGet("/", () => Results.Ok(new { service = "attendance", status = "ok" }))
   .WithName("InfoAttendance");

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;
