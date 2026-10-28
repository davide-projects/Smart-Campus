using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using MassTransit;
using MassTransit.Testing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartCampus.Bookings.Api.Consumers;
using Testcontainers.MsSql;

namespace SmartCampus.Bookings.IntegrationTests;

// Prepara un SQL Server reale (Testcontainers) e il servizio Bookings con un bus in memoria
// al posto di RabbitMQ. Condivisa da tutti i test della classe che la usa.
public sealed class BookingsApiFixture : IAsyncLifetime
{
    // Chiavi finte, solo per i test: la prima è quella che Bookings usa per verificare i token, la seconda no.
    public const string JwtSigningKey = "bookings-integration-signing-key-with-at-least-32-bytes";
    public const string OtherSigningKey = "another-integration-signing-key-with-at-least-32-bytes";

    // Nome nel token dei client di prova: l'organizzatore di una prenotazione deve averne uno.
    public const string DefaultUserName = "Studente di prova";

    private const string JwtIssuer = "SmartCampus.Identity";
    private const string JwtAudience = "SmartCampus";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    // "Adesso" del servizio: i test lo fissano per controllare la divisione tra future e passate.
    public TestClock Clock { get; } = new();

    // Connection string del container, per i test che creano un database a parte (es. la migrazione).
    public string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                // Il servizio legge questa connection string: le migrazioni all'avvio
                // creano le tabelle nel database del container.
                builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());

                // Configurazione JWT di prova. AddInMemoryCollection viene dopo gli user-secrets di Development,
                // quindi i test usano sempre questa chiave anche se in locale ne hai impostata un'altra.
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:Issuer"] = JwtIssuer,
                        ["Jwt:Audience"] = JwtAudience,
                        ["Jwt:SigningKey"] = JwtSigningKey
                    }));

                // Sostituisce RabbitMQ con il bus in memoria di MassTransit: niente broker nei test.
                builder.ConfigureTestServices(services =>
                {
                    services.AddMassTransitTestHarness(bus => bus.AddConsumer<RoomEventsConsumer>());

                    // Orologio controllabile al posto di TimeProvider.System.
                    services.AddSingleton<TimeProvider>(Clock);
                });
            });

        // Accedere ai servizi avvia l'applicazione (migrazioni + bus).
        await Factory.Services.GetTestHarness().Start();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    // Client con un token valido per l'utente indicato, come se arrivasse dal Gateway.
    public HttpClient CreateClientFor(Guid userId, string? name = DefaultUserName, string role = "Student")
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(userId.ToString(), name, role));

        return client;
    }

    // Crea un JWT firmato come quello di Identity. Con subject = null il claim "sub" manca, con name = null manca "name".
    // Una durata negativa produce un token già scaduto (oltre la tolleranza di 30 secondi).
    public static string CreateToken(
        string? subject,
        string? name = DefaultUserName,
        string role = "Student",
        string signingKey = JwtSigningKey,
        TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var remaining = lifetime ?? TimeSpan.FromMinutes(30);

        var claims = new List<Claim> { new("role", role) };

        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            NotBefore = remaining < TimeSpan.Zero ? now - TimeSpan.FromHours(2) : now,
            Expires = now + remaining,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
