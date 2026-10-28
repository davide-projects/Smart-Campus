using Microsoft.EntityFrameworkCore;

using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Reservations;

public sealed record ReservationListItem(
    Guid Id,
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid OrganizerId,
    int Participants,
    IReadOnlyList<Guid> MemberIds,
    ReservationStatus Status,
    DateTimeOffset? CancelledAt);

public static class ReservationListEndpoints
{
    // Il campus lavora in ora locale italiana (domanda aperta I-11), come per le fasce di disponibilità.
    private static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static RouteGroupBuilder MapReservationListEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/bookings?roomId=...&date=2026-11-03
        api.MapGet("/", ListByRoomAndDateAsync)
            .WithName("ListReservationsByRoomAndDate")
            .RequireAuthorization(policy => policy.RequireRole("Staff"))
            .WithTags("Reservations")
            .Produces<IReadOnlyList<ReservationListItem>>()
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> ListByRoomAndDateAsync(
        Guid roomId,
        DateOnly date,
        BookingDbContext db,
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

        // La giornata si legge in ora del campus: le prenotazioni che la toccano, in ordine di orario.
        var dayStart = ToCampusTime(date.ToDateTime(TimeOnly.MinValue));
        var dayEnd = ToCampusTime(date.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var reservations = await db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == roomId
                && reservation.Start < dayEnd
                && reservation.End > dayStart)
            .OrderBy(reservation => reservation.Start)
            .ThenBy(reservation => reservation.End)
            .Select(reservation => new
            {
                reservation.Id,
                reservation.Start,
                reservation.End,
                reservation.OrganizerId,
                reservation.Participants,
                reservation.Status,
                reservation.CancelledAt,
                MemberIds = reservation.Members.Select(member => member.UserId).ToList()
            })
            .ToListAsync(cancellationToken);

        // Le cancellate restano nell'elenco: si riconoscono dallo stato.
        var items = reservations
            .Select(r => new ReservationListItem(
                r.Id, r.Start, r.End, r.OrganizerId, r.Participants, r.MemberIds, r.Status, r.CancelledAt))
            .ToList();

        return Results.Ok(items);
    }

    private static DateTimeOffset ToCampusTime(DateTime local) =>
        new(local, CampusTimeZone.GetUtcOffset(local));
}
