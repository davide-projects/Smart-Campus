using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Features.Rooms;

// Filtri della ricerca pubblica. Tutti opzionali: se non ci sono, si vedono tutti gli spazi attivi.
public sealed record RoomFilter(RoomType? Type = null, int? MinCapacity = null, int? MaxCapacity = null, Guid? BuildingId = null);
