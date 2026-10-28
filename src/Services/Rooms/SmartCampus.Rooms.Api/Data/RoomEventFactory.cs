using SmartCampus.Contracts;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

// Unico punto in cui si costruiscono RoomCreated e RoomUpdated, così il contenuto degli eventi
// (compreso il nome dell'edificio) è lo stesso in creazione, modifica e pubblicazione seed.
internal static class RoomEventFactory
{
    public static RoomCreated ToCreatedEvent(Room room, string buildingName) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.IsBookable)
    {
        OpeningHours = ToOpeningHours(room),
        BuildingName = buildingName
    };

    public static RoomUpdated ToUpdatedEvent(Room room, string buildingName) => new(
        room.Id,
        room.Name,
        room.Capacity,
        room.IsBookable,
        room.IsActive)
    {
        OpeningHours = ToOpeningHours(room),
        BuildingName = buildingName
    };

    private static IReadOnlyList<RoomOpeningHours> ToOpeningHours(Room room) => room.OpeningHours
        .OrderBy(hours => hours.DayOfWeek)
        .Select(hours => new RoomOpeningHours(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
        .ToArray();
}
