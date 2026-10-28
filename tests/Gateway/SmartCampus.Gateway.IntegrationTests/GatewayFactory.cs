using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>Avvia il Gateway reale con chiave JWT di prova e tutti i servizi puntati al servizio a valle finto.</summary>
public sealed class GatewayFactory(string downstreamAddress) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // AddInMemoryCollection viene dopo appsettings.json, quindi sovrascrive gli indirizzi dei cluster.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestTokens.SigningKey,
                ["ReverseProxy:Clusters:identity:Destinations:d1:Address"] = downstreamAddress,
                ["ReverseProxy:Clusters:rooms:Destinations:d1:Address"] = downstreamAddress,
                ["ReverseProxy:Clusters:bookings:Destinations:d1:Address"] = downstreamAddress
            }));
    }
}

/// <summary>Condivide un solo Gateway e un solo servizio finto fra tutti i test di una classe.</summary>
public sealed class GatewayFixture : IAsyncLifetime
{
    public FakeDownstream Downstream { get; private set; } = null!;

    public GatewayFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Downstream = await FakeDownstream.StartAsync();
        Factory = new GatewayFactory(Downstream.Address);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Downstream.DisposeAsync();
    }
}

public static class HttpClientExtensions
{
    /// <summary>Invia una richiesta, con il token come <c>Authorization: Bearer</c> se presente.</summary>
    public static Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string url, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        }

        return client.SendAsync(request);
    }
}
