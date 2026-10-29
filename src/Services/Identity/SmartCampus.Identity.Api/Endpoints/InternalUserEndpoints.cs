using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartCampus.Identity.Api.Domain;
using SmartCampus.Identity.Api.Infrastructure;

namespace SmartCampus.Identity.Api.Endpoints;

public sealed record StudentsCheckRequest(IReadOnlyList<Guid>? UserIds);

public sealed record CheckedStudent(Guid Id, string DisplayName);

public sealed record StudentsCheckResponse(IReadOnlyList<CheckedStudent> Students, IReadOnlyList<Guid> NotStudents);

public static class InternalUserEndpoints
{
    private const string MissingUserIdsMessage = "Indica almeno un utente da verificare.";

    /// <summary>
    /// Registra gli endpoint interni sul gruppo <c>/internal</c> creato in Program.cs. Il gruppo è fuori da
    /// <c>/api</c>, quindi il Gateway non ha una route che lo raggiunga: lo chiamano solo gli altri servizi.
    /// </summary>
    public static RouteGroupBuilder MapInternalUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/students/check", CheckStudentsAsync)
             .Accepts<StudentsCheckRequest>("application/json")
             .Produces<StudentsCheckResponse>()
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    // Indica quali id corrispondono a studenti registrati (INV-9). Staff e id inesistenti finiscono entrambi
    // in notStudents, così chi chiama non può usare la risposta per scoprire chi è registrato.
    private static async Task<IResult> CheckStudentsAsync(
        HttpRequest httpRequest,
        IdentityContext db,
        CancellationToken cancellationToken)
    {
        // Il corpo è letto qui e non dal binding dei minimal API: così corpo mancante, JSON non valido e
        // valori non GUID ricevono lo stesso ValidationProblem in ogni ambiente (in Development il binding
        // lancerebbe un'eccezione invece di rispondere 400).
        var request = await ReadRequestAsync(httpRequest, cancellationToken);
        if (request?.UserIds is not { Count: > 0 } userIds)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userIds"] = [MissingUserIdsMessage]
            });
        }

        var distinctIds = userIds.Distinct().ToList();

        // Una sola query sugli id distinti.
        var students = await db.Users.AsNoTracking()
            .Where(user => distinctIds.Contains(user.Id) && user.Role == UserRole.Student)
            .Select(user => new CheckedStudent(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(student => student.Id).ToHashSet();
        var notStudents = distinctIds.Where(id => !studentIds.Contains(id)).ToList();

        return Results.Ok(new StudentsCheckResponse(students, notStudents));
    }

    private static async Task<StudentsCheckRequest?> ReadRequestAsync(
        HttpRequest httpRequest, CancellationToken cancellationToken)
    {
        if (!httpRequest.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await httpRequest.ReadFromJsonAsync<StudentsCheckRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
