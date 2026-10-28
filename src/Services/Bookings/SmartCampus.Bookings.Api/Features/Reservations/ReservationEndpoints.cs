using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Reservations;

public sealed record FutureReservationsCountResponse(Guid RoomId, int Count);

public static class ReservationEndpoints
{
    // Il campus lavora in ora locale italiana (domanda aperta I-11).
    private static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder api)
    {
        // POST /api/bookings
        api.MapPost("/", CreateReservationAsync)
            .WithName("CreateReservation")
            .WithTags("Reservations")
            .Produces<ReservationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // GET /api/bookings/mine
        api.MapGet("/mine", GetMineAsync)
            .WithName("GetMyReservations")
            .WithTags("Reservations")
            .Produces<MyReservationsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        api.MapGet("/rooms/{roomId:guid}/future-reservations-count", CountFutureAsync)
            .WithName("CountFutureReservations")
            .WithTags("Reservations")
            .Produces<FutureReservationsCountResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> CreateReservationAsync(
        CreateReservationRequest request,
        HttpRequest httpRequest,
        BookingDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // 1. Chi prenota: dal token inoltrato dal Gateway. Il nome si salva con la prenotazione
        // così com'è adesso (snapshot): un cambio successivo in Identity non lo aggiorna.
        var organizerId = CurrentUser.TryGetUserId(httpRequest);
        var organizerName = CurrentUser.TryGetUserName(httpRequest);

        if (organizerId is null || organizerName is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Accesso richiesto",
                detail: "Effettua l'accesso per prenotare.");
        }

        // 2. Regole sulla sola richiesta: se è sbagliata non serve prendere nessun lock.
        var now = timeProvider.GetUtcNow();

        if (ReservationRules.ValidateRequest(request, now) is { } invalidRequest)
        {
            return ToProblem(invalidRequest);
        }

        // Con EnableRetryOnFailure una transazione manuale va eseguita dentro la strategia di retry:
        // se la connessione cade a metà, si ripete l'intera transazione da capo.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // In caso di nuovo tentativo si riparte puliti, senza oggetti rimasti dal tentativo precedente.
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // 3. Lock sulla riga dello spazio (INV-1): chi prenota lo stesso spazio aspetta qui il proprio turno.
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

            // 4. Lock sulla persona (INV-8): due prenotazioni della stessa persona, anche su spazi diversi,
            // passano una alla volta. Il lock si libera da solo alla fine della transazione.
            var userLock = $"user:{organizerId}";
            try
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    DECLARE @result int;
                    EXEC @result = sp_getapplock @Resource = {userLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @result < 0 THROW 50001, 'Impossibile ottenere il lock sull''utente.', 1;
                    """,
                    cancellationToken);
            }
            catch (SqlException exception) when (exception.Number == 50001)
            {
                // Dopo 10 secondi di attesa un'altra prenotazione della stessa persona è ancora in corso.
                return ToProblem(ReservationRules.UserLockTimeout());
            }

            // 5. Regole sullo spazio: attivo, prenotabile, dentro l'orario (INV-3, INV-4).
            if (ReservationRules.ValidateAgainstRoom(request, room, CampusTimeZone) is { } invalidForRoom)
            {
                return ToProblem(invalidForRoom);
            }

            // 6. INV-1: nessuna prenotazione confermata sovrapposta sullo stesso spazio.
            var roomTaken = await db.Reservations.AnyAsync(
                reservation =>
                    reservation.RoomId == request.RoomId &&
                    reservation.Status == ReservationStatus.Confirmed &&
                    reservation.Start < request.End &&
                    reservation.End > request.Start,
                cancellationToken);

            if (roomTaken)
            {
                return ToProblem(ReservationRules.RoomConflict());
            }

            // 7. INV-8: l'organizzatore non è già impegnato altrove, né come organizzatore né come membro.
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

            // 8. Inserimento e commit: da qui la fascia risulta occupata per tutti.
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                RoomId = room.RoomId,
                OrganizerId = organizerId.Value,
                OrganizerName = organizerName,
                Start = request.Start,
                End = request.End,
                Participants = 1,
                Purpose = request.Purpose,
                Status = ReservationStatus.Confirmed,
                CreatedAt = now
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

    // Le prenotazioni in cui l'utente è organizzatore o membro, divise in future, passate e cancellate.
    // Nessuna paginazione, per scelta (design della change 22-mie-prenotazioni, decisione 5): le future sono
    // poche per costruzione (A2, INV-8), mentre passate e cancellate crescono nel tempo.
    // A parità di inizio l'Id rende l'ordine stabile tra una chiamata e l'altra.
    private static async Task<IResult> GetMineAsync(
        HttpRequest httpRequest,
        BookingDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUser.TryGetUserId(httpRequest);

        if (userId is null)
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
                    reservation.OrganizerId == userId)
            })
            .ToListAsync(cancellationToken);

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

    private static IResult ToProblem(ReservationRejection rejection) =>
        Results.Problem(statusCode: rejection.StatusCode, title: rejection.Title, detail: rejection.Detail);

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
