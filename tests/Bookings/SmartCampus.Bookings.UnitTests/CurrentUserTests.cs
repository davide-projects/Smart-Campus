using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.UnitTests;

// Lettura dei claim "sub" e "name" dal token inoltrato dal Gateway (#23).
public sealed class CurrentUserTests
{
    [Fact]
    public void TryGetUserName_ClaimPresent_ReturnsTheName()
    {
        var request = RequestWithPayload(new { sub = Guid.NewGuid().ToString(), name = "Mario Rossi" });

        Assert.Equal("Mario Rossi", CurrentUser.TryGetUserName(request));
    }

    [Fact]
    public void TryGetUserName_ClaimMissing_ReturnsNull()
    {
        var request = RequestWithPayload(new { sub = Guid.NewGuid().ToString() });

        Assert.Null(CurrentUser.TryGetUserName(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetUserName_ClaimEmpty_ReturnsNull(string name)
    {
        var request = RequestWithPayload(new { sub = Guid.NewGuid().ToString(), name });

        Assert.Null(CurrentUser.TryGetUserName(request));
    }

    [Fact]
    public void TryGetUserName_ClaimAtMaxLength_ReturnsTheName()
    {
        var name = new string('a', CurrentUser.MaxUserNameLength);
        var request = RequestWithPayload(new { sub = Guid.NewGuid().ToString(), name });

        Assert.Equal(name, CurrentUser.TryGetUserName(request));
    }

    [Fact]
    public void TryGetUserName_ClaimTooLong_ReturnsNull()
    {
        var request = RequestWithPayload(new
        {
            sub = Guid.NewGuid().ToString(),
            name = new string('a', CurrentUser.MaxUserNameLength + 1)
        });

        Assert.Null(CurrentUser.TryGetUserName(request));
    }

    [Fact]
    public void TryGetUserName_WithoutAuthorizationHeader_ReturnsNull()
    {
        Assert.Null(CurrentUser.TryGetUserName(new DefaultHttpContext().Request));
    }

    [Fact]
    public void TryGetUserId_ClaimPresent_ReturnsTheId()
    {
        var userId = Guid.NewGuid();
        var request = RequestWithPayload(new { sub = userId.ToString(), name = "Mario Rossi" });

        Assert.Equal(userId, CurrentUser.TryGetUserId(request));
    }

    [Fact]
    public void TryGetUserId_SubNotAGuid_ReturnsNull()
    {
        var request = RequestWithPayload(new { sub = "non-un-guid", name = "Mario Rossi" });

        Assert.Null(CurrentUser.TryGetUserId(request));
    }

    [Fact]
    public void TryGetUserId_SubNotAString_ReturnsNull()
    {
        var request = RequestWithPayload(new { sub = 42, name = "Mario Rossi" });

        Assert.Null(CurrentUser.TryGetUserId(request));
    }

    [Fact]
    public void TryGetUserId_PayloadNotAnObject_ReturnsNull()
    {
        var request = RequestWithPayload(new[] { "sub", "name" });

        Assert.Null(CurrentUser.TryGetUserId(request));
        Assert.Null(CurrentUser.TryGetUserName(request));
    }

    // Token di prova: la firma è finta perché CurrentUser non la verifica (lo fa il Gateway).
    private static HttpRequest RequestWithPayload(object payload)
    {
        static string Encode(object value) =>
            Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {Encode(new { alg = "none", typ = "JWT" })}.{Encode(payload)}.test";
        return context.Request;
    }
}
