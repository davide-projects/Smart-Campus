namespace SmartCampus.Rooms.Domain;

// Spazio del campus: aula, laboratorio o sala studio.
// IsActive = false significa dismesso: resta in archivio ma non compare nella ricerca (US-01, US-11).
public class Room
{
    public Guid Id { get; set; }

    public Guid BuildingId { get; set; }

    public Building Building { get; set; } = null!;

    public required string Name { get; set; }

    public RoomType Type { get; set; }

    public int Capacity { get; set; }

    public int Floor { get; set; }

    // Se false lo spazio resta visibile ma non può essere prenotato (US-10).
    public bool IsBookable { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public ICollection<OpeningHours> OpeningHours { get; set; } = [];

    public ICollection<RoomEquipment> RoomEquipment { get; set; } = [];
}
