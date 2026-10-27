namespace SmartCampus.Bookings.Api.Entities;

public class ReservationMember
{
    public Guid ReservationId  { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid UserId { get; set; }

}
