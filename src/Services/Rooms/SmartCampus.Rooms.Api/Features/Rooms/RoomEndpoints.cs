using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Contracts;
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

        api.MapPost("/", CreateAsync)
            .WithName("CreateRoom")
            .WithTags("Rooms")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        api.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateRoom")
            .WithTags("Rooms")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        api.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateRoom")
            .WithTags("Rooms")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> CreateAsync(
        RoomWriteRequest request,
        RoomsDbContext db,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateWriteRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (!await db.Buildings.AnyAsync(building => building.Id == request.BuildingId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.BuildingId)] = ["L'edificio indicato non esiste."]
            });
        }

        var roomName = request.Name.Trim();
        if (await db.Rooms.AnyAsync(room => room.BuildingId == request.BuildingId && room.Name == roomName, cancellationToken))
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Nome spazio già utilizzato",
                Detail = "Esiste già uno spazio con questo nome nell'edificio indicato."
            });
        }

        var room = new Room
        {
            Id = Guid.NewGuid(),
            BuildingId = request.BuildingId,
            Name = roomName,
            Type = request.Type,
            Capacity = request.Capacity,
            Floor = request.Floor,
            IsBookable = request.IsBookable,
            IsActive = true,
            OpeningHours = CreateOpeningHours(request.OpeningHours)
        };

        db.Rooms.Add(room);
        await publishEndpoint.Publish(RoomSeedEventPublisher.ToCreatedEvent(room), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/rooms/{room.Id}", new { room.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        RoomWriteRequest request,
        RoomsDbContext db,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateWriteRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var room = await db.Rooms
            .Include(existing => existing.OpeningHours)
            .SingleOrDefaultAsync(existing => existing.Id == id && existing.IsActive, cancellationToken);

        if (room is null)
        {
            return Results.Problem(
                title: "Spazio non trovato",
                detail: "Non esiste uno spazio attivo con l'identificativo richiesto.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!await db.Buildings.AnyAsync(building => building.Id == request.BuildingId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.BuildingId)] = ["L'edificio indicato non esiste."]
            });
        }

        var roomName = request.Name.Trim();
        if (await db.Rooms.AnyAsync(existing =>
                existing.Id != id && existing.BuildingId == request.BuildingId && existing.Name == roomName,
                cancellationToken))
        {
            return Results.Conflict(new ProblemDetails
            {
                Title = "Nome spazio già utilizzato",
                Detail = "Esiste già uno spazio con questo nome nell'edificio indicato."
            });
        }

        room.BuildingId = request.BuildingId;
        room.Name = roomName;
        room.Type = request.Type;
        room.Capacity = request.Capacity;
        room.Floor = request.Floor;
        room.IsBookable = request.IsBookable;
        var requestedHours = request.OpeningHours.ToDictionary(hours => hours.DayOfWeek);
        foreach (var existingHours in room.OpeningHours.ToArray())
        {
            if (!requestedHours.TryGetValue(existingHours.DayOfWeek, out var updatedHours))
            {
                room.OpeningHours.Remove(existingHours);
                db.OpeningHours.Remove(existingHours);
                continue;
            }

            existingHours.OpensAt = updatedHours.OpensAt;
            existingHours.ClosesAt = updatedHours.ClosesAt;
            requestedHours.Remove(existingHours.DayOfWeek);
        }

        foreach (var openingHours in requestedHours.Values)
        {
            db.OpeningHours.Add(new OpeningHours
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Room = room,
                DayOfWeek = openingHours.DayOfWeek,
                OpensAt = openingHours.OpensAt,
                ClosesAt = openingHours.ClosesAt
            });
        }

        var roomUpdated = new RoomUpdated(
            room.Id,
            room.Name,
            room.Capacity,
            room.IsBookable,
            room.IsActive)
        {
            OpeningHours = RoomSeedEventPublisher.ToOpeningHours(room)
        };
        await publishEndpoint.Publish(roomUpdated, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        RoomsDbContext db,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms
            .SingleOrDefaultAsync(existing => existing.Id == id && existing.IsActive, cancellationToken);

        if (room is null)
        {
            return Results.Problem(
                title: "Spazio non trovato",
                detail: "Non esiste uno spazio attivo con l'identificativo richiesto.",
                statusCode: StatusCodes.Status404NotFound);
        }

        room.IsActive = false;
        await publishEndpoint.Publish(new RoomDeactivated(room.Id), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static Dictionary<string, string[]> ValidateWriteRequest(RoomWriteRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.BuildingId == Guid.Empty)
        {
            errors[nameof(request.BuildingId)] = ["L'identificativo dell'edificio non è valido."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            errors[nameof(request.Name)] = ["Il nome deve contenere da 1 a 100 caratteri."];
        }

        if (request.Capacity <= 0)
        {
            errors[nameof(request.Capacity)] = ["La capienza deve essere maggiore di zero."];
        }

        if (!Enum.IsDefined(request.Type))
        {
            errors[nameof(request.Type)] = ["Il tipo di spazio non è valido."];
        }

        if (request.OpeningHours is null)
        {
            errors[nameof(request.OpeningHours)] = ["Gli orari di apertura sono obbligatori; usa una lista vuota per indicare uno spazio sempre chiuso."];
            return errors;
        }

        if (request.OpeningHours.Any(hours => hours is null))
        {
            errors[nameof(request.OpeningHours)] = ["La lista degli orari non può contenere valori nulli."];
            return errors;
        }

        if (request.OpeningHours.Any(hours => !Enum.IsDefined(hours.DayOfWeek)))
        {
            errors[nameof(request.OpeningHours)] = ["Il giorno della settimana non è valido."];
        }
        else if (request.OpeningHours.GroupBy(hours => hours.DayOfWeek).Any(group => group.Count() > 1))
        {
            errors[nameof(request.OpeningHours)] = ["È ammessa una sola fascia di apertura per giorno."];
        }
        else if (request.OpeningHours.Any(hours => hours.ClosesAt <= hours.OpensAt))
        {
            errors[nameof(request.OpeningHours)] = ["L'orario di chiusura deve essere successivo all'apertura."];
        }

        return errors;
    }

    private static List<OpeningHours> CreateOpeningHours(IReadOnlyList<RoomOpeningHours> openingHours) => openingHours
        .Select(hours => new OpeningHours
        {
            Id = Guid.NewGuid(),
            DayOfWeek = hours.DayOfWeek,
            OpensAt = hours.OpensAt,
            ClosesAt = hours.ClosesAt
        })
        .ToList();

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
