using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartCampus.Gateway.IntegrationTests;

public sealed class ConfigurationTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
{
    [Fact]
    public void EveryRoute_DeclaresItsAccessPolicy()
    {
        // Una route senza AuthorizationPolicy resterebbe aperta senza che nessuno lo abbia deciso:
        // ogni nuova route deve dichiarare "Anonymous", "Default" (autenticato) o una policy per ruolo.
        var configuration = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        var routes = configuration.GetSection("ReverseProxy:Routes").GetChildren().ToList();

        Assert.NotEmpty(routes);
        Assert.All(routes, route =>
            Assert.False(string.IsNullOrWhiteSpace(route["AuthorizationPolicy"]), $"La route '{route.Key}' non ha AuthorizationPolicy."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("chiave-troppo-corta")]
    public async Task Startup_Fails_WithoutAValidSigningKey(string signingKey)
    {
        await using var factory = new GatewayFactory(fixture.Downstream.Address, signingKey);

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Jwt:SigningKey", exception.ToString());
    }
}
