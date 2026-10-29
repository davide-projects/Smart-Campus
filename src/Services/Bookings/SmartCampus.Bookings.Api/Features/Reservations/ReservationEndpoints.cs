using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.ServiceDefaults;

namespace SmartCampus.Bookings.Api.Features.Reservations;

public sealed record FutureReservationsCountResponse(Guid RoomId, int Count);

// Risposta di POST /api/bookings/{id}/check-in: istante del check-in appena registrato.
public sealed record CheckInResponse(DateTimeOffset CheckedInAt);

public static class ReservationEndpoints
{
    // Il campus lavora in ora locale italiana (domanda aperta I-11).
    // Lunghezza massima di DisplayName in Identity e della colonna OrganizerName.
    public const int MaxUserNameLength = 128;

    // Valore del claim "role" del personale del campus emesso da Identity (Roles.Staff esiste solo nel Gateway).
    public const string StaffRole = "Staff";

    private static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder api)
    {
        // POST /api/bookings
        api.MapPost("/", CreateReservationAsync)
            .WithName("CreateReservation")
            .WithTags("Reservations")
            .Produces<ReservationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // GET /api/bookings/mine
        api.MapGet("/mine", GetMineAsync)
            .WithName("GetMyReservations")
            .WithTags("Reservations")
            .Produces<MyReservationsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // DELETE /api/bookings/{id}: cambio di stato, la riga resta (storico per «Cancellate» e report).
        // Con un id non GUID la route non corrisponde e risponde 404 il routing.
        api.MapDelete("/{id:guid}", CancelReservationAsync)
            .WithName("CancelReservation")
            .WithTags("Reservations")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // POST /api/bookings/{id}/check-in (US-14): nessun corpo, chi lo fa è il claim "sub".
        // Con un id non GUID la route non corrisponde e risponde 404 il routing, come per la DELETE.
        api.MapPost("/{id:guid}/check-in", CheckInAsync)
            .WithName("CheckInReservation")
            .WithTags("Reservations")
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/rooms/{roomId:guid}/future-reservations-count", CountFutureAsync)
            .WithName("CountFutureReservations")
            .WithTags("Reservations")
            .Produces<FutureReservationsCountResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    // Ordine dei controlli: design della change 16-prenotare-per-un-gruppo, decisione 4.
    private static async Task<IResult> CreateReservationAsync(
        CreateReservationRequest request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        BookingDbContext db,
        IdentityClient identityClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // 1. Chi prenota: l'utente del token, già verificato (firma, scadenza...) da AddSmartCampusJwtBearer.
        // Il nome si salva con la prenotazione così com'è adesso (snapshot): un cambio successivo in Identity non lo aggiorna.
        // Un token valido ma senza un "sub" che sia un GUID, o senza un nome utilizzabile, non identifica nessun utente.
        var organizerName = TryGetUserName(user);

        if (!Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var organizerId) || organizerName is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Accesso richiesto",
                detail: "Effettua l'accesso per prenotare.");
        }

        var memberIds = request.MemberIds ?? [];

        // 2. Solo gli studenti creano prenotazioni di gruppo: è un permesso, quindi uno Staff con membri
        // riceve sempre 403, qualunque siano gli altri dati. Senza membri la sua prenotazione resta com'è.
        if (memberIds.Count > 0 && user.IsInRole(StaffRole))
        {
            return ToProblem(ReservationRules.StaffGroupForbidden());
        }

        // 3. Regole sulla sola richiesta e sui membri: se è sbagliata non serve chiamare Identity né prendere lock.
        var now = timeProvider.GetUtcNow();

        if (ReservationRules.ValidateRequest(request, now) is { } invalidRequest)
        {
            return ToProblem(invalidRequest);
        }

        if (ReservationRules.ValidateMembers(memberIds, organizerId) is { } invalidMembers)
        {
            return ToProblem(invalidMembers);
        }

        // 4. INV-9: i membri sono studenti registrati, secondo Identity. La chiamata avviene prima della transazione
        // e fuori dalla strategia di retry: una risposta lenta non tiene bloccati spazio e persone, e un nuovo
        // tentativo della transazione non richiama Identity.
        IReadOnlyDictionary<Guid, string?> memberNames = new Dictionary<Guid, string?>();

        if (memberIds.Count > 0)
        {
            var check = await identityClient.CheckStudentsAsync(
                memberIds,
                httpRequest.Headers.Authorization.ToString(),
                cancellationToken);

            memberNames = check.DisplayNames;

            switch (check.Outcome)
            {
                case StudentsCheckOutcome.Unavailable:
                    return ToProblem(ReservationRules.MembersCheckUnavailable());
                case StudentsCheckOutcome.NotAllStudents:
                    return ToProblem(ReservationRules.NotStudents());
            }
        }

        // Organizzatore e membri in ordine crescente di id: due richieste che coinvolgono le stesse persone
        // prendono i lock nello stesso ordine e la seconda aspetta la prima invece di bloccarsi a vicenda.
        var people = memberIds.Append(organizerId).Order().ToList();

        // Con EnableRetryOnFailure una transazione manuale va eseguita dentro la strategia di retry:
        // se la connessione cade a metà, si ripete l'intera transazione da capo.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // In caso di nuovo tentativo si riparte puliti, senza oggetti rimasti dal tentativo precedente.
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // 5. Lock sulla riga dello spazio (INV-1): chi prenota lo stesso spazio aspetta qui il proprio turno.
            var room = await db.BookableRooms
                .FromSql($"SELECT * FROM BookableRooms WITH (UPDLOCK, ROWLOCK) WHERE RoomId = {request.RoomId}")
                .SingleOrDefaultAsync(cancellationToken);

            if (room is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Spazio non trovato",
                    detail: "Lo spazio richiesto non esiste.");
            }

            await db.Entry(room).Collection(bookableRoom => bookableRoom.OpeningHours).LoadAsync(cancellationToken);

            // 6. Lock sulle persone (INV-8): due prenotazioni che coinvolgono la stessa persona, anche su spazi
            // diversi, passano una alla volta. I lock si liberano da soli alla fine della transazione.
            foreach (var personId in people)
            {
                if (!await TryLockUserAsync(db, personId, cancellationToken))
                {
                    // Dopo 10 secondi di attesa un'altra prenotazione della stessa persona è ancora in corso.
                    return ToProblem(personId == organizerId
                        ? ReservationRules.UserLockTimeout()
                        : ReservationRules.MemberLockTimeout());
                }
            }

            // 7. Regole sullo spazio: attivo, prenotabile, dentro l'orario (INV-3, INV-4), poi capienza (INV-5).
            if (ReservationRules.ValidateAgainstRoom(request, room, CampusTimeZone) is { } invalidForRoom)
            {
                return ToProblem(invalidForRoom);
            }

            var participants = 1 + memberIds.Count;

            if (ReservationRules.ValidateCapacity(participants, room) is { } overCapacity)
            {
                return ToProblem(overCapacity);
            }

            // 8. INV-1: nessuna prenotazione confermata sovrapposta sullo stesso spazio.
            var roomTaken = await db.Reservations.AnyAsync(
                reservation =>
                    reservation.RoomId == request.RoomId &&
                    reservation.Status == ReservationStatus.Confirmed &&
                    reservation.Start < request.End &&
                    reservation.End > request.Start,
                cancellationToken);

            if (roomTaken)
            {
                var freeSlots = await FreeRangesOfTheDayAsync(db, room, request.Start, now, cancellationToken);

                return ToProblem(ReservationRules.RoomConflict(freeSlots));
            }

            // 9. INV-8: l'organizzatore non è già impegnato altrove, né come organizzatore né come membro...
            var organizerBusy = await db.Reservations.AnyAsync(
                reservation =>
                    reservation.Status == ReservationStatus.Confirmed &&
                    reservation.Start < request.End &&
                    reservation.End > request.Start &&
                    (reservation.OrganizerId == organizerId ||
                     reservation.Members.Any(member => member.UserId == organizerId)),
                cancellationToken);

            if (organizerBusy)
            {
                return ToProblem(ReservationRules.OrganizerConflict());
            }

            // ...e nemmeno i membri.
            if (memberIds.Count > 0)
            {
                var membersBusy = await db.Reservations.AnyAsync(
                    reservation =>
                        reservation.Status == ReservationStatus.Confirmed &&
                        reservation.Start < request.End &&
                        reservation.End > request.Start &&
                        (memberIds.Contains(reservation.OrganizerId) ||
                         reservation.Members.Any(member => memberIds.Contains(member.UserId))),
                    cancellationToken);

                if (membersBusy)
                {
                    return ToProblem(ReservationRules.MemberConflict());
                }
            }

            // 10. Prenotazione e membri con un solo SaveChanges, poi commit: da qui la fascia risulta occupata per tutti.
            var reservationId = Guid.NewGuid();
            var reservation = new Reservation
            {
                Id = reservationId,
                RoomId = room.RoomId,
                OrganizerId = organizerId,
                OrganizerName = organizerName,
                Start = request.Start,
                End = request.End,
                Participants = participants,
                Purpose = request.Purpose,
                Status = ReservationStatus.Confirmed,
                CreatedAt = now,
                Members = memberIds
                    .Select(memberId => new ReservationMember
                    {
                        ReservationId = reservationId,
                        UserId = memberId,
                        // Nome del momento della creazione, come OrganizerName. Uno non utilizzabile si salva null
                        // senza rifiutare: il membro è comunque uno studente confermato (decisione 8 del design).
                        DisplayName = UsableUserName(memberNames.GetValueOrDefault(memberId))
                    })
                    .ToList()
            };

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new ReservationResponse(
                reservation.Id,
                room.RoomId,
                room.Name,
                reservation.Start,
                reservation.End,
                reservation.Participants,
                reservation.Status);

            return Results.Created($"/api/bookings/{reservation.Id}", response);
        });
    }

    // Lock esclusivo sulla persona (risorsa "user:{id}"), legato alla transazione aperta.
    // False se dopo 10 secondi un'altra transazione lo tiene ancora.
    private static async Task<bool> TryLockUserAsync(BookingDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var userLock = $"user:{userId}";
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = {userLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 50001, 'Impossibile ottenere il lock sull''utente.', 1;
                """,
                cancellationToken);
            return true;
        }
        catch (SqlException exception) when (exception.Number == 50001)
        {
            return false;
        }
    }

    // Le prenotazioni in cui l'utente è organizzatore o membro, divise in future, passate e cancellate.
    // Nessuna paginazione, per scelta (design della change 22-mie-prenotazioni, decisione 5): le future sono
    // poche per costruzione (A2, INV-8), mentre passate e cancellate crescono nel tempo.
    // A parità di inizio l'Id rende l'ordine stabile tra una chiamata e l'altra.
    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal user,
        BookingDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Accesso richiesto",
                detail: "Effettua l'accesso per vedere le tue prenotazioni.");
        }

        // Una sola query: gli indici (OrganizerId, Start) e ReservationMembers (UserId) coprono i due rami.
        var reservations = await db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.OrganizerId == userId ||
                reservation.Members.Any(member => member.UserId == userId))
            .Select(reservation => new
            {
                reservation.Status,
                Item = new MyReservationItem(
                    reservation.Id,
                    reservation.RoomId,
                    reservation.Room.Name,
                    reservation.Room.BuildingName,
                    reservation.Start,
                    reservation.End,
                    reservation.Members.Any(),
                    reservation.OrganizerName,
                    reservation.OrganizerId == userId,
                    // Check-in fatto da chiunque (organizzatore, membro o Staff): vale per tutto il gruppo.
                    reservation.AttendanceRecords
                        .Where(record => record.Type == AttendanceType.CheckIn)
                        .Select(record => (DateTimeOffset?)record.RecordedAt)
                        .FirstOrDefault(),
                    reservation.Members
                        .Select(member => new MyReservationMember(member.UserId, member.DisplayName))
                        .ToList())
            })
            .ToListAsync(cancellationToken);

        // Membri in ordine di nome, senza distinzione di maiuscole; i nomi non noti in fondo, a parità per id.
        reservations = reservations
            .Select(reservation => reservation with { Item = reservation.Item with { Members = SortMembers(reservation.Item.Members) } })
            .ToList();

        // Una prenotazione è "passata" quando la sua fine è già arrivata: quelle in corso restano tra le future.
        var now = timeProvider.GetUtcNow();
        var confirmed = reservations.Where(reservation => reservation.Status == ReservationStatus.Confirmed).ToList();

        var response = new MyReservationsResponse(
            Upcoming: confirmed
                .Where(reservation => reservation.Item.End > now)
                .Select(reservation => reservation.Item)
                .OrderBy(item => item.Start)
                .ThenBy(item => item.Id)
                .ToList(),
            Past: confirmed
                .Where(reservation => reservation.Item.End <= now)
                .Select(reservation => reservation.Item)
                .OrderByDescending(item => item.Start)
                .ThenBy(item => item.Id)
                .ToList(),
            Cancelled: reservations
                .Where(reservation => reservation.Status == ReservationStatus.Cancelled)
                .Select(reservation => reservation.Item)
                .OrderByDescending(item => item.Start)
                .ThenBy(item => item.Id)
                .ToList());

        return Results.Ok(response);
    }

    // Cancellazione di una prenotazione (US-08). Ordine dei controlli e concorrenza: design della change
    // 25-cancella-prenotazione, decisioni 2 e 4.
    private static async Task<IResult> CancelReservationAsync(
        Guid id,
        ClaimsPrincipal user,
        BookingDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // 1. Token valido ma senza un "sub" che sia un GUID: non identifica nessun utente.
        if (!Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Accesso richiesto",
                detail: "Effettua l'accesso per cancellare la prenotazione.");
        }

        // Il ruolo è il claim "role" del token così com'è (RoleClaimType = "role").
        var isStaff = user.IsInRole(StaffRole);
        var now = timeProvider.GetUtcNow();

        // Il ciclo termina al più al secondo giro: un UPDATE con 0 righe vuol dire che un'altra richiesta
        // ha già portato la prenotazione a Cancelled (INV-7: non torna più Confirmed), quindi la rilettura
        // dà 204 o 403, oppure 404/422; non può più dare "consentita" con lo stesso "adesso".
        for (var attempt = 0; attempt < 2; attempt++)
        {
            // 2. Lettura senza tracking: la scrittura è un comando a parte. Insieme alla prenotazione
            // si legge se ha un check-in (design della change 45-check-in, decisione 7).
            var found = await db.Reservations
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    Reservation = r,
                    HasCheckIn = r.AttendanceRecords.Any(record => record.Type == AttendanceType.CheckIn)
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (found is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Prenotazione non trovata",
                    detail: "La prenotazione richiesta non esiste.");
            }

            // 3-6. Chi può cancellare, già cancellata, prenotazione usata, limite di tempo.
            var decision = ReservationRules.ValidateCancellation(found.Reservation, userId, isStaff, found.HasCheckIn, now);

            switch (decision.Outcome)
            {
                case CancellationOutcome.Rejected:
                    return ToProblem(decision.Rejection!);
                case CancellationOutcome.AlreadyCancelled:
                    return Results.NoContent();
            }

            // 7. Un solo UPDATE condizionato: SQL Server valuta la WHERE in modo atomico sulla riga, quindi
            // con richieste parallele al massimo una porta la prenotazione a Cancelled. Istruzione singola,
            // senza transazione esplicita: un nuovo tentativo di EnableRetryOnFailure trova 0 righe e rilegge.
            // La condizione sul check-in esclude la prenotazione usata nel frattempo: il lock di scrittura
            // dell'UPDATE aspetta quello del check-in in corso (design della change 45-check-in, decisione 4).
            var updated = await db.Reservations
                .Where(r =>
                    r.Id == id &&
                    r.Status == ReservationStatus.Confirmed &&
                    r.Start > now &&
                    !r.AttendanceRecords.Any(record => record.Type == AttendanceType.CheckIn))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, ReservationStatus.Cancelled)
                    .SetProperty(r => r.CancelledAt, now),
                    cancellationToken);

            if (updated == 1)
            {
                return Results.NoContent();
            }

            // 0 righe: la prenotazione è cambiata tra la lettura e l'UPDATE. Si rilegge e si decide di nuovo.
        }

        // Non dovrebbe mai succedere: vorrebbe dire che una prenotazione cancellata è tornata Confirmed (INV-7).
        throw new InvalidOperationException(
            $"La prenotazione {id} risulta ancora cancellabile dopo un UPDATE senza righe aggiornate: " +
            "una prenotazione cancellata non deve tornare confermata (INV-7).");
    }

    // Check-in di una prenotazione (US-14). Ordine dei controlli e concorrenza: design della change
    // 45-check-in, decisioni 3 e 4.
    private static async Task<IResult> CheckInAsync(
        Guid id,
        ClaimsPrincipal user,
        BookingDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // 1. Token valido ma senza un "sub" che sia un GUID: non identifica nessun utente.
        if (!Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Accesso richiesto",
                detail: "Effettua l'accesso per fare il check-in.");
        }

        // Il ruolo è il claim "role" del token così com'è (RoleClaimType = "role").
        var isStaff = user.IsInRole(StaffRole);
        var now = timeProvider.GetUtcNow();

        // Con EnableRetryOnFailure una transazione manuale va eseguita dentro la strategia di retry.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // In caso di nuovo tentativo si riparte puliti, senza oggetti rimasti dal tentativo precedente.
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // 2. Lock sulla riga della prenotazione fino al commit: i check-in sulla stessa prenotazione passano
            // uno alla volta, e l'UPDATE della DELETE aspetta (o viene aspettato da) questa transazione.
            var reservation = await db.Reservations
                .FromSql($"SELECT * FROM Reservations WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (reservation is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Prenotazione non trovata",
                    detail: "La prenotazione richiesta non esiste.");
            }

            // 3. Sotto il lock: appartenenza come membro e tipo dell'eventuale record di presenza.
            var isMember = await db.ReservationMembers.AnyAsync(
                member => member.ReservationId == id && member.UserId == userId,
                cancellationToken);

            var existingRecord = await ExistingAttendanceAsync(db, id, cancellationToken);

            // 4-8. Chi può fare il check-in, cancellata, no-show, già registrato, finestra.
            if (ReservationRules.ValidateCheckIn(reservation, userId, isMember, isStaff, existingRecord, now) is { } rejection)
            {
                return ToProblem(rejection);
            }

            // 9. Inserimento e commit: da qui la prenotazione risulta usata per tutto il gruppo.
            var record = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                ReservationId = id,
                Type = AttendanceType.CheckIn,
                RecordedAt = now,
                RecordedBy = userId
            };

            db.AttendanceRecords.Add(record);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueIndexViolation(exception))
            {
                // Un altro record di presenza è stato scritto senza passare dal lock (indice unico su ReservationId):
                // si annulla, si rilegge e si risponde con il rifiuto del record trovato, mai 500.
                // Il lock mette in fila i check-in, quindi qui si arriva solo se il record è scritto da altro codice (es. il no-show);
                // se la rilettura non lo trova più (caso teorico) si risponde «già registrato».
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                var recorded = await ExistingAttendanceAsync(db, id, cancellationToken) ?? AttendanceType.CheckIn;
                return ToProblem(ReservationRules.ExistingAttendance(recorded));
            }

            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new CheckInResponse(record.RecordedAt));
        });
    }

    // Tipo dell'eventuale record di presenza della prenotazione (al massimo uno), null se non ce n'è.
    private static Task<AttendanceType?> ExistingAttendanceAsync(
        BookingDbContext db,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        db.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.ReservationId == reservationId)
            .Select(record => (AttendanceType?)record.Type)
            .FirstOrDefaultAsync(cancellationToken);

    // 2601: chiave duplicata in un indice unico; 2627: violazione di un vincolo UNIQUE o PRIMARY KEY.
    private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    // Nome visualizzato emesso da Identity. Null se il claim manca, è vuoto o è più lungo della colonna.
    private static List<MyReservationMember> SortMembers(IEnumerable<MyReservationMember> members) =>
        members
            .OrderBy(member => member.DisplayName is null)
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Id)
            .ToList();

    private static string? TryGetUserName(ClaimsPrincipal user) =>
        UsableUserName(user.FindFirstValue(JwtClaimNames.Name));

    // Un nome è utilizzabile se non è vuoto e sta nella colonna (MaxUserNameLength).
    private static string? UsableUserName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaxUserNameLength
            ? name
            : null;

    // Le fasce libere dello stesso spazio nel giorno della richiesta (in ora del campus), da proporre dopo un conflitto.
    // Le fasce già iniziate sono escluse: non sarebbero prenotabili.
    private static async Task<IReadOnlyList<FreeRange>> FreeRangesOfTheDayAsync(
        BookingDbContext db,
        BookableRoom room,
        DateTimeOffset requestedStart,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(requestedStart, CampusTimeZone).DateTime);

        var dayStartLocal = date.ToDateTime(TimeOnly.MinValue);
        var dayEndLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var dayStart = new DateTimeOffset(dayStartLocal, CampusTimeZone.GetUtcOffset(dayStartLocal));
        var dayEnd = new DateTimeOffset(dayEndLocal, CampusTimeZone.GetUtcOffset(dayEndLocal));

        var reservations = await db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == room.RoomId &&
                reservation.Status == ReservationStatus.Confirmed &&
                reservation.Start < dayEnd &&
                reservation.End > dayStart)
            .ToListAsync(cancellationToken);

        var openingHours = room.OpeningHours.SingleOrDefault(hours => hours.DayOfWeek == date.DayOfWeek);
        var availability = AvailabilityCalculator.Calculate(room.RoomId, date, openingHours, reservations, CampusTimeZone);

        return AvailabilityCalculator.FreeRanges(availability, notBefore: now);
    }

    // Oltre a titolo e dettaglio, la risposta porta il codice di motivo e, se ci sono, i limiti da rispettare.
    private static IResult ToProblem(ReservationRejection rejection)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = rejection.Code };

        if (rejection.Limits is not null)
        {
            extensions["limits"] = rejection.Limits;
        }

        if (rejection.FreeSlots is not null)
        {
            extensions["freeSlots"] = rejection.FreeSlots;
        }

        return Results.Problem(
            statusCode: rejection.StatusCode,
            title: rejection.Title,
            detail: rejection.Detail,
            extensions: extensions);
    }

    private static async Task<IResult> CountFutureAsync(
        Guid roomId,
        BookingDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var roomExists = await db.BookableRooms.AnyAsync(room => room.RoomId == roomId, cancellationToken);
        if (!roomExists)
        {
            return Results.Problem(
                title: "Spazio non trovato",
                detail: "Non esiste uno spazio prenotabile con l'identificativo richiesto.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Conta le prenotazioni confermate non ancora terminate, comprese quelle in corso:
        // dismettere lo spazio interromperebbe anche quelle. Le terminate restano nei report.
        var now = clock.GetUtcNow();
        var count = await db.Reservations
            .AsNoTracking()
            .CountAsync(reservation =>
                reservation.RoomId == roomId
                && reservation.Status == ReservationStatus.Confirmed
                && reservation.End > now, cancellationToken);

        return Results.Ok(new FutureReservationsCountResponse(roomId, count));
    }
}
