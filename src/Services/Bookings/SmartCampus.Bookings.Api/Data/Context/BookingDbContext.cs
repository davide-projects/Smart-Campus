using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Context;

public class BookingDbContext(DbContextOptions<BookingDbContext> options)
    : DbContext(options)
{
    public DbSet<BookableRoom> BookableRooms => Set<BookableRoom>();
    public DbSet<BookableRoomOpeningHours> BookableRoomOpeningHours => Set<BookableRoomOpeningHours>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationMember> ReservationMembers => Set<ReservationMember>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
