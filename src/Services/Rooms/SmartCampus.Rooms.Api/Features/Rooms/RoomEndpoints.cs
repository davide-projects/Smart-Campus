using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Rooms.Data;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Features.Rooms;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/", SearchAsync)
            .WithName("SearchRooms")
            .WithTags("Rooms")
            .Produces<IReadOnlyList<RoomSummaryResponse>>()
            .ProducesValidationProblem();

        api.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetRoomById")
            .WithTags("Rooms")
            .Produces<RoomDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        RoomsDbContext db,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms
            .AsNoTracking()
            .Where(room => room.Id == id && room.IsActive)
            .Include(room => room.Building)
            .Include(room => room.OpeningHours)
            .Include(room => room.RoomEquipment)
                .ThenInclude(link => link.Equipment)
            .SingleOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            return Results.Problem(
                title: "Spazio non trovato",
                detail: "Non esiste uno spazio attivo con l'identificativo richiesto.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var openingHoursByDay = room.OpeningHours.ToDictionary(hours => hours.DayOfWeek);
        var openingHours = Enum.GetValues<DayOfWeek>()
            .Select(day => openingHoursByDay.TryGetValue(day, out var hours)
                ? new OpeningHoursResponse(day, hours.OpensAt, hours.ClosesAt, IsClosed: false)
                : new OpeningHoursResponse(day, null, null, IsClosed: true))
            .ToList();

        var equipment = room.RoomEquipment
            .Select(link => new RoomEquipmentResponse(link.Equipment.Id, link.Equipment.Name))
            .OrderBy(item => item.Name)
            .ToList();

        return Results.Ok(new RoomDetailResponse(
            room.Id,
            room.Name,
            room.Type,
            room.BuildingId,
            room.Building.Name,
            room.Building.Code,
            room.Floor,
            room.Capacity,
            room.IsBookable,
            equipment,
            openingHours));
    }

    private static async Task<IResult> SearchAsync(
        [FromQuery] RoomType? type,
        [FromQuery] int? minCapacity,
        [FromQuery] Guid? buildingId,
        RoomsDbContext db,
        CancellationToken cancellationToken)
    {
        if (minCapacity is <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["minCapacity"] = ["La capienza minima deve essere maggiore di zero."]
            });
        }

        if (buildingId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["buildingId"] = ["L'identificativo dell'edificio non è valido."]
            });
        }

        var rooms = await RoomSearch
            .Apply(db.Rooms.AsNoTracking(), new RoomFilter(type, minCapacity, buildingId))
            .OrderBy(room => room.Building.Name)
            .ThenBy(room => room.Name)
            .Select(room => new RoomSummaryResponse(
                room.Id,
                room.Name,
                room.Type,
                room.Capacity,
                room.Floor,
                room.IsBookable,
                room.BuildingId,
                room.Building.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(rooms);
    }
}
