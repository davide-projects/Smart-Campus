namespace SmartCampus.Rooms.Domain;

// Edificio del campus: raggruppa gli spazi.
public class Building
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    // Codice breve usato dal personale (es. "A", "LIB").
    public required string Code { get; set; }

    public ICollection<Room> Rooms { get; set; } = [];
}
