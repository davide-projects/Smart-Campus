using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri di US-04 verificati sull'endpoint reale con SQL Server (Testcontainers).
[Collection(BookingsApiCollection.Name)]
public sealed class CreateReservationTests(BookingsApiFixture fixture)
{
    // Stesse opzioni JSON dell'API: lo stato arriva come stringa ("Confirmed").
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
        var created = await response.Content.ReadFromJsonAsync<ReservationResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal(roomId, created.RoomId);
        Assert.Equal(At("10:00"), created.Start);
        Assert.Equal(ReservationStatus.Confirmed, created.Status);

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
    public async Task Create_WithNameClaim_Returns201AndSavesTheOrganizerName()
    {
        var roomId = await SeedRoomAsync();

        var response = await ClientFor(Guid.NewGuid(), "Mario Rossi").PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ReservationResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal(1, created.Participants);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var saved = await db.Reservations.AsNoTracking().SingleAsync(reservation => reservation.Id == created.Id);
        Assert.Equal("Mario Rossi", saved.OrganizerName);
    }

    [Fact]
    public async Task Create_WithTokenSignedWithAnotherKey_Returns401AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        var token = BookingsApiFixture.CreateToken(Guid.NewGuid().ToString(), signingKey: BookingsApiFixture.OtherSigningKey);

        var response = await PostWithTokenAsync(token, roomId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Fact]
    public async Task Create_WithUnsignedToken_Returns401AndCreatesNothing()
    {
        // Prima la firma non veniva verificata: bastava un token "alg: none" con un sub qualsiasi per prenotare a nome di altri.
        var roomId = await SeedRoomAsync();

        var response = await PostWithTokenAsync(UnsignedToken(Guid.NewGuid()), roomId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Fact]
    public async Task Create_WithExpiredToken_Returns401AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        var token = BookingsApiFixture.CreateToken(Guid.NewGuid().ToString(), lifetime: TimeSpan.FromMinutes(-10));

        var response = await PostWithTokenAsync(token, roomId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_WithoutNameClaim_Returns401AndCreatesNothing(string? name)
    {
        var roomId = await SeedRoomAsync();

        var response = await ClientFor(Guid.NewGuid(), name).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accesso richiesto", body);
        Assert.Contains("Effettua l'accesso per prenotare.", body);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Fact]
    public async Task Create_NameClaimTooLong_Returns401AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        var name = new string('a', ReservationEndpoints.MaxUserNameLength + 1);

        var response = await ClientFor(Guid.NewGuid(), name).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accesso richiesto", body);
        Assert.Contains("Effettua l'accesso per prenotare.", body);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("non-e-un-guid")]
    public async Task Create_WithValidTokenButNoUserId_Returns401AndCreatesNothing(string? subject)
    {
        // Token firmato correttamente, ma senza un "sub" che identifichi un utente.
        var roomId = await SeedRoomAsync();

        var response = await PostWithTokenAsync(BookingsApiFixture.CreateToken(subject), roomId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountReservationsAsync(roomId));
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

    // Client con un token valido, firmato con la chiave che Bookings usa nei test.
    private HttpClient ClientFor(Guid userId, string? name = BookingsApiFixture.DefaultUserName) => fixture.CreateClientFor(userId, name);

    private async Task<HttpResponseMessage> PostWithTokenAsync(string token, Guid roomId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));
    }

    // Token senza firma ("alg": "none"): per un controllo serio non deve essere accettato.
    private static string UnsignedToken(Guid userId)
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
