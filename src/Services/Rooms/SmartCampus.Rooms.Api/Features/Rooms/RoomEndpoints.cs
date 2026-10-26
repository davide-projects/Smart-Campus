using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Rooms.Data;
using SmartCampus.Rooms.Domain;

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

        return api;
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
