using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.UnitTests;

public class SmartCampusJwtOptionsValidatorTests
{
    private static SmartCampusJwtOptions ValidOptions() => new()
    {
        Issuer = "SmartCampus.Identity",
        Audience = "SmartCampus",
        SigningKey = new string('k', SmartCampusJwtOptions.MinimumSigningKeyBytes),
        ExpirationMinutes = 60
    };

    private readonly SmartCampusJwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        Assert.True(_validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Validate_ExpirationOutOfRange_Fails(int minutes)
    {
        var options = ValidOptions();
        options.ExpirationMinutes = minutes;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_SigningKeyFailure_ProvidesConfigurationGuidance()
    {
        var options = ValidOptions();
        options.SigningKey = "short";

        var result = _validator.Validate(null, options);

        Assert.Contains(result.Failures!, message => message.Contains("Jwt__SigningKey"));
    }
}
