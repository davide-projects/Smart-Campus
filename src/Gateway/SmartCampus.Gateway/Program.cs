using SmartCampus.ServiceDefaults;

// API Gateway: unico ingresso pubblico. Instrada /api/{servizio}/** verso il servizio corrispondente.
// Non contiene logica di dominio.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.MapReverseProxy();

app.Run();

public partial class Program;
