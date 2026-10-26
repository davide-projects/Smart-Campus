namespace SmartCampus.Rooms.Domain;

// Attrezzatura che può essere presente in più spazi (proiettore, PC, lavagna...).
public class Equipment
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ICollection<RoomEquipment> Rooms { get; set; } = [];
}
