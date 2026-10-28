using SmartCampus.Gateway.Auth;
using SmartCampus.ServiceDefaults;

// API Gateway: unico ingresso pubblico. Instrada /api/{servizio}/** verso il servizio corrispondente
// e valida il JWT emesso da Identity; Rooms lo rivalida per proteggere le route riservate allo Staff.
// Non contiene logica di dominio.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGatewayAuthentication(builder.Configuration);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();

// L'ordine conta: CORS prima dell'autenticazione, così le richieste preflight (OPTIONS, senza token)
// non vengono respinte con 401 e le risposte 401/403 portano comunque le intestazioni CORS.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

public partial class Program;
