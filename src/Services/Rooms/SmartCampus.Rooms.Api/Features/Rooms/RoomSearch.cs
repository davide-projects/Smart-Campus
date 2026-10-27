using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Features.Rooms;

// Regole della ricerca, separate dall'endpoint per poterle verificare con unit test
// su una lista in memoria, senza database.
public static class RoomSearch
{
    public static IQueryable<Room> Apply(IQueryable<Room> rooms, RoomFilter filter)
    {
        // Gli spazi dismessi non compaiono mai nella ricerca (US-01, US-11).
        var query = rooms.Where(room => room.IsActive);

        if (filter.Type is { } type)
        {
            query = query.Where(room => room.Type == type);
        }

        if (filter.MinCapacity is { } minCapacity)
        {
            query = query.Where(room => room.Capacity >= minCapacity);
        }

        if (filter.BuildingId is { } buildingId)
        {
            query = query.Where(room => room.BuildingId == buildingId);
        }

        return query;
    }
}
