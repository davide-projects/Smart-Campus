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
            .RequireAuthorization(policy => policy.RequireRole("Staff"))
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        api.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateRoom")
            .WithTags("Rooms")
            .RequireAuthorization(policy => policy.RequireRole("Staff"))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        api.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateRoom")
            .WithTags("Rooms")
            .RequireAuthorization(policy => policy.RequireRole("Staff"))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> CreateAsync(
        RoomWriteRequest request,
        RoomsDbContext db,
        EquipmentStore equipmentStore,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateWriteRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        // Il nome dell'edificio serve all'evento; se manca, l'edificio non esiste.
        var buildingName = await db.Buildings
            .Where(building => building.Id == request.BuildingId)
            .Select(building => building.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (buildingName is null)
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

        var equipment = await equipmentStore.GetByIdsAsync(request.EquipmentIds, cancellationToken);
        if (equipment.Count != request.EquipmentIds.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EquipmentIds)] = ["Una o più attrezzature indicate non esistono."]
            });
        }

        var roomId = Guid.NewGuid();
        var room = new Room
        {
            Id = roomId,
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
        await publishEndpoint.Publish(RoomEventFactory.ToCreatedEvent(room, buildingName), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        // Le attrezzature vanno su MongoDB solo dopo il salvataggio SQL: se MongoDB non risponde
        // lo spazio resta comunque registrato. Le due scritture non sono atomiche (limitazione
        // dichiarata nella descrizione della PR).
        await equipmentStore.SetRoomEquipmentAsync(roomId, equipment, cancellationToken);

        return Results.Created($"/api/rooms/{room.Id}", new { room.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        RoomWriteRequest request,
        RoomsDbContext db,
        EquipmentStore equipmentStore,
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

        // Il nome dell'edificio serve all'evento; se manca, l'edificio non esiste.
        var buildingName = await db.Buildings
            .Where(building => building.Id == request.BuildingId)
            .Select(building => building.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (buildingName is null)
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

        var equipment = await equipmentStore.GetByIdsAsync(request.EquipmentIds, cancellationToken);
        if (equipment.Count != request.EquipmentIds.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EquipmentIds)] = ["Una o più attrezzature indicate non esistono."]
            });
        }

        room.BuildingId = request.BuildingId;
        room.Name = roomName;
        room.Type = request.Type;
        room.Capacity = request.Capacity;
        room.Floor = request.Floor;
        room.IsBookable = request.IsBookable;
        UpdateOpeningHours(room, request.OpeningHours, db);

        await publishEndpoint.Publish(RoomEventFactory.ToUpdatedEvent(room, buildingName), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        // Come in CreateAsync: prima SQL, poi MongoDB. Le due scritture non sono atomiche.
        await equipmentStore.SetRoomEquipmentAsync(room.Id, equipment, cancellationToken);

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

        if (request.EquipmentIds is null || request.EquipmentIds.Any(id => id == Guid.Empty))
        {
            errors[nameof(request.EquipmentIds)] = ["La lista delle attrezzature è obbligatoria e non può contenere identificativi vuoti."];
        }
        else if (request.EquipmentIds.Distinct().Count() != request.EquipmentIds.Count)
        {
            errors[nameof(request.EquipmentIds)] = ["Un'attrezzatura non può essere indicata più di una volta."];
        }

        if (request.OpeningHours is null)
        {
            errors[nameof(request.OpeningHours)] = ["Gli orari sono obbligatori; usa una lista vuota per indicare uno spazio sempre chiuso."];
            return errors;
        }

        if (request.OpeningHours.Any(hours => hours is null || !Enum.IsDefined(hours.DayOfWeek)))
        {
            errors[nameof(request.OpeningHours)] = ["Gli orari contengono un giorno non valido."];
            return errors;
        }

        if (request.OpeningHours.GroupBy(hours => hours.DayOfWeek).Any(group => group.Count() > 1))
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

    private static void UpdateOpeningHours(Room room, IReadOnlyList<RoomOpeningHours> requestedHours, RoomsDbContext db)
    {
        var requestedByDay = requestedHours.ToDictionary(hours => hours.DayOfWeek);
        foreach (var existing in room.OpeningHours.ToArray())
        {
            if (!requestedByDay.TryGetValue(existing.DayOfWeek, out var updated))
            {
                room.OpeningHours.Remove(existing);
                db.OpeningHours.Remove(existing);
                continue;
            }

            existing.OpensAt = updated.OpensAt;
            existing.ClosesAt = updated.ClosesAt;
            requestedByDay.Remove(existing.DayOfWeek);
        }

        foreach (var hours in requestedByDay.Values)
        {
            db.OpeningHours.Add(new OpeningHours
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Room = room,
                DayOfWeek = hours.DayOfWeek,
                OpensAt = hours.OpensAt,
                ClosesAt = hours.ClosesAt
            });
        }
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        RoomsDbContext db,
        EquipmentStore equipmentStore,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms
            .AsNoTracking()
            .Where(room => room.Id == id && room.IsActive)
            .Include(room => room.Building)
            .Include(room => room.OpeningHours)
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

        var equipment = (await equipmentStore.GetForRoomAsync(room.Id, cancellationToken))
            .Select(item => new RoomEquipmentResponse(item.Id, item.Name))
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
        [FromQuery] int? maxCapacity,
        [FromQuery] Guid? buildingId,
        [FromQuery] Guid[]? equipmentIds,
        RoomsDbContext db,
        EquipmentStore equipmentStore,
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

        if (equipmentIds?.Any(id => id == Guid.Empty) == true)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["equipmentIds"] = ["Gli identificativi delle attrezzature non sono validi."]
            });
        }

        var matchingRoomIds = equipmentIds is null || equipmentIds.Length == 0
            ? null
            : await equipmentStore.FindRoomIdsWithAllEquipmentAsync(equipmentIds, cancellationToken);

        var rooms = await RoomSearch
            .ApplyEquipmentFilter(
                RoomSearch.Apply(db.Rooms.AsNoTracking(), new RoomFilter(type, minCapacity, maxCapacity, buildingId)),
                matchingRoomIds?.ToArray())
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

        var equipmentByRoom = await equipmentStore.GetForRoomsAsync(
            rooms.Select(room => room.Id).ToArray(),
            cancellationToken);
        var results = rooms
            .Select(room => room with
            {
                Equipment = equipmentByRoom.TryGetValue(room.Id, out var equipment)
                    ? equipment.Select(item => new RoomEquipmentResponse(item.Id, item.Name)).OrderBy(item => item.Name).ToArray()
                    : []
            })
            .ToArray();

        return Results.Ok(results);
    }
}
