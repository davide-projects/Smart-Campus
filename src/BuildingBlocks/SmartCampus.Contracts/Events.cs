namespace SmartCampus.Contracts;

// Solo i messaggi scambiati tra servizi tramite broker.
// NON inserire qui entità di dominio o classi EF: ogni servizio possiede il proprio modello.

public record RoomOpeningHours(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);

// Pubblicati da Rooms, consumati da Bookings (copia locale BookableRoom).
// BuildingName è facoltativo per compatibilità: un evento senza il campo (per esempio accodato
// prima della sua introduzione) si legge con null. Rooms lo valorizza sempre.
public record RoomCreated(Guid RoomId, string Name, int Capacity, bool IsBookable)
{
    public IReadOnlyList<RoomOpeningHours> OpeningHours { get; init; } = [];

    public string? BuildingName { get; init; }
}

public record RoomUpdated(Guid RoomId, string Name, int Capacity, bool IsBookable, bool IsActive)
{
    public IReadOnlyList<RoomOpeningHours> OpeningHours { get; init; } = [];

    public string? BuildingName { get; init; }
}

public record RoomDeactivated(Guid RoomId);
