using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public BookableRoom Room { get; set; } = null!;
    public Guid OrganizerId { get; set; }
    public string? OrganizerName { get; set; } // Nome visualizzato al momento della creazione (claim "name"); null per le prenotazioni precedenti.
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int Participants { get; set; }
    public string? Purpose { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = []; // Versione della riga gestita da SQL Server: impedisce di sovrascrivere modifiche altrui.
    public ICollection<ReservationMember> Members { get; set; } = [];
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = []; // Al massimo uno (indice unico su ReservationId).
}
