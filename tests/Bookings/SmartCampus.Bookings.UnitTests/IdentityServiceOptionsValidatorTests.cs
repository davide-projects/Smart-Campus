using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.UnitTests;

// All'avvio Services:Identity:BaseAddress deve essere un URI assoluto, altrimenti il servizio non parte.
public sealed class IdentityServiceOptionsValidatorTests
{
    private readonly IdentityServiceOptionsValidator _validator = new();

    [Theory]
    [InlineData("http://localhost:5101")]
    [InlineData("http://identity:8080")]
    public void AbsoluteAddress_IsValid(string address)
    {
        var result = _validator.Validate(null, new IdentityServiceOptions { BaseAddress = address });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("identity:8080/api")]
    [InlineData("/internal")]
    public void MissingOrRelativeAddress_FailsNamingTheKey(string? address)
    {
        var result = _validator.Validate(null, new IdentityServiceOptions { BaseAddress = address });

        Assert.True(result.Failed);
        Assert.Contains("Services:Identity:BaseAddress", result.FailureMessage);
    }
}
