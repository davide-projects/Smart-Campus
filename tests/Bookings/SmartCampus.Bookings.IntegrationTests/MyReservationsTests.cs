using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri del task #23 (US-07) su GET /api/bookings/mine, con SQL Server reale e "adesso" fissato.
// I dati si inseriscono direttamente nel database: oggi nessuna API crea membri (US-05).
// Ogni test usa utenti nuovi, così non vede le prenotazioni degli altri test.
[Collection(BookingsApiCollection.Name)]
public sealed class MyReservationsTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // "Adesso" fisso: giovedì 5 novembre 2026, 10:30 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 30, 0, TimeSpan.Zero);

    private readonly BookingsApiFixture _fixture;

    public MyReservationsTests(BookingsApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Clock.Set(Now);
    }

    // Riporta l'orologio all'ora reale per le altre classi della collezione.
    public void Dispose() => _fixture.Clock.Reset();

    [Fact]
    public async Task Mine_ReservationOfSomeoneElse_IsNotVisible()
    {
        var room = await SeedRoomAsync();
        var organizer = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var member = Guid.NewGuid();
        var others = await SeedAsync(
            NewReservation(room, organizer, Now.AddDays(1)),
            NewReservation(room, organizer, Now.AddDays(-1)),
            NewReservation(room, organizer, Now.AddDays(2), ReservationStatus.Cancelled),
            NewReservation(room, organizer, Now.AddDays(3), members: [member]));
        var own = await SeedAsync(NewReservation(room, stranger, Now.AddDays(4)));

        var result = await GetMineAsync(stranger);

        var all = result.Upcoming.Concat(result.Past).Concat(result.Cancelled).Select(item => item.Id).ToList();
        Assert.Equal(own.Select(reservation => reservation.Id), all);
        Assert.DoesNotContain(all, id => others.Any(reservation => reservation.Id == id));
    }

    [Fact]
    public async Task Mine_AsMember_ShowsTheGroupReservationWithTheOrganizer()
    {
        var room = await SeedRoomAsync();
        var mario = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var reservations = await SeedAsync(
            NewReservation(room, mario, Now.AddDays(1), organizerName: "Mario Rossi", members: [anna]));

        var result = await GetMineAsync(anna);

        var item = Assert.Single(result.Upcoming);
        Assert.Equal(reservations[0].Id, item.Id);
        Assert.True(item.IsGroup);
        Assert.Equal("Mario Rossi", item.OrganizerName);
        Assert.False(item.IsOrganizer);
        Assert.Empty(result.Past);
        Assert.Empty(result.Cancelled);
    }

    [Fact]
    public async Task Mine_AsOrganizerOfAGroup_ShowsTheReservationOnce()
    {
        var room = await SeedRoomAsync();
        var organizer = Guid.NewGuid();
        var reservations = await SeedAsync(
            NewReservation(room, organizer, Now.AddDays(1), organizerName: "Mario Rossi", members: [Guid.NewGuid(), Guid.NewGuid()]));

        var result = await GetMineAsync(organizer);

        var item = Assert.Single(result.Upcoming);
        Assert.Equal(reservations[0].Id, item.Id);
        Assert.True(item.IsGroup);
        Assert.True(item.IsOrganizer);
        Assert.Empty(result.Past);
        Assert.Empty(result.Cancelled);
    }

    [Fact]
    public async Task Mine_Upcoming_IsInAscendingOrderAndIncludesTheOneInProgress()
    {
        var room = await SeedRoomAsync();
        var user = Guid.NewGuid();
        var dayAfterTomorrow = NewReservation(room, user, Now.AddDays(2).AddMinutes(-30)); // dopodomani alle 10:00
        var tomorrow = NewReservation(room, user, Now.AddDays(1).AddHours(3.5));           // domani alle 14:00
        var inProgress = NewReservation(room, user, Now.AddMinutes(-30)); // 10:00-11:00, ora sono le 10:30
        await SeedAsync(dayAfterTomorrow, tomorrow, inProgress);

        var result = await GetMineAsync(user);

        Assert.Equal([inProgress.Id, tomorrow.Id, dayAfterTomorrow.Id], result.Upcoming.Select(item => item.Id));
        Assert.Empty(result.Past);
    }

    [Fact]
    public async Task Mine_Past_IsInDescendingOrderAndIncludesTheOneEndingNow()
    {
        var room = await SeedRoomAsync();
        var user = Guid.NewGuid();
        var lastWeek = NewReservation(room, user, Now.AddDays(-7));
        var endingNow = NewReservation(room, user, Now.AddHours(-1)); // 09:30-10:30: finisce esattamente adesso
        var yesterday = NewReservation(room, user, Now.AddDays(-1));
        await SeedAsync(lastWeek, endingNow, yesterday);

        var result = await GetMineAsync(user);

        Assert.Equal([endingNow.Id, yesterday.Id, lastWeek.Id], result.Past.Select(item => item.Id));
        Assert.Empty(result.Upcoming);
    }

    [Fact]
    public async Task Mine_Cancelled_AreOnlyInCancelledInDescendingOrder()
    {
        var room = await SeedRoomAsync();
        var user = Guid.NewGuid();
        var lastWeek = NewReservation(room, user, Now.AddDays(-7), ReservationStatus.Cancelled);
        var tomorrow = NewReservation(room, user, Now.AddDays(1), ReservationStatus.Cancelled);
        await SeedAsync(lastWeek, tomorrow);

        var result = await GetMineAsync(user);

        Assert.Equal([tomorrow.Id, lastWeek.Id], result.Cancelled.Select(item => item.Id));
        Assert.Empty(result.Upcoming);
        Assert.Empty(result.Past);
    }

    [Fact]
    public async Task Mine_CompleteItem_ContainsRoomBuildingTimesAndOrganizer()
    {
        var room = await SeedRoomAsync("Aula A1", "Edificio A");
        var mario = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.FromHours(1)); // domani 10:00 ora di Roma
        var reservation = NewReservation(room, mario, start, organizerName: "Mario Rossi");
        await SeedAsync(reservation);

        var result = await GetMineAsync(mario);

        var item = Assert.Single(result.Upcoming);
        Assert.Equal(reservation.Id, item.Id);
        Assert.Equal(room.RoomId, item.RoomId);
        Assert.Equal("Aula A1", item.RoomName);
        Assert.Equal("Edificio A", item.BuildingName);
        Assert.Equal(start, item.Start);
        Assert.Equal(start.AddHours(1), item.End);
        Assert.False(item.IsGroup);
        Assert.Equal("Mario Rossi", item.OrganizerName);
        Assert.True(item.IsOrganizer);
    }

    [Fact]
    public async Task Mine_ItemWithoutBuildingAndOrganizerName_ReturnsNullForThem()
    {
        var room = await SeedRoomAsync("Aula senza edificio", buildingName: null);
        var user = Guid.NewGuid();
        await SeedAsync(NewReservation(room, user, Now.AddDays(1), organizerName: null));

        var response = await ClientFor(user).GetAsync("/api/bookings/mine");

        // Si controlla il JSON grezzo: i campi devono esserci e valere null, non mancare.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = body.RootElement.GetProperty("upcoming").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("buildingName").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("organizerName").ValueKind);
        Assert.Equal("Aula senza edificio", item.GetProperty("roomName").GetString());
        Assert.True(item.GetProperty("isOrganizer").GetBoolean());
    }

    [Fact]
    public async Task Mine_WithoutReservations_Returns200WithEmptyLists()
    {
        var response = await ClientFor(Guid.NewGuid()).GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, body.RootElement.GetProperty("upcoming").GetArrayLength());
        Assert.Equal(0, body.RootElement.GetProperty("past").GetArrayLength());
        Assert.Equal(0, body.RootElement.GetProperty("cancelled").GetArrayLength());
    }

    [Fact]
    public async Task Mine_SixtyPastReservations_ReturnsThemAllWithoutPaging()
    {
        var room = await SeedRoomAsync();
        var user = Guid.NewGuid();
        await SeedAsync(Enumerable.Range(1, 60)
            .Select(day => NewReservation(room, user, Now.AddDays(-day)))
            .ToArray());

        var result = await GetMineAsync(user);

        Assert.Equal(60, result.Past.Count);
    }

    [Fact]
    public async Task Mine_WithoutToken_Returns401()
    {
        // Lo rifiuta l'autenticazione (RequireAuthorization sul gruppo), prima ancora dell'endpoint.
        var response = await _fixture.Factory.CreateClient().GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WithTokenSignedWithAnotherKey_Returns401()
    {
        var client = _fixture.Factory.CreateClient();
        var token = BookingsApiFixture.CreateToken(Guid.NewGuid().ToString(), signingKey: BookingsApiFixture.OtherSigningKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WithExpiredToken_Returns401()
    {
        // Lo rifiuta l'autenticazione prima dell'endpoint: il corpo non è quello dell'endpoint e non si controlla.
        var client = _fixture.Factory.CreateClient();
        var token = BookingsApiFixture.CreateToken(Guid.NewGuid().ToString(), lifetime: TimeSpan.FromMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WithUnsignedToken_Returns401()
    {
        // Token "alg: none" con un sub qualsiasi: senza firma non vale, anche se il contenuto è plausibile.
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BookingsApiFixture.CreateUnsignedToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/bookings/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mine_WithValidTokenWithoutValidSub_Returns401()
    {
        // Token firmato correttamente, ma senza un "sub" che identifichi un utente: risponde l'endpoint.
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BookingsApiFixture.CreateToken(subject: null));

        var response = await client.GetAsync("/api/bookings/mine");

        await AssertUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Mine_AfterCreatingWithNameClaim_ShowsTheOrganizerName()
    {
        // La POST controlla anticipo e orario con l'ora reale: qui l'orologio non è fissato.
        _fixture.Clock.Reset();
        var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, rome).Date).AddDays(2);
        var local = day.ToDateTime(new TimeOnly(10, 0));
        var start = new DateTimeOffset(local, rome.GetUtcOffset(local));
        var room = await SeedRoomAsync(openAllDay: true);
        var mario = Guid.NewGuid();
        var client = ClientFor(mario, "Mario Rossi");

        var created = await client.PostAsJsonAsync("/api/bookings", new CreateReservationRequest(room.RoomId, start, start.AddHours(1), Purpose: null));
        var result = await GetMineAsync(mario);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = Assert.Single(result.Upcoming);
        Assert.Equal("Mario Rossi", item.OrganizerName);
        Assert.True(item.IsOrganizer);
        Assert.False(item.IsGroup);
    }

    // --- Supporto ---

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accesso richiesto", body);
        Assert.Contains("Effettua l'accesso per vedere le tue prenotazioni.", body);
    }

    private async Task<MyReservationsResponse> GetMineAsync(Guid userId)
    {
        var response = await ClientFor(userId).GetAsync("/api/bookings/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MyReservationsResponse>(Json))!;
    }

    private HttpClient ClientFor(Guid userId, string? name = BookingsApiFixture.DefaultUserName) =>
        _fixture.CreateClientFor(userId, name);

    private async Task<BookableRoom> SeedRoomAsync(
        string name = "Aula test",
        string? buildingName = "Edificio test",
        bool openAllDay = false)
    {
        var room = new BookableRoom
        {
            RoomId = Guid.NewGuid(),
            Name = name,
            BuildingName = buildingName,
            Capacity = 30,
            IsBookable = true,
            IsActive = true
        };

        // Orari servono solo alla prova con la POST.
        if (openAllDay)
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                room.OpeningHours.Add(new BookableRoomOpeningHours
                {
                    RoomId = room.RoomId,
                    DayOfWeek = day,
                    OpensAt = new TimeOnly(8, 0),
                    ClosesAt = new TimeOnly(20, 0)
                });
            }
        }

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.BookableRooms.Add(room);
        await db.SaveChangesAsync();

        return room;
    }

    private async Task<Reservation[]> SeedAsync(params Reservation[] reservations)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Reservations.AddRange(reservations);
        await db.SaveChangesAsync();

        return reservations;
    }

    // Prenotazione di un'ora; con membri diventa di gruppo (INV-5: partecipanti = organizzatore + membri).
    private static Reservation NewReservation(
        BookableRoom room,
        Guid organizerId,
        DateTimeOffset start,
        ReservationStatus status = ReservationStatus.Confirmed,
        string? organizerName = "Studente di prova",
        Guid[]? members = null)
    {
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.RoomId,
            OrganizerId = organizerId,
            OrganizerName = organizerName,
            Start = start,
            End = start.AddHours(1),
            Participants = 1 + (members?.Length ?? 0),
            Status = status,
            CreatedAt = Now.AddDays(-30),
            CancelledAt = status == ReservationStatus.Cancelled ? Now.AddDays(-1) : null
        };

        foreach (var member in members ?? [])
        {
            reservation.Members.Add(new ReservationMember { ReservationId = reservation.Id, UserId = member });
        }

        return reservation;
    }
}
