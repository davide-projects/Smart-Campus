namespace SmartCampus.Bookings.Api.Features.Reservations;

// Motivo di rifiuto: diventa una risposta ProblemDetails con il codice HTTP previsto dall'handoff (6.2).
public sealed record ReservationRejection(
    int StatusCode,
    string Title,
    string Detail);
