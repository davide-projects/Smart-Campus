namespace SmartCampus.Bookings.Api.Features.Reservations;

// Codici di motivo dei rifiuti: stabili e leggibili da un programma, così il frontend sceglie il messaggio
// senza dover leggere il testo. Compaiono nella risposta ProblemDetails come proprietà "code".
// "members-occupied" (un membro del gruppo è già impegnato) e "capacity-exceeded" (troppi partecipanti)
// arrivano con le prenotazioni di gruppo (US-05).
public static class RejectionCodes
{
    // Prenotazione: conflitti (409).
    public const string RoomOccupied = "room-occupied";
    public const string PersonOccupied = "person-occupied";
    public const string BookingInProgress = "booking-in-progress";

    // Prenotazione: regole sulla richiesta e sullo spazio (422).
    public const string InvalidTimeRange = "invalid-time-range";
    public const string InThePast = "in-the-past";
    public const string SlotBoundary = "slot-boundary";
    public const string PurposeTooLong = "purpose-too-long";
    public const string DurationExceeded = "duration-exceeded";
    public const string TooFarAhead = "too-far-ahead";
    public const string RoomNotBookable = "room-not-bookable";
    public const string DifferentDays = "different-days";
    public const string RoomClosed = "room-closed";
    public const string OutsideOpeningHours = "outside-opening-hours";

    // Cancellazione.
    public const string CancellationForbidden = "cancellation-forbidden";
    public const string CancellationTooLate = "cancellation-too-late";
}
