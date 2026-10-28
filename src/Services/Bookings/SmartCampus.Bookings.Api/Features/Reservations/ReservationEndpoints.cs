using Microsoft.EntityFrameworkCore;

using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Reservations;

public sealed record FutureReservationsCountResponse(Guid RoomId, int Count);

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/rooms/{roomId:guid}/future-reservations-count", CountFutureAsync)
            .WithName("CountFutureReservations")
            .WithTags("Reservations")
            .Produces<FutureReservationsCountResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return api;
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
