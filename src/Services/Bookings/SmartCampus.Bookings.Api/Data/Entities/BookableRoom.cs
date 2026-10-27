namespace SmartCampus.Bookings.Api.Entities;

public class BookableRoom
{
    public Guid RoomId { get; set; }
    public required string Name { get; set; }
    public int Capacity { get; set; }
    public bool IsBookable { get; set; }
    public bool IsActive { get; set; }
    public ICollection<BookableRoomOpeningHours> OpeningHours { get; set; } = [];
}