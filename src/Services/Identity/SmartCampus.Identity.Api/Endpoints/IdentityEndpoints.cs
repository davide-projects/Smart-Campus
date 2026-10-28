using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using SmartCampus.Identity.Api.Auth;
using SmartCampus.Identity.Api.Domain;
using SmartCampus.Identity.Api.Infrastructure;
using SmartCampus.ServiceDefaults;

namespace SmartCampus.Identity.Api.Endpoints;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record UserProfile(Guid Id, string Username, string DisplayName, string Role)
{
    public static UserProfile From(User user) => new(user.Id, user.Username, user.DisplayName, user.Role.ToString());
}

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, UserProfile User);

public static class IdentityEndpoints
{
    // Messaggio volutamente generico: non rivela se è sbagliato il nome utente o la password.
    private const string InvalidCredentialsMessage = "Nome utente o password non corretti.";

    /// <summary>Registra login e profilo sul gruppo <c>/api/identity</c> creato in Program.cs (l'endpoint informativo "/" è lì).</summary>
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", GetProfileAsync).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IdentityContext db,
        IPasswordService passwords,
        ITokenService tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Inserisci nome utente e password."]
            });
        }

        var username = request.Username.Trim();
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);

        if (user is null)
        {
            passwords.SimulateVerification(request.Password);
            return InvalidCredentials();
        }

        if (!passwords.Verify(user.PasswordHash, request.Password))
        {
            return InvalidCredentials();
        }

        var token = tokens.Create(user);
        return Results.Ok(new LoginResponse(token.Value, "Bearer", token.ExpiresAt, UserProfile.From(user)));
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        IdentityContext db,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtClaimNames.Subject), out var userId))
        {
            return Results.Unauthorized();
        }

        // Legge dal database, così il profilo riflette lo stato attuale dell'utente e non solo il contenuto del token.
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? Results.Unauthorized() : Results.Ok(UserProfile.From(user));
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Accesso non riuscito",
            detail: InvalidCredentialsMessage);
}
