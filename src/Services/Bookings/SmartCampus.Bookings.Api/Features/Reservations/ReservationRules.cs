using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;

namespace SmartCampus.Bookings.Api.Features.Reservations;

// Regole di una prenotazione come funzioni pure: ricevono tutto come parametro (anche "adesso")
// e restituiscono null se la richiesta va bene, altrimenti il motivo del rifiuto.
public static class ReservationRules
{
    // Assunzione A1: si prenota a blocchi di 30 minuti.
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    // Assunzione A2: al massimo 4 ore, al massimo 14 giorni prima.
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(4);
    public static readonly TimeSpan MaxAdvance = TimeSpan.FromDays(14);

    public const int MaxPurposeLength = 200;

    // Assunzione A6: il check-in si fa da 15 minuti prima a 15 minuti dopo l'inizio, estremi inclusi.
    public static readonly TimeSpan CheckInBefore = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan CheckInAfter = TimeSpan.FromMinutes(15);

    // Gruppo 1: regole sulla sola richiesta. Si controllano prima della transazione:
    // se la richiesta è sbagliata non serve prendere nessun lock.
    public static ReservationRejection? ValidateRequest(CreateReservationRequest request, DateTimeOffset now)
    {
        // INV-2
        if (request.End <= request.Start)
        {
            return Unprocessable(RejectionCodes.InvalidTimeRange, "Orario non valido", "L'orario di fine deve essere successivo a quello di inizio.");
        }

        // INV-6
        if (request.Start < now)
        {
            return Unprocessable(RejectionCodes.InThePast, "Orario nel passato", "Non è possibile prenotare nel passato.");
        }

        // A1: inizio e fine all'ora esatta o alla mezz'ora, come le fasce della disponibilità.
        if (!IsOnSlotBoundary(request.Start) || !IsOnSlotBoundary(request.End))
        {
            return Unprocessable(
                RejectionCodes.SlotBoundary,
                "Orario non valido",
                "Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora.",
                Limits(("slotMinutes", (int)SlotLength.TotalMinutes)));
        }

        // Il motivo è facoltativo, ma la colonna nel database ha al massimo 200 caratteri.
        if (request.Purpose?.Length > MaxPurposeLength)
        {
            return Unprocessable(
                RejectionCodes.PurposeTooLong,
                "Motivo troppo lungo",
                "Il motivo può contenere al massimo 200 caratteri.",
                Limits(("maxLength", MaxPurposeLength)));
        }

        // A2
        if (request.End - request.Start > MaxDuration)
        {
            return Unprocessable(
                RejectionCodes.DurationExceeded,
                "Durata eccessiva",
                "Una prenotazione può durare al massimo 4 ore.",
                Limits(("maxDurationMinutes", (int)MaxDuration.TotalMinutes)));
        }

        // A2
        if (request.Start > now + MaxAdvance)
        {
            return Unprocessable(
                RejectionCodes.TooFarAhead,
                "Troppo in anticipo",
                "Si può prenotare al massimo 14 giorni prima.",
                Limits(("maxAdvanceDays", (int)MaxAdvance.TotalDays)));
        }

        return null;
    }

    // Gruppo 2: regole sullo spazio. Si controllano dentro la transazione, dopo il lock sulla riga
    // dello spazio, così nessuno può cambiarlo mentre decidiamo.
    public static ReservationRejection? ValidateAgainstRoom(
        CreateReservationRequest request,
        BookableRoom room,
        TimeZoneInfo campusTimeZone)
    {
        // INV-4
        if (!room.IsActive || !room.IsBookable)
        {
            return Unprocessable(RejectionCodes.RoomNotBookable, "Spazio non prenotabile", "Questo spazio non è prenotabile.");
        }

        // INV-3: gli orari di apertura sono in ora locale del campus.
        var localStart = TimeZoneInfo.ConvertTime(request.Start, campusTimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(request.End, campusTimeZone);

        if (localStart.Date != localEnd.Date)
        {
            return Unprocessable(RejectionCodes.DifferentDays, "Orario non valido", "La prenotazione deve iniziare e finire nello stesso giorno.");
        }

        var openingHours = room.OpeningHours.SingleOrDefault(hours => hours.DayOfWeek == localStart.DayOfWeek);

        if (openingHours is null)
        {
            return Unprocessable(RejectionCodes.RoomClosed, "Fuori orario", "Lo spazio è chiuso in questo giorno.");
        }

        var startTime = TimeOnly.FromDateTime(localStart.DateTime);
        var endTime = TimeOnly.FromDateTime(localEnd.DateTime);

        if (startTime < openingHours.OpensAt || endTime > openingHours.ClosesAt)
        {
            return Unprocessable(
                RejectionCodes.OutsideOpeningHours,
                "Fuori orario",
                $"Lo spazio è aperto dalle {openingHours.OpensAt:HH\\:mm} alle {openingHours.ClosesAt:HH\\:mm}.",
                Limits(
                    ("opensAt", openingHours.OpensAt.ToString("HH\\:mm")),
                    ("closesAt", openingHours.ClosesAt.ToString("HH\\:mm"))));
        }

        return null;
    }

    // INV-1: conflitto con una prenotazione confermata dello stesso spazio.
    // Il testo non cambia (il frontend lo riconosce); le fasce libere dello stesso giorno stanno in FreeSlots.
    public static ReservationRejection RoomConflict(IReadOnlyList<FreeRange> freeSlots) =>
        new(
            StatusCodes.Status409Conflict,
            "Spazio già prenotato",
            "Lo spazio è già prenotato in questa fascia.",
            RejectionCodes.RoomOccupied,
            FreeSlots: freeSlots);

    // INV-8: l'organizzatore ha già una prenotazione confermata sovrapposta, anche in un altro spazio.
    public static ReservationRejection OrganizerConflict() =>
        new(StatusCodes.Status409Conflict, "Hai già una prenotazione", "Hai già una prenotazione in questa fascia oraria.", RejectionCodes.PersonOccupied);

    public static ReservationRejection UserLockTimeout() =>
        new(StatusCodes.Status409Conflict, "Prenotazione in corso", "Hai un'altra prenotazione in corso, riprova tra qualche secondo.", RejectionCodes.BookingInProgress);

    // Cancellazione (US-08): l'ordine dei controlli è quello del design della change 25-cancella-prenotazione, decisione 2,
    // con il controllo sulla prenotazione usata della change 45-check-in, decisione 7.
    // 1. chi può cancellare: prima di tutto, così un estraneo riceve sempre 403 e non scopre lo stato della prenotazione;
    // 2. già cancellata: nessuna modifica (idempotenza), anche se nel frattempo l'inizio è passato;
    // 3. con check-in: una prenotazione usata non si cancella più, nemmeno prima dell'inizio;
    // 4. limite di tempo (assunzione A5): si cancella solo prima dell'inizio, Staff compreso.
    public static CancellationDecision ValidateCancellation(
        Reservation reservation,
        Guid userId,
        bool isStaff,
        bool hasCheckIn,
        DateTimeOffset now)
    {
        if (reservation.OrganizerId != userId && !isStaff)
        {
            return CancellationDecision.Reject(new ReservationRejection(
                StatusCodes.Status403Forbidden,
                "Cancellazione non consentita",
                "Non puoi cancellare questa prenotazione.",
                RejectionCodes.CancellationForbidden));
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            return CancellationDecision.AlreadyCancelled;
        }

        if (hasCheckIn)
        {
            return CancellationDecision.Reject(Unprocessable(
                RejectionCodes.CancellationAlreadyUsed,
                "Cancellazione non consentita",
                "Non è possibile cancellare una prenotazione già usata."));
        }

        if (reservation.Start <= now)
        {
            return CancellationDecision.Reject(Unprocessable(
                RejectionCodes.CancellationTooLate,
                "Cancellazione non consentita",
                "Non è possibile cancellare una prenotazione già iniziata o passata."));
        }

        return CancellationDecision.Allowed;
    }

    // Check-in (US-14): l'ordine dei controlli è quello del design della change 45-check-in, decisione 3.
    // 1. chi può fare il check-in (organizzatore, membro o Staff): prima di tutto, come per la cancellazione;
    // 2-4. stati definitivi (cancellata, no-show, già registrato): spiegano il rifiuto meglio della finestra;
    // 5. finestra A6, solo per organizzatore e membri: lo Staff non ha limiti di finestra.
    // existingRecord è il tipo dell'eventuale record di presenza già salvato (al massimo uno per prenotazione).
    // Restituisce null se il check-in è consentito.
    public static ReservationRejection? ValidateCheckIn(
        Reservation reservation,
        Guid userId,
        bool isMember,
        bool isStaff,
        AttendanceType? existingRecord,
        DateTimeOffset now)
    {
        if (reservation.OrganizerId != userId && !isMember && !isStaff)
        {
            return new ReservationRejection(
                StatusCodes.Status403Forbidden,
                CheckInRejectedTitle,
                "Non puoi fare il check-in di questa prenotazione.",
                RejectionCodes.CheckInForbidden);
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            return Unprocessable(
                RejectionCodes.CheckInCancelled,
                CheckInRejectedTitle,
                "Non è possibile fare il check-in di una prenotazione cancellata.");
        }

        if (existingRecord is { } recorded)
        {
            return ExistingAttendance(recorded);
        }

        if (!isStaff && (now < reservation.Start - CheckInBefore || now > reservation.Start + CheckInAfter))
        {
            return Unprocessable(
                RejectionCodes.CheckInOutsideWindow,
                CheckInRejectedTitle,
                "Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.");
        }

        return null;
    }

    // Rifiuto del check-in per un record di presenza già salvato: usato anche dopo la violazione
    // dell'indice unico, quando la rilettura trova il record scritto da un'altra richiesta.
    public static ReservationRejection ExistingAttendance(AttendanceType recorded) =>
        recorded == AttendanceType.NoShow
            ? Unprocessable(
                RejectionCodes.CheckInNoShow,
                CheckInRejectedTitle,
                "La prenotazione è già stata segnata come non usata.")
            : Unprocessable(
                RejectionCodes.CheckInAlreadyRecorded,
                CheckInRejectedTitle,
                "Il check-in di questa prenotazione è già stato registrato.");

    private const string CheckInRejectedTitle = "Check-in non consentito";

    private static bool IsOnSlotBoundary(DateTimeOffset value) =>
        value.TimeOfDay.Ticks % SlotLength.Ticks == 0;

    private static ReservationRejection Unprocessable(
        string code,
        string title,
        string detail,
        IReadOnlyDictionary<string, object?>? limits = null) =>
        new(StatusCodes.Status422UnprocessableEntity, title, detail, code, limits);

    // I limiti da rispettare, come coppie nome-valore: finiscono nella proprietà "limits" della risposta.
    private static Dictionary<string, object?> Limits(params (string Key, object? Value)[] limits) =>
        limits.ToDictionary(limit => limit.Key, limit => limit.Value);
}
