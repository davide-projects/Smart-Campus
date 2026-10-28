using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartCampus.ServiceDefaults;

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
        using var provider = BuildProvider(signingKey);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SmartCampusJwtOptions>>().Value);

        Assert.Contains("Jwt:SigningKey", exception.Message);
        Assert.Contains("Jwt__SigningKey", exception.Message);
    }

    [Fact]
    public void Options_AreAccepted_WithAValidSigningKey()
    {
        using var provider = BuildProvider(TestTokens.SigningKey);

        var options = provider.GetRequiredService<IOptions<SmartCampusJwtOptions>>().Value;

        Assert.Equal(TestTokens.SigningKey, options.SigningKey);
    }

    private static ServiceProvider BuildProvider(string signingKey)
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

        return services.BuildServiceProvider();
    }
}