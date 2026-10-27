using SmartCampus.Contracts;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Features.Rooms;

public sealed record RoomWriteRequest(
    Guid BuildingId,
    string Name,
    RoomType Type,
    int Capacity,
    int Floor,
    bool IsBookable,
    IReadOnlyList<RoomOpeningHours> OpeningHours);
