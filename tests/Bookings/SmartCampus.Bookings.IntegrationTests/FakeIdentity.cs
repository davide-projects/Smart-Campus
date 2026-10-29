using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace SmartCampus.Bookings.IntegrationTests;

// Identity finta per i test di Bookings: risponde alla verifica interna dei membri a seconda del caso
// e registra le richieste ricevute. Condivisa dalla fixture; i test che la usano la riportano allo stato
// iniziale con Reset. Thread-safe, perché i test di concorrenza la chiamano in parallelo.
public sealed class FakeIdentity
{
    public enum Behavior
    {
        // Risponde 200: studenti tutti gli id, tranne quelli segnati con MarkNotStudent.
        Students,
        // Non risponde finché la richiesta non viene annullata (il timeout di 3 s di IdentityClient).
        NoResponse,
        ServerError,
        InvalidJson,
        Unreachable
    }

    public sealed record ReceivedRequest(HttpMethod Method, string Path, string? Authorization, IReadOnlyList<Guid> UserIds);

    private readonly ConcurrentDictionary<Guid, byte> _notStudents = new();
    private readonly ConcurrentQueue<ReceivedRequest> _requests = new();

    public Behavior Mode { get; set; } = Behavior.Students;

    public IReadOnlyList<ReceivedRequest> Requests => [.. _requests];

    public void MarkNotStudent(Guid userId) => _notStudents[userId] = 0;

    public void Reset()
    {
        Mode = Behavior.Students;
        _notStudents.Clear();
        _requests.Clear();
    }

    internal async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadFromJsonAsync<CheckRequest>(cancellationToken);
        var userIds = body?.UserIds ?? [];

        _requests.Enqueue(new ReceivedRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null,
            userIds));

        switch (Mode)
        {
            case Behavior.NoResponse:
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Non raggiungibile: il ritardo termina solo con l'annullamento.");
            case Behavior.ServerError:
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            case Behavior.InvalidJson:
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ non è json", Encoding.UTF8, "application/json")
                };
            case Behavior.Unreachable:
                throw new HttpRequestException("Connessione rifiutata (Identity finta).");
        }

        var distinct = userIds.Distinct().ToList();
        var response = new
        {
            students = distinct
                .Where(id => !_notStudents.ContainsKey(id))
                .Select(id => new { id, displayName = "Studente di prova" }),
            notStudents = distinct.Where(_notStudents.ContainsKey)
        };

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
    }

    private sealed record CheckRequest(IReadOnlyList<Guid>? UserIds);
}

// Handler primario di IdentityClient nei test: inoltra ogni richiesta alla FakeIdentity della fixture.
// Uno nuovo per ogni handler creato da IHttpClientFactory, così il suo smaltimento non tocca lo stato condiviso.
public sealed class FakeIdentityHandler(FakeIdentity identity) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        identity.HandleAsync(request, cancellationToken);
}
