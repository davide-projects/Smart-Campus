using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SmartCampus.Bookings.Api.Features.Reservations;

// Esito della verifica dei membri presso Identity (INV-9).
public enum StudentsCheckOutcome
{
    AllStudents,
    NotAllStudents,
    Unavailable
}

// Configurazione della chiamata a Identity: sezione "Services:Identity".
public sealed class IdentityServiceOptions
{
    public const string SectionName = "Services:Identity";
    public const string BaseAddressKey = "Services:Identity:BaseAddress";

    public string? BaseAddress { get; set; }
}

// All'avvio l'indirizzo di Identity deve essere un URI assoluto http o https (su Linux "/internal" sarebbe
// un URI assoluto file://), come si fa per Jwt:SigningKey:
// meglio fermare il servizio subito che scoprirlo alla prima prenotazione di gruppo.
public sealed class IdentityServiceOptionsValidator : IValidateOptions<IdentityServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityServiceOptions options) =>
        Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var address)
        && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{IdentityServiceOptions.BaseAddressKey} è obbligatorio e deve essere un indirizzo assoluto http o https (es. http://localhost:5101).");
}

// Client HTTP tipizzato verso la verifica interna di Identity (POST /internal/users/students/check).
// È la prima chiamata sincrona tra servizi: breve (timeout 3 s), senza retry, con il token di chi prenota
// e senza credenziali proprie. Ogni problema tecnico diventa "non disponibile": il chiamante non vede eccezioni.
public sealed class IdentityClient(HttpClient httpClient, ILogger<IdentityClient> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const string CheckPath = "/internal/users/students/check";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record CheckRequest(IReadOnlyCollection<Guid> UserIds);

    private sealed record CheckedStudent(Guid Id, string? DisplayName);

    private sealed record CheckResponse(IReadOnlyList<CheckedStudent>? Students, IReadOnlyList<Guid>? NotStudents);

    public async Task<StudentsCheckOutcome> CheckStudentsAsync(
        IReadOnlyCollection<Guid> userIds,
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, CheckPath)
        {
            Content = JsonContent.Create(new CheckRequest(userIds), options: Json)
        };

        // Stessa intestazione ricevuta da chi prenota. Non va mai nei log.
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Unavailable($"stato HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadFromJsonAsync<CheckResponse>(Json, cancellationToken);

            if (body?.Students is null || body.NotStudents is null)
            {
                return Unavailable("risposta senza students o notStudents");
            }

            // Un membro è accettato solo se Identity lo conferma come studente.
            var students = body.Students.Select(student => student.Id).ToHashSet();

            return body.NotStudents.Count == 0 && userIds.All(students.Contains)
                ? StudentsCheckOutcome.AllStudents
                : StudentsCheckOutcome.NotAllStudents;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Annullamento non chiesto da chi prenota: è scaduto il timeout di HttpClient.
            return Unavailable("timeout");
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception.GetType().Name);
        }
        catch (JsonException exception)
        {
            return Unavailable(exception.GetType().Name);
        }
        catch (NotSupportedException exception)
        {
            // Contenuto con un tipo diverso da JSON.
            return Unavailable(exception.GetType().Name);
        }
    }

    // Solo il motivo tecnico: niente id, nomi o token.
    private StudentsCheckOutcome Unavailable(string reason)
    {
        logger.LogWarning("Verifica dei membri presso Identity non disponibile: {Reason}.", reason);
        return StudentsCheckOutcome.Unavailable;
    }
}
