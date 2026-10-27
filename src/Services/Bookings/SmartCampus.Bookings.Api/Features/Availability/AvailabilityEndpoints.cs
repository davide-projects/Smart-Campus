using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Availability;

public static class AvailabilityEndpoints
{
    // Il campus lavora in ora locale italiana (domanda aperta I-11).
    private static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static RouteGroupBuilder MapAvailabilityEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/bookings/availability?roomId=...&date=2026-11-03
        api.MapGet("/availability", GetAvailabilityAsync)
            .WithName("GetAvailability");

        return api;
    }

    private static async Task<IResult> GetAvailabilityAsync(
        Guid roomId,
        DateOnly date,
        BookingDbContext db,
        CancellationToken cancellationToken)
    {
        var room = await db.BookableRooms
            .AsNoTracking()
            .Include(bookableRoom => bookableRoom.OpeningHours)
            .SingleOrDefaultAsync(bookableRoom => bookableRoom.RoomId == roomId, cancellationToken);

        // Spazio sconosciuto o dismesso: per Bookings non esiste.
        if (room is null || !room.IsActive)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Spazio non trovato",
                detail: "Lo spazio richiesto non esiste o non è più disponibile.");
        }

        var openingHours = room.OpeningHours.SingleOrDefault(hours => hours.DayOfWeek == date.DayOfWeek);

        // Inizio e fine della giornata in ora del campus, per leggere solo le prenotazioni di quel giorno.
        var dayStart = ToCampusTime(date.ToDateTime(TimeOnly.MinValue));
        var dayEnd = ToCampusTime(date.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var reservations = await db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == roomId &&
                reservation.Status == ReservationStatus.Confirmed &&
                reservation.Start < dayEnd &&
                reservation.End > dayStart)
            .ToListAsync(cancellationToken);

        var availability = AvailabilityCalculator.Calculate(roomId, date, openingHours, reservations, CampusTimeZone);

        return Results.Ok(availability);
    }

    private static DateTimeOffset ToCampusTime(DateTime local) =>
        new(local, CampusTimeZone.GetUtcOffset(local));
}
