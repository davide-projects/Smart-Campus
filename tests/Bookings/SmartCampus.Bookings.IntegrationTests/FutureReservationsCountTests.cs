using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

public sealed class FutureReservationsCountTests(BookingsApiFixture fixture) : IClassFixture<BookingsApiFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // Inserisce i dati di prova direttamente nel database del test: l'API per creare prenotazioni non esiste ancora.
    private async Task SeedAsync(Func<BookingDbContext, Task> action)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<BookingDbContext>());
    }

    [Fact]
    public async Task Count_IncludesOnlyConfirmedReservationsNotYetEnded()
    {
        var room = NewRoom();
        var otherRoom = NewRoom();
        var now = DateTimeOffset.UtcNow;

        await SeedAsync(async db =>
        {
            db.BookableRooms.AddRange(room, otherRoom);
            db.Reservations.AddRange(
                NewReservation(room, now.AddDays(1), ReservationStatus.Confirmed),       // futura: contata
                NewReservation(room, now.AddDays(3), ReservationStatus.Confirmed),       // futura: contata
                NewReservation(room, now.AddMinutes(-30), ReservationStatus.Confirmed),  // in corso (finisce tra 30 minuti): contata
                NewReservation(room, now.AddDays(2), ReservationStatus.Cancelled),       // cancellata: no
                NewReservation(room, now.AddHours(-2), ReservationStatus.Confirmed),     // già terminata: no
                NewReservation(otherRoom, now.AddDays(1), ReservationStatus.Confirmed)); // altro spazio: no
            await db.SaveChangesAsync();
        });

        var result = await GetCountAsync(room.RoomId);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Count_IsZeroWhenTheRoomHasNoReservations()
    {
        var room = NewRoom();
        await SeedAsync(async db =>
        {
            db.BookableRooms.Add(room);
            await db.SaveChangesAsync();
        });

        var result = await GetCountAsync(room.RoomId);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Count_ReturnsNotFoundForUnknownRoom()
    {
        var response = await _client.GetAsync(
            $"/api/bookings/rooms/{Guid.NewGuid()}/future-reservations-count");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<FutureReservationsCountResponse> GetCountAsync(Guid roomId)
    {
        var response = await _client.GetAsync($"/api/bookings/rooms/{roomId}/future-reservations-count");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FutureReservationsCountResponse>())!;
    }

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
