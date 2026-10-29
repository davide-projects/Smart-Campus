namespace SmartCampus.Bookings.Api.Features.Reservations;

// Motivo di rifiuto: diventa una risposta ProblemDetails con il codice HTTP previsto dall'handoff (6.2).
// Code è il codice di motivo (RejectionCodes); Limits riporta i limiti da rispettare, quando ce ne sono
// (per esempio gli orari di apertura o la durata massima).
public sealed record ReservationRejection(
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    IReadOnlyDictionary<string, object?>? Limits = null);
