namespace SmartCampus.Rooms.Data;

internal sealed record EquipmentRecord(Guid Id, string Name);

internal sealed record RoomEquipmentAssignment(Guid RoomId, Guid EquipmentId);
