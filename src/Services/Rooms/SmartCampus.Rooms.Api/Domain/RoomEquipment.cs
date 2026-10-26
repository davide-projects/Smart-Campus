namespace SmartCampus.Rooms.Domain;

// Relazione molti-a-molti tra spazi e attrezzature.
// Esplicita perché serve anche per il seed e per configurare i vincoli di cancellazione.
public class RoomEquipment
{
    public Guid RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public Guid EquipmentId { get; set; }

    public Equipment Equipment { get; set; } = null!;
}
