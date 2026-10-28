using SmartCampus.Contracts;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

internal static class RoomEventFactory
{
    public static RoomCreated ToCreatedEvent(Room room) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.IsBookable)
    {
        OpeningHours = ToOpeningHours(room)
    };

    public static IReadOnlyList<RoomOpeningHours> ToOpeningHours(Room room) => room.OpeningHours
        .OrderBy(hours => hours.DayOfWeek)
        .Select(hours => new RoomOpeningHours(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
        .ToArray();
}
