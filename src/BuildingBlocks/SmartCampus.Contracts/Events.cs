namespace SmartCampus.Contracts;

// Solo i messaggi scambiati tra servizi tramite broker.
// NON inserire qui entità di dominio o classi EF: ogni servizio possiede il proprio modello.

public record RoomOpeningHours(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);

// Pubblicati da Rooms, consumati da Bookings (copia locale BookableRoom).
// BuildingName e Type sono facoltativi per compatibilità: un evento senza questi campi (per esempio
// accodato prima della loro introduzione) si legge con null. Rooms li valorizza sempre.
// Type è il nome del valore di RoomType in Rooms: "Classroom", "Laboratory" o "StudyRoom".
public record RoomCreated(Guid RoomId, string Name, int Capacity, bool IsBookable)
{
    public IReadOnlyList<RoomOpeningHours> OpeningHours { get; init; } = [];

    public string? BuildingName { get; init; }

    public string? Type { get; init; }
}

public record RoomUpdated(Guid RoomId, string Name, int Capacity, bool IsBookable, bool IsActive)
{
    public IReadOnlyList<RoomOpeningHours> OpeningHours { get; init; } = [];

    public string? BuildingName { get; init; }

    public string? Type { get; init; }
}

public record RoomDeactivated(Guid RoomId);
