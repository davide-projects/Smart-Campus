namespace SmartCampus.Rooms.Models;

// Fascia di apertura di uno spazio in un giorno della settimana.
// Lo spazio è prenotabile solo dentro queste fasce (lo verificherà Bookings).
public class OpeningHours
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }
}
