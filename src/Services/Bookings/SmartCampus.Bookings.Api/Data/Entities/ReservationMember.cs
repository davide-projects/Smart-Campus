namespace SmartCampus.Bookings.Api.Entities;

public class ReservationMember
{
    public Guid ReservationId  { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid UserId { get; set; }

    // Nome del membro al momento della creazione, dalla verifica di Identity; null se Bookings non lo conosce.
    public string? DisplayName { get; set; }
}
