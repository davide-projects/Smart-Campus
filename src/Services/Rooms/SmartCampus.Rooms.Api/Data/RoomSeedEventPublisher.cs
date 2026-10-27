using System.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Contracts;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

internal sealed class RoomSeedEventPublisher(
    RoomsDbContext db,
    IPublishEndpoint publishEndpoint)
{
    public async Task PublishOnceAsync(CancellationToken cancellationToken)
    {
        if (await db.RoomSeedEventDispatches.AnyAsync(cancellationToken))
        {
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            if (await db.RoomSeedEventDispatches.AnyAsync(cancellationToken))
            {
                return;
            }

            var rooms = await db.Rooms
                .Include(room => room.OpeningHours)
                .ToListAsync(cancellationToken);

            foreach (var room in rooms)
            {
                if (room.IsActive)
                {
                    await publishEndpoint.Publish(ToCreatedEvent(room), cancellationToken);
                }
                else
                {
                    await publishEndpoint.Publish(new RoomDeactivated(room.Id), cancellationToken);
                }
            }

            db.RoomSeedEventDispatches.Add(new RoomSeedEventDispatch
            {
                Id = 1,
                DispatchedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    internal static RoomCreated ToCreatedEvent(Room room) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.IsBookable)
    {
        OpeningHours = ToOpeningHours(room)
    };

    internal static IReadOnlyList<RoomOpeningHours> ToOpeningHours(Room room) => room.OpeningHours
        .OrderBy(hours => hours.DayOfWeek)
        .Select(hours => new RoomOpeningHours(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
        .ToArray();
}
