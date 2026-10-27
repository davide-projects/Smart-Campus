namespace SmartCampus.Bookings.Api.Entities;

public class BookableRoomOpeningHours
{
    public Guid RoomId { get; set; }
    public BookableRoom Room { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}