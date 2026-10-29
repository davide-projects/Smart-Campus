using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// US-05: una prenotazione di gruppo creata con la POST compare tra le prenotazioni di ogni membro,
// e i membri non possono cancellarla né cambiarne i partecipanti.
[Collection(BookingsApiCollection.Name)]
public sealed class GroupReservationAccessTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private static readonly DateOnly Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).Date).AddDays(2);

    private readonly BookingsApiFixture _fixture;

    public GroupReservationAccessTests(BookingsApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Identity.Reset();
    }

    [Fact]
    public async Task EveryMemberFindsTheGroupReservationAndTheOrganizerSeesItOnce()
    {
        Guid mario = Guid.NewGuid(), anna = Guid.NewGuid(), luca = Guid.NewGuid();
        var reservationId = await CreateGroupAsync(mario, "Mario Rossi", anna, luca);

        foreach (var member in new[] { anna, luca })
        {
            var mine = await MineAsync(member);
            var item = Assert.Single(mine.Upcoming, reservation => reservation.Id == reservationId);
            Assert.True(item.IsGroup);
            Assert.False(item.IsOrganizer);
            Assert.Equal("Mario Rossi", item.OrganizerName);
        }

        var organizerView = await MineAsync(mario);
        var own = Assert.Single(organizerView.Upcoming, reservation => reservation.Id == reservationId);
        Assert.True(own.IsGroup);
        Assert.True(own.IsOrganizer);
    }

    [Fact]
    public async Task MemberCannotCancelTheGroupReservation()
    {
        Guid mario = Guid.NewGuid(), anna = Guid.NewGuid(), luca = Guid.NewGuid();
        var reservationId = await CreateGroupAsync(mario, "Mario Rossi", anna, luca);

        using var annaClient = _fixture.CreateClientFor(anna);
        var response = await annaClient.DeleteAsync($"/api/bookings/{reservationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Non puoi cancellare questa prenotazione.", problem.RootElement.GetProperty("detail").GetString());
        await AssertUnchangedAsync(reservationId, ReservationStatus.Confirmed, participants: 3, anna, luca);
    }

    [Theory]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    public async Task NoRouteChangesTheMembers(string method, bool asMember)
    {
        Guid mario = Guid.NewGuid(), anna = Guid.NewGuid(), luca = Guid.NewGuid();
        var reservationId = await CreateGroupAsync(mario, "Mario Rossi", anna, luca);

        using var client = _fixture.CreateClientFor(asMember ? anna : mario);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/bookings/{reservationId}")
        {
            Content = JsonContent.Create(new { memberIds = new[] { Guid.NewGuid() }, participants = 2 })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await AssertUnchangedAsync(reservationId, ReservationStatus.Confirmed, participants: 3, anna, luca);
    }

    // --- Dati di prova e verifiche ---

    private async Task<Guid> CreateGroupAsync(Guid organizerId, string organizerName, params Guid[] members)
    {
        var roomId = await SeedRoomAsync();
        using var client = _fixture.CreateClientFor(organizerId, organizerName);

        var response = await client.PostAsJsonAsync(
            "/api/bookings", new CreateReservationRequest(roomId, At("10:00"), At("11:00"), null, members));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Id;
    }

    private async Task<MyReservationsResponse> MineAsync(Guid userId)
    {
        using var client = _fixture.CreateClientFor(userId);
        return (await client.GetFromJsonAsync<MyReservationsResponse>("/api/bookings/mine", Json))!;
    }

    private async Task AssertUnchangedAsync(Guid reservationId, ReservationStatus status, int participants, params Guid[] members)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var reservation = await db.Reservations
            .AsNoTracking()
            .Include(r => r.Members)
            .SingleAsync(r => r.Id == reservationId);

        Assert.Equal(status, reservation.Status);
        Assert.Equal(participants, reservation.Participants);
        Assert.Equal(members.Order(), reservation.Members.Select(member => member.UserId).Order());
    }

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

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.BookableRooms.Add(room);
        await db.SaveChangesAsync();

        return roomId;
    }

    private static DateTimeOffset At(string time)
    {
        var local = Day.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
