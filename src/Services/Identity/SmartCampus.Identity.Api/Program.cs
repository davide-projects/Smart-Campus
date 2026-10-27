using Microsoft.EntityFrameworkCore;

using SmartCampus.Identity.Api.Auth;
using SmartCampus.Identity.Api.Endpoints;
using SmartCampus.Identity.Api.Infrastructure;
using SmartCampus.ServiceDefaults;

// Servizio Identity: chi sei e cosa puoi fare. Login, ruoli (Student, Staff), emissione dei JWT.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// La stringa di connessione è letta in modo differito (user-secrets o variabile d'ambiente).
builder.Services.AddDbContext<IdentityContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
        ?? throw new InvalidOperationException(
            "Stringa di connessione 'Database' mancante: impostala negli user-secrets (vedi README).");

    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
});

builder.Services.AddSmartCampusJwt(builder.Configuration);
builder.Services.AddScoped<UserSeeder>();
builder.Services.AddHostedService<DatabaseInitializer>();

// Il controllo "ready" dipende dal database (i controlli di base arrivano da ServiceDefaults).
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityContext>("database", tags: ["ready"]);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Tutte le route del servizio stanno sotto /api/identity: il gateway le inoltra senza riscriverle.
var api = app.MapGroup("/api/identity");

api.MapGet("/", () => Results.Ok(new { service = "identity", status = "ok" }))
   .WithName("InfoIdentity");

api.MapIdentityEndpoints();

app.Run();

// Esposto per WebApplicationFactory nei test di integrazione.
public partial class Program;