using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Features.Rooms;

public sealed record RoomDetailResponse(
    Guid Id,
    string Name,
    RoomType Type,
    Guid BuildingId,
    string BuildingName,
    string BuildingCode,
    int Floor,
    int Capacity,
    bool IsBookable,
    IReadOnlyList<RoomEquipmentResponse> Equipment,
    IReadOnlyList<OpeningHoursResponse> OpeningHours);

public sealed record RoomEquipmentResponse(Guid Id, string Name);

public sealed record OpeningHoursResponse(
    DayOfWeek DayOfWeek,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt,
    bool IsClosed);
