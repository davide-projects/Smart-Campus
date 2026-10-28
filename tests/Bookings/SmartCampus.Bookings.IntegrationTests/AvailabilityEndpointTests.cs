using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;

namespace SmartCampus.Bookings.IntegrationTests;

// Criterio della #10: la disponibilità si legge dal database reale e la risposta
// non contiene mai dati dell'organizzatore.
[Collection(BookingsApiCollection.Name)]
public sealed class AvailabilityEndpointTests(BookingsApiFixture fixture)
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    [Fact]
    public async Task GetAvailability_ReturnsSlotsAndNeverExposesTheOrganizer()
    {
        // Arrange: aula aperta il martedì 08:00-10:00, prenotata 08:30-09:00
        var roomId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        await SeedAsync(roomId, organizerId);
        var client = fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/bookings/availability?roomId={roomId}&date=2026-11-03");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(organizerId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("organizer", json, StringComparison.OrdinalIgnoreCase);

        var availability = await response.Content.ReadFromJsonAsync<DailyAvailability>();
        Assert.NotNull(availability);
        Assert.True(availability.IsOpen);
        Assert.Equal([true, false, true, true], availability.Slots.Select(slot => slot.IsFree));
    }

    [Fact]
    public async Task GetAvailability_ForUnknownRoom_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/bookings/availability?roomId={Guid.NewGuid()}&date=2026-11-03");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedAsync(Guid roomId, Guid organizerId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var room = new BookableRoom { RoomId = roomId, Name = "Aula test", Capacity = 30, IsBookable = true, IsActive = true };
        room.OpeningHours.Add(new BookableRoomOpeningHours
        {
            RoomId = roomId,
            DayOfWeek = DayOfWeek.Tuesday,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(10, 0)
        });
        db.BookableRooms.Add(room);

        db.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            OrganizerId = organizerId,
            Start = At("08:30"),
            End = At("09:00"),
            Participants = 2,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static DateTimeOffset At(string time)
    {
        var local = Tuesday.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
