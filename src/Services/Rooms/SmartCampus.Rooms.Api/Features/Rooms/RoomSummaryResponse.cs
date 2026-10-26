using SmartCampus.Rooms.Domain;

namespace SmartCampus.Rooms.Features.Rooms;

// Dati minimi richiesti da US-01: nome, tipo, edificio e capienza.
public sealed record RoomSummaryResponse(
    Guid Id,
    string Name,
    RoomType Type,
    int Capacity,
    int Floor,
    bool IsBookable,
    Guid BuildingId,
    string BuildingName);
