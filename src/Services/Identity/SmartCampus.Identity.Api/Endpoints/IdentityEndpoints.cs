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

// Solo i dati che servono a scegliere un membro del gruppo: niente ruolo, sono tutti studenti.
public sealed record StudentSummary(Guid Id, string Username, string DisplayName);

public static class IdentityEndpoints
{
    // Messaggio volutamente generico: non rivela se è sbagliato il nome utente o la password.
    private const string InvalidCredentialsMessage = "Nome utente o password non corretti.";

    // Limiti della ricerca degli studenti: abbastanza per un autocomplete, non per scaricare l'elenco.
    public const int StudentSearchMinLength = 2;
    public const int StudentSearchMaxResults = 10;

    /// <summary>Registra login e profilo sul gruppo <c>/api/identity</c> creato in Program.cs (l'endpoint informativo "/" è lì).</summary>
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/me", GetProfileAsync).RequireAuthorization();
        group.MapGet("/students", SearchStudentsAsync).RequireAuthorization();

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

    // Ricerca degli studenti per il modulo di prenotazione di gruppo. Solo gli studenti creano prenotazioni
    // di gruppo, quindi solo loro possono cercare; chi cerca non compare nei risultati.
    private static async Task<IResult> SearchStudentsAsync(
        string? search,
        ClaimsPrincipal principal,
        IdentityContext db,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtClaimNames.Subject), out var userId))
        {
            return Results.Unauthorized();
        }

        if (!principal.IsInRole(nameof(UserRole.Student)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Ricerca non consentita",
                detail: "Solo gli studenti possono cercare altri studenti.");
        }

        var text = search?.Trim() ?? string.Empty;
        if (text.Length < StudentSearchMinLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["search"] = ["Scrivi almeno 2 caratteri per cercare."]
            });
        }

        // Contains diventa LIKE con escape di %, _ e [; la collation del database ignora maiuscole e minuscole.
        var students = await db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Student && user.Id != userId)
            .Where(user => user.DisplayName.Contains(text) || user.Username.Contains(text))
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Username)
            .Take(StudentSearchMaxResults)
            .Select(user => new StudentSummary(user.Id, user.Username, user.DisplayName))
            .ToListAsync(cancellationToken);

        return Results.Ok(students);
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Accesso non riuscito",
            detail: InvalidCredentialsMessage);
}
