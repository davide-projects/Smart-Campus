using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Entities;

// Record di presenza di una prenotazione: al massimo uno per prenotazione, valido per tutto il gruppo.
public class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public AttendanceType Type { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Guid RecordedBy { get; set; } // Claim "sub" di chi ha registrato il record: studente o Staff.
}
