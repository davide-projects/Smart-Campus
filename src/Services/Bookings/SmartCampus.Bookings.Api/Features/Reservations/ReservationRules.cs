using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;

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

    // Gruppo 1: regole sulla sola richiesta. Si controllano prima della transazione:
    // se la richiesta è sbagliata non serve prendere nessun lock.
    public static ReservationRejection? ValidateRequest(CreateReservationRequest request, DateTimeOffset now)
    {
        // INV-2
        if (request.End <= request.Start)
        {
            return Unprocessable("Orario non valido", "L'orario di fine deve essere successivo a quello di inizio.");
        }

        // INV-6
        if (request.Start < now)
        {
            return Unprocessable("Orario nel passato", "Non è possibile prenotare nel passato.");
        }

        // A1: inizio e fine all'ora esatta o alla mezz'ora, come le fasce della disponibilità.
        if (!IsOnSlotBoundary(request.Start) || !IsOnSlotBoundary(request.End))
        {
            return Unprocessable("Orario non valido", "Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora.");
        }

        // Il motivo è facoltativo, ma la colonna nel database ha al massimo 200 caratteri.
        if (request.Purpose?.Length > MaxPurposeLength)
        {
            return Unprocessable("Motivo troppo lungo", "Il motivo può contenere al massimo 200 caratteri.");
        }

        // A2
        if (request.End - request.Start > MaxDuration)
        {
            return Unprocessable("Durata eccessiva", "Una prenotazione può durare al massimo 4 ore.");
        }

        // A2
        if (request.Start > now + MaxAdvance)
        {
            return Unprocessable("Troppo in anticipo", "Si può prenotare al massimo 14 giorni prima.");
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
            return Unprocessable("Spazio non prenotabile", "Questo spazio non è prenotabile.");
        }

        // INV-3: gli orari di apertura sono in ora locale del campus.
        var localStart = TimeZoneInfo.ConvertTime(request.Start, campusTimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(request.End, campusTimeZone);

        if (localStart.Date != localEnd.Date)
        {
            return Unprocessable("Orario non valido", "La prenotazione deve iniziare e finire nello stesso giorno.");
        }

        var openingHours = room.OpeningHours.SingleOrDefault(hours => hours.DayOfWeek == localStart.DayOfWeek);

        if (openingHours is null)
        {
            return Unprocessable("Fuori orario", "Lo spazio è chiuso in questo giorno.");
        }

        var startTime = TimeOnly.FromDateTime(localStart.DateTime);
        var endTime = TimeOnly.FromDateTime(localEnd.DateTime);

        if (startTime < openingHours.OpensAt || endTime > openingHours.ClosesAt)
        {
            return Unprocessable(
                "Fuori orario",
                $"Lo spazio è aperto dalle {openingHours.OpensAt:HH\\:mm} alle {openingHours.ClosesAt:HH\\:mm}.");
        }

        return null;
    }

    // INV-1: conflitto con una prenotazione confermata dello stesso spazio.
    public static ReservationRejection RoomConflict() =>
        new(StatusCodes.Status409Conflict, "Spazio già prenotato", "Lo spazio è già prenotato in questa fascia.");

    // INV-8: l'organizzatore ha già una prenotazione confermata sovrapposta, anche in un altro spazio.
    public static ReservationRejection OrganizerConflict() =>
        new(StatusCodes.Status409Conflict, "Hai già una prenotazione", "Hai già una prenotazione in questa fascia oraria.");

    public static ReservationRejection UserLockTimeout() =>
        new(StatusCodes.Status409Conflict, "Prenotazione in corso", "Hai un'altra prenotazione in corso, riprova tra qualche secondo.");

    // Cancellazione (US-08): l'ordine dei controlli è quello del design della change 25-cancella-prenotazione, decisione 2.
    // 1. chi può cancellare: prima di tutto, così un estraneo riceve sempre 403 e non scopre lo stato della prenotazione;
    // 2. già cancellata: nessuna modifica (idempotenza), anche se nel frattempo l'inizio è passato;
    // 3. limite di tempo (assunzione A5): si cancella solo prima dell'inizio, Staff compreso.
    public static CancellationDecision ValidateCancellation(
        Reservation reservation,
        Guid userId,
        bool isStaff,
        DateTimeOffset now)
    {
        if (reservation.OrganizerId != userId && !isStaff)
        {
            return CancellationDecision.Reject(new ReservationRejection(
                StatusCodes.Status403Forbidden,
                "Cancellazione non consentita",
                "Non puoi cancellare questa prenotazione."));
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            return CancellationDecision.AlreadyCancelled;
        }

        if (reservation.Start <= now)
        {
            return CancellationDecision.Reject(Unprocessable(
                "Cancellazione non consentita",
                "Non è possibile cancellare una prenotazione già iniziata o passata."));
        }

        return CancellationDecision.Allowed;
    }

    private static bool IsOnSlotBoundary(DateTimeOffset value) =>
        value.TimeOfDay.Ticks % SlotLength.Ticks == 0;

    private static ReservationRejection Unprocessable(string title, string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, title, detail);
}
