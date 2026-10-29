namespace SmartCampus.Bookings.Api.Features.Reservations;

// Codici di motivo dei rifiuti: stabili e leggibili da un programma, così il frontend sceglie il messaggio
// senza dover leggere il testo. Compaiono nella risposta ProblemDetails come proprietà "code".
public static class RejectionCodes
{
    // Prenotazione: conflitti (409).
    public const string RoomOccupied = "room-occupied";
    public const string PersonOccupied = "person-occupied";
    public const string BookingInProgress = "booking-in-progress";

    // Prenotazione di gruppo (US-05): conflitti (409).
    public const string MembersOccupied = "members-occupied";
    public const string MemberBookingInProgress = "member-booking-in-progress";

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

    // Prenotazione di gruppo (US-05): regole sui membri e sulla capienza (422).
    public const string TooManyMembers = "too-many-members";
    public const string OrganizerAsMember = "organizer-as-member";
    public const string DuplicateMembers = "duplicate-members";
    public const string MembersNotStudents = "members-not-students";
    public const string CapacityExceeded = "capacity-exceeded";

    // Prenotazione di gruppo (US-05): permesso (403) e verifica dei membri non disponibile (503).
    public const string GroupBookingForbidden = "group-booking-forbidden";
    public const string MembersCheckUnavailable = "members-check-unavailable";

    // Cancellazione.
    public const string CancellationForbidden = "cancellation-forbidden";
    public const string CancellationAlreadyUsed = "cancellation-already-used";
    public const string CancellationTooLate = "cancellation-too-late";

    // Check-in.
    public const string CheckInForbidden = "check-in-forbidden";
    public const string CheckInCancelled = "check-in-cancelled";
    public const string CheckInNoShow = "check-in-no-show";
    public const string CheckInAlreadyRecorded = "check-in-already-recorded";
    public const string CheckInOutsideWindow = "check-in-outside-window";
}
