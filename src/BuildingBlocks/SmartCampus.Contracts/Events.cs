namespace SmartCampus.Contracts;

// Solo i messaggi scambiati tra servizi tramite broker.
// NON inserire qui entità di dominio o classi EF: ogni servizio possiede il proprio modello.

// Pubblicati da Rooms, consumati da Bookings (copia locale BookableRoom).
public record RoomCreated(Guid RoomId, string Name, int Capacity, bool IsBookable);
public record RoomUpdated(Guid RoomId, string Name, int Capacity, bool IsBookable, bool IsActive);
public record RoomDeactivated(Guid RoomId);
