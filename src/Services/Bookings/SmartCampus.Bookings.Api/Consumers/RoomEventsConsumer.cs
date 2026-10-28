using MassTransit;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Contracts;

namespace SmartCampus.Bookings.Api.Consumers;

// Tiene aggiornata la copia locale degli spazi (BookableRoom) con gli eventi pubblicati da Rooms.
// È idempotente: ricevere lo stesso evento due volte produce lo stesso risultato di riceverlo una volta.
public sealed class RoomEventsConsumer(BookingDbContext db, ILogger<RoomEventsConsumer> logger) :
    IConsumer<RoomCreated>,
    IConsumer<RoomUpdated>,
    IConsumer<RoomDeactivated>
{
    public Task Consume(ConsumeContext<RoomCreated> context) =>
        UpsertAsync(context.Message.RoomId, context.Message.Name, context.Message.BuildingName, context.Message.Type, context.Message.Capacity,
            context.Message.IsBookable, isActive: true, context.Message.OpeningHours, context.CancellationToken);

    public Task Consume(ConsumeContext<RoomUpdated> context) =>
        UpsertAsync(context.Message.RoomId, context.Message.Name, context.Message.BuildingName, context.Message.Type, context.Message.Capacity,
            context.Message.IsBookable, context.Message.IsActive, context.Message.OpeningHours, context.CancellationToken);

    public async Task Consume(ConsumeContext<RoomDeactivated> context)
    {
        var room = await db.BookableRooms.FindAsync([context.Message.RoomId], context.CancellationToken);

        if (room is null)
        {
            // Spazio mai visto: non c'è niente da disattivare.
            logger.LogWarning("RoomDeactivated per lo spazio {RoomId} non presente nella copia locale.", context.Message.RoomId);
            return;
        }

        room.IsActive = false;
        await db.SaveChangesAsync(context.CancellationToken);
    }

    // "Upsert" = aggiorna se esiste, altrimenti crea. È questo che rende il consumer idempotente.
    private async Task UpsertAsync(
        Guid roomId,
        string name,
        string? buildingName,
        string? type,
        int capacity,
        bool isBookable,
        bool isActive,
        IReadOnlyList<RoomOpeningHours> openingHours,
        CancellationToken cancellationToken)
    {
        var room = await db.BookableRooms
            .Include(existing => existing.OpeningHours)
            .SingleOrDefaultAsync(existing => existing.RoomId == roomId, cancellationToken);

        if (room is null)
        {
            room = new BookableRoom { RoomId = roomId, Name = name };
            db.BookableRooms.Add(room);
        }

        room.Name = name;

        // Un evento senza edificio (accodato prima che Rooms lo inviasse) non cancella quello già noto.
        if (buildingName is not null)
        {
            room.BuildingName = buildingName;
        }

        // Stesso criterio per il tipo: un evento che non lo contiene non cancella quello già noto.
        if (type is not null)
        {
            room.Type = type;
        }

        room.Capacity = capacity;
        room.IsBookable = isBookable;
        room.IsActive = isActive;

        var requestedByDay = openingHours.ToDictionary(hours => hours.DayOfWeek);
        foreach (var existing in room.OpeningHours.ToArray())
        {
            if (!requestedByDay.TryGetValue(existing.DayOfWeek, out var updated))
            {
                room.OpeningHours.Remove(existing);
                db.BookableRoomOpeningHours.Remove(existing);
                continue;
            }

            existing.OpensAt = updated.OpensAt;
            existing.ClosesAt = updated.ClosesAt;
            requestedByDay.Remove(existing.DayOfWeek);
        }

        foreach (var hours in requestedByDay.Values)
        {
            room.OpeningHours.Add(new BookableRoomOpeningHours
            {
                RoomId = room.RoomId,
                Room = room,
                DayOfWeek = hours.DayOfWeek,
                OpensAt = hours.OpensAt,
                ClosesAt = hours.ClosesAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
