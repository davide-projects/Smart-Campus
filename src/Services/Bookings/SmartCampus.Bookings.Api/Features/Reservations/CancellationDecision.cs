namespace SmartCampus.Bookings.Api.Features.Reservations;

// Esito della regola di cancellazione: si può cancellare, è già cancellata (nessuna modifica)
// oppure la richiesta va rifiutata con il motivo indicato.
public sealed record CancellationDecision
{
    public static readonly CancellationDecision Allowed = new(CancellationOutcome.Allowed, null);
    public static readonly CancellationDecision AlreadyCancelled = new(CancellationOutcome.AlreadyCancelled, null);

    private CancellationDecision(CancellationOutcome outcome, ReservationRejection? rejection)
    {
        Outcome = outcome;
        Rejection = rejection;
    }

    public CancellationOutcome Outcome { get; }

    // Valorizzato solo quando Outcome è Rejected.
    public ReservationRejection? Rejection { get; }

    public static CancellationDecision Reject(ReservationRejection rejection) =>
        new(CancellationOutcome.Rejected, rejection);
}

public enum CancellationOutcome { Allowed, AlreadyCancelled, Rejected }
