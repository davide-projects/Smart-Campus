using SmartCampus.Identity.Api.Auth;

namespace SmartCampus.Identity.UnitTests;

public class JwtOptionsValidatorTests
{
    private static JwtOptions ValidOptions() => new()
    {
        Issuer = "SmartCampus.Identity",
        Audience = "SmartCampus",
        SigningKey = new string('k', JwtOptions.MinimumSigningKeyBytes),
        ExpirationMinutes = 60
    };

    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        Assert.True(_validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Fact]
    public void Validate_MissingSigningKey_Fails()
    {
        var options = ValidOptions();
        options.SigningKey = string.Empty;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, message => message.Contains("Jwt:SigningKey"));
    }

    [Fact]
    public void Validate_ShortSigningKey_Fails()
    {
        var options = ValidOptions();
        options.SigningKey = new string('k', JwtOptions.MinimumSigningKeyBytes - 1);

        Assert.True(_validator.Validate(null, options).Failed);
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
    public void Validate_MissingIssuerAndAudience_ReportsBoth()
    {
        var options = ValidOptions();
        options.Issuer = " ";
        options.Audience = string.Empty;

        var result = _validator.Validate(null, options);

        Assert.Equal(2, result.Failures!.Count());
    }
}
