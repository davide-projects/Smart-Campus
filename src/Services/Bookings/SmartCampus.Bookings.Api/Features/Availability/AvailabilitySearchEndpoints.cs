using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Availability;

// Uno spazio libero nella fascia oraria cercata.
public sealed record AvailableRoomResponse(
    Guid RoomId,
    string Name,
    string? BuildingName,
    string? Type,
    int Capacity);

public static class AvailabilitySearchEndpoints
{
    // Il campus lavora in ora locale italiana (domanda aperta I-11).
    private static readonly TimeZoneInfo CampusTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static RouteGroupBuilder MapAvailabilitySearchEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/bookings/availability/search?date=2026-11-04&start=10:00&end=12:00&type=Laboratory&minCapacity=20
        api.MapGet("/availability/search", SearchAsync)
            .WithName("SearchAvailableRooms")
            .WithTags("Availability")
            .Produces<IReadOnlyList<AvailableRoomResponse>>()
            .Produces(StatusCodes.Status400BadRequest);

        return api;
    }

    private static async Task<IResult> SearchAsync(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string? type,
        int? minCapacity,
        BookingDbContext db,
        CancellationToken cancellationToken)
    {
        if (end <= start)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Fascia oraria non valida",
                detail: "L'ora di fine deve essere successiva a quella di inizio.");
        }

        if (minCapacity is < 1)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Capienza minima non valida",
                detail: "La capienza minima deve essere almeno 1.");
        }

        var dayOfWeek = date.DayOfWeek;
        var windowStart = ToCampusTime(date.ToDateTime(start));
        var windowEnd = ToCampusTime(date.ToDateTime(end));

        // Spazi attivi e prenotabili, aperti per tutta la fascia e senza prenotazioni confermate che la tocchino.
        // Le adiacenti (che finiscono quando la fascia comincia, o cominciano quando finisce) non la toccano.
        // Con il filtro per tipo restano fuori gli spazi di cui Bookings non conosce ancora il tipo.
        var rooms = await db.BookableRooms
            .AsNoTracking()
            .Where(room => room.IsActive && room.IsBookable)
            .Where(room => type == null || room.Type == type)
            .Where(room => minCapacity == null || room.Capacity >= minCapacity)
            .Where(room => room.OpeningHours.Any(hours =>
                hours.DayOfWeek == dayOfWeek && hours.OpensAt <= start && hours.ClosesAt >= end))
            .Where(room => !db.Reservations.Any(reservation =>
                reservation.RoomId == room.RoomId
                && reservation.Status == ReservationStatus.Confirmed
                && reservation.Start < windowEnd
                && reservation.End > windowStart))
            .OrderBy(room => room.Name)
            .Select(room => new AvailableRoomResponse(room.RoomId, room.Name, room.BuildingName, room.Type, room.Capacity))
            .ToListAsync(cancellationToken);

        return Results.Ok(rooms);
    }

    private static DateTimeOffset ToCampusTime(DateTime local) =>
        new(local, CampusTimeZone.GetUtcOffset(local));
}
