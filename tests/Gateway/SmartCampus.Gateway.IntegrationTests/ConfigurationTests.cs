using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartCampus.Gateway.Auth;

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
    public void Options_AreRejected_WithoutAValidSigningKey(string signingKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SmartCampus.Identity",
                ["Jwt:Audience"] = "SmartCampus",
                ["Jwt:SigningKey"] = signingKey
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGatewayAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<GatewayJwtOptions>>().Value);

        Assert.Contains("Jwt:SigningKey", exception.Message);
    }

    [Fact]
    public void Options_AreAccepted_WithAValidSigningKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SmartCampus.Identity",
                ["Jwt:Audience"] = "SmartCampus",
                ["Jwt:SigningKey"] = TestTokens.SigningKey
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGatewayAuthentication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<GatewayJwtOptions>>().Value;

        Assert.Equal(TestTokens.SigningKey, options.SigningKey);
    }
}
