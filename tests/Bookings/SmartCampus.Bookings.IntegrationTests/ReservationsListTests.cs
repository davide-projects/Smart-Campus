using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

[Collection(BookingsApiCollection.Name)]
public sealed class ReservationsListTests(BookingsApiFixture fixture)
{
    // Giugno: in Italia l'offset è +02:00, lontano dai passaggi di ora legale.
    private static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    private static readonly DateOnly Day = new(2031, 6, 10);

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // Inserisce i dati di prova direttamente nel database del test.
    private async Task SeedAsync(Func<BookingDbContext, Task> action)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<BookingDbContext>());
    }

    [Fact]
    public async Task List_ReturnsTheReservationsOfTheDayInTimeOrder_WithCancelledOnesMarked()
    {
        var room = NewRoom();
        var otherRoom = NewRoom();
        var organizerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var eleven = NewReservation(room, At(Day, 11), ReservationStatus.Confirmed);
        eleven.OrganizerId = organizerId;
        eleven.Participants = 2;
        eleven.Members.Add(new ReservationMember { UserId = memberId });

        await SeedAsync(async db =>
        {
            db.BookableRooms.AddRange(room, otherRoom);
            db.Reservations.AddRange(
                NewReservation(room, At(Day, 14), ReservationStatus.Cancelled),            // cancellata: presente e marcata
                eleven,                                                                    // inserita fuori ordine
                NewReservation(room, At(Day, 9), ReservationStatus.Confirmed),
                NewReservation(room, At(Day.AddDays(-1), 9), ReservationStatus.Confirmed), // giorno prima: no
                NewReservation(room, At(Day.AddDays(1), 9), ReservationStatus.Confirmed),  // giorno dopo: no
                NewReservation(otherRoom, At(Day, 10), ReservationStatus.Confirmed));      // altro spazio: no
            await db.SaveChangesAsync();
        });

        var items = await GetListAsync(room.RoomId, Day);

        Assert.Equal(new[] { 9, 11, 14 }, items.Select(item => item.Start.ToOffset(CampusOffset).Hour).ToArray());
        Assert.Equal(new[] { "Confirmed", "Confirmed", "Cancelled" }, items.Select(item => item.Status).ToArray());
        Assert.Equal(organizerId, items[1].OrganizerId);
        Assert.Equal(2, items[1].Participants);
        Assert.Equal(new[] { memberId }, items[1].MemberIds.ToArray());
    }

    [Fact]
    public async Task List_IsEmptyWhenTheDayHasNoReservations()
    {
        var room = NewRoom();
        await SeedAsync(async db =>
        {
            db.BookableRooms.Add(room);
            await db.SaveChangesAsync();
        });

        var items = await GetListAsync(room.RoomId, Day);

        Assert.Empty(items);
    }

    [Fact]
    public async Task List_ReturnsNotFoundForUnknownRoom()
    {
        var response = await _client.GetAsync($"/api/bookings?roomId={Guid.NewGuid()}&date={Day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<ReservationListItem>> GetListAsync(Guid roomId, DateOnly date)
    {
        var response = await _client.GetAsync($"/api/bookings?roomId={roomId}&date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ReservationListItem>>())!;
    }
    private static DateTimeOffset At(DateOnly day, int hour) =>
        new(day.ToDateTime(new TimeOnly(hour, 0)), CampusOffset);

    private static BookableRoom NewRoom() => new()
    {
        RoomId = Guid.NewGuid(),
        Name = $"Test room {Guid.NewGuid():N}",
        Capacity = 30,
        IsBookable = true,
        IsActive = true
    };

    private static Reservation NewReservation(BookableRoom room, DateTimeOffset start, ReservationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = room.RoomId,
        OrganizerId = Guid.NewGuid(),
        Start = start,
        End = start.AddHours(1),
        Participants = 1,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        CancelledAt = status == ReservationStatus.Cancelled ? DateTimeOffset.UtcNow : null
    };
}
