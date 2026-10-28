using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using SmartCampus.Identity.Api.Auth;
using SmartCampus.Identity.Api.Domain;
using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.UnitTests;

public class JwtTokenServiceTests
{
    private const string SigningKey = "chiave-di-test-di-almeno-32-byte-0123456789";

    private static readonly DateTimeOffset Now = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "SmartCampus.Identity",
        Audience = "SmartCampus",
        SigningKey = SigningKey,
        ExpirationMinutes = 60
    };

    private JwtTokenService CreateService() =>
        new(Options.Create(_options), new FixedTimeProvider(Now));

    [Theory]
    [InlineData(UserRole.Student, "Student")]
    [InlineData(UserRole.Staff, "Staff")]
    public void Create_IncludesIdentityAndRoleClaims(UserRole role, string expectedRole)
    {
        var id = Guid.NewGuid();
        var user = new User(id, "student1", "Mario Rossi", "hash", role);

        var jwt = new JsonWebToken(CreateService().Create(user).Value);

        Assert.Equal(id.ToString(), jwt.GetClaim(JwtClaimNames.Subject).Value);
        Assert.Equal("student1", jwt.GetClaim(JwtClaimNames.Username).Value);
        Assert.Equal("Mario Rossi", jwt.GetClaim(JwtClaimNames.Name).Value);
        Assert.Equal(expectedRole, jwt.GetClaim(JwtClaimNames.Role).Value);
        Assert.Equal("SmartCampus.Identity", jwt.Issuer);
        Assert.Contains("SmartCampus", jwt.Audiences);
    }

    [Fact]
    public void Create_ExpiresAfterConfiguredMinutes()
    {
        var user = new User(Guid.NewGuid(), "student1", "Mario Rossi", "hash", UserRole.Student);

        var token = CreateService().Create(user);
        var jwt = new JsonWebToken(token.Value);

        Assert.Equal(Now.AddMinutes(60), token.ExpiresAt);
        Assert.Equal(Now.AddMinutes(60).UtcDateTime, jwt.ValidTo);
    }

    [Fact]
    public void Create_DoesNotLeakPasswordHash()
    {
        var user = new User(Guid.NewGuid(), "student1", "Mario Rossi", "hash-segreto", UserRole.Student);

        var token = CreateService().Create(user).Value;
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token.Split('.')[1]));

        Assert.DoesNotContain("hash-segreto", payload);
    }

    [Fact]
    public async Task Create_SignatureIsValidOnlyWithTheConfiguredKey()
    {
        var user = new User(Guid.NewGuid(), "student1", "Mario Rossi", "hash", UserRole.Student);
        var token = CreateService().Create(user).Value;
        var handler = new JsonWebTokenHandler();

        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = "SmartCampus.Identity",
            ValidAudience = "SmartCampus",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            // La data è fissata dal FakeTimeProvider: qui si verifica solo la firma.
            ValidateLifetime = false
        };

        var withRightKey = await handler.ValidateTokenAsync(token, Parameters(SigningKey));
        var withOtherKey = await handler.ValidateTokenAsync(token, Parameters("un-altra-chiave-di-test-di-32-byte-9876543210"));

        Assert.True(withRightKey.IsValid);
        Assert.False(withOtherKey.IsValid);
    }

    // Orologio fisso: rende prevedibile la scadenza del token senza dipendenze aggiuntive.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
