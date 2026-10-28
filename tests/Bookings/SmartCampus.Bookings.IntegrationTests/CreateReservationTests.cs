using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri di US-04 verificati sull'endpoint reale con SQL Server (Testcontainers).
// La concorrenza (20 richieste parallele) è nel task #14.
[Collection(BookingsApiCollection.Name)]
public sealed class CreateReservationTests(BookingsApiFixture fixture)
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // Dopodomani: sempre nel futuro e sempre entro i 14 giorni (A2), qualunque sia la data di esecuzione.
    private static readonly DateOnly Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).Date).AddDays(2);

    [Fact]
    public async Task Create_FreeSlot_Returns201AndTheSlotBecomesOccupied()
    {
        var roomId = await SeedRoomAsync();
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(created);
        Assert.Equal(roomId, created.RoomId);
        Assert.Equal(At("10:00"), created.Start);

        var availability = await client.GetFromJsonAsync<DailyAvailability>(
            $"/api/bookings/availability?roomId={roomId}&date={Day:yyyy-MM-dd}");
        Assert.False(availability!.Slots.Single(slot => slot.Start == At("10:00")).IsFree);
        Assert.False(availability.Slots.Single(slot => slot.Start == At("10:30")).IsFree);
    }

    [Fact]
    public async Task Create_PartiallyOverlapping_Returns409AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "10:30", "11:30"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountReservationsAsync(roomId));
    }

    [Fact]
    public async Task Create_StartingExactlyWhenAnotherEnds_Returns201()
    {
        var roomId = await SeedRoomAsync();
        await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "11:00", "12:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameOrganizerInAnotherRoomAtTheSameTime_Returns409()
    {
        var firstRoom = await SeedRoomAsync();
        var secondRoom = await SeedRoomAsync();
        var client = ClientFor(Guid.NewGuid());
        await client.PostAsJsonAsync("/api/bookings", Request(firstRoom, "10:00", "11:00"));

        var response = await client.PostAsJsonAsync("/api/bookings", Request(secondRoom, "10:30", "11:30"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountReservationsAsync(secondRoom));
    }

    [Fact]
    public async Task Create_OutsideOpeningHours_Returns422WithTheOpeningTimes()
    {
        var roomId = await SeedRoomAsync();

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "19:00", "21:00"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("aperto dalle 08:00 alle 20:00", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var roomId = await SeedRoomAsync();

        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownRoom_Returns404()
    {
        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(Guid.NewGuid(), "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Dati di prova ---

    // Spazio aperto tutti i giorni 08:00-20:00, con un Id nuovo per ogni test.
    private async Task<Guid> SeedRoomAsync()
    {
        var roomId = Guid.NewGuid();
        var room = new BookableRoom { RoomId = roomId, Name = "Aula test", Capacity = 30, IsBookable = true, IsActive = true };

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            room.OpeningHours.Add(new BookableRoomOpeningHours
            {
                RoomId = roomId,
                DayOfWeek = day,
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(20, 0)
            });
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.BookableRooms.Add(room);
        await db.SaveChangesAsync();

        return roomId;
    }

    private async Task<int> CountReservationsAsync(Guid roomId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.CountAsync(reservation => reservation.RoomId == roomId);
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken(userId));
        return client;
    }

    // Token di prova con il solo claim "sub". La firma è finta: Bookings non la verifica (lo fa il Gateway).
    private static string TestToken(Guid userId)
    {
        static string Encode(object value) =>
            Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));

        return $"{Encode(new { alg = "none", typ = "JWT" })}.{Encode(new { sub = userId.ToString(), role = "Student" })}.test";
    }

    private static CreateReservationRequest Request(Guid roomId, string start, string end) =>
        new(roomId, At(start), At(end), Purpose: null);

    private static DateTimeOffset At(string time)
    {
        var local = Day.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
