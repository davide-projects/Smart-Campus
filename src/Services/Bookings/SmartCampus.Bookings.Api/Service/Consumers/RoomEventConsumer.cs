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
        UpsertAsync(context.Message.RoomId, context.Message.Name, context.Message.Capacity,
            context.Message.IsBookable, isActive: true, context.CancellationToken);

    public Task Consume(ConsumeContext<RoomUpdated> context) =>
        UpsertAsync(context.Message.RoomId, context.Message.Name, context.Message.Capacity,
            context.Message.IsBookable, context.Message.IsActive, context.CancellationToken);

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
    private async Task UpsertAsync(Guid roomId, string name, int capacity, bool isBookable, bool isActive, CancellationToken cancellationToken)
    {
        var room = await db.BookableRooms.FindAsync([roomId], cancellationToken);

        if (room is null)
        {
            room = new BookableRoom { RoomId = roomId, Name = name };
            db.BookableRooms.Add(room);
        }

        room.Name = name;
        room.Capacity = capacity;
        room.IsBookable = isBookable;
        room.IsActive = isActive;   // copia sempre lo stato reale di Rooms

        await db.SaveChangesAsync(cancellationToken);
    }
}