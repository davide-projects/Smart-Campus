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

// US-05: POST /api/bookings con i membri del gruppo, su SQL Server reale (Testcontainers) e con Identity finta.
// Ogni test usa spazi e utenti nuovi; la Identity finta torna allo stato iniziale a ogni test.
[Collection(BookingsApiCollection.Name)]
public sealed class GroupReservationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // Dopodomani: sempre nel futuro e sempre entro i 14 giorni (A2), qualunque sia la data di esecuzione.
    private static readonly DateOnly Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).Date).AddDays(2);

    private readonly BookingsApiFixture _fixture;

    public GroupReservationTests(BookingsApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Identity.Reset();
    }

    // --- Creazione ---

    [Fact]
    public async Task TwoFreeStudentMembers_Returns201WithThreeParticipantsAndSavesTheMembers()
    {
        var roomId = await SeedRoomAsync(capacity: 30);
        Guid anna = Guid.NewGuid(), luca = Guid.NewGuid();

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", anna, luca));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ReservationResponse>(Json);
        Assert.Equal(3, created!.Participants);
        Assert.Equal(ReservationStatus.Confirmed, created.Status);

        // La lista Staff mostra partecipanti e membri salvati.
        var staff = _fixture.CreateClientFor(Guid.NewGuid(), role: "Staff");
        var list = await staff.GetFromJsonAsync<List<ReservationListItem>>(
            $"/api/bookings?roomId={roomId}&date={Day:yyyy-MM-dd}", Json);
        var item = Assert.Single(list!);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(3, item.Participants);
        Assert.Equal(new[] { anna, luca }.Order(), item.MemberIds.Order());
    }

    [Fact]
    public async Task GroupReservation_CallsIdentityOnceWithTheMembersAndTheSameAuthorization()
    {
        var roomId = await SeedRoomAsync();
        var organizerId = Guid.NewGuid();
        Guid anna = Guid.NewGuid(), luca = Guid.NewGuid();
        using var client = _fixture.CreateClientFor(organizerId);

        var response = await client.PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00", anna, luca));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var call = Assert.Single(_fixture.Identity.Requests);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/internal/users/students/check", call.Path);
        Assert.Equal(new[] { anna, luca }.Order(), call.UserIds.Order());
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.ToString(), call.Authorization);
    }

    [Fact]
    public async Task WithoutMembersOrWithAnEmptyList_DoesNotCallIdentity()
    {
        var roomId = await SeedRoomAsync();

        var withoutField = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));
        var emptyList = await Post(Guid.NewGuid(), new CreateReservationRequest(roomId, At("12:00"), At("13:00"), null, []));

        Assert.Equal(HttpStatusCode.Created, withoutField.StatusCode);
        Assert.Equal(HttpStatusCode.Created, emptyList.StatusCode);
        Assert.Equal(1, (await emptyList.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Participants);
        Assert.Empty(_fixture.Identity.Requests);
    }

    [Fact]
    public async Task TenMembersInARoomForThirty_Returns201WithElevenParticipants()
    {
        var roomId = await SeedRoomAsync(capacity: 30);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", NewIds(10)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(11, (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Participants);
    }

    [Fact]
    public async Task SingleReservationWhileIdentityWouldFail_Returns201()
    {
        _fixture.Identity.Mode = FakeIdentity.Behavior.Unreachable;
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Participants);
        Assert.Empty(_fixture.Identity.Requests);
    }

    // --- Capienza (INV-5) ---

    [Fact]
    public async Task ParticipantsOverCapacity_Returns422WithTheCapacityAndCreatesNothing()
    {
        var roomId = await SeedRoomAsync(capacity: 4);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", NewIds(4)));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.CapacityExceeded);
        Assert.Equal("Capienza superata", problem.GetProperty("title").GetString());
        Assert.Equal("Lo spazio ospita al massimo 4 persone.", problem.GetProperty("detail").GetString());
        Assert.Equal(4, problem.GetProperty("limits").GetProperty("maxParticipants").GetInt32());
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task ParticipantsEqualToCapacity_Returns201()
    {
        var roomId = await SeedRoomAsync(capacity: 4);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", NewIds(3)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(4, (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Participants);
    }

    // --- Membri già impegnati (INV-8) ---

    [Fact]
    public async Task MemberAlreadyOrganizerElsewhere_Returns409AndCreatesNothing()
    {
        var anna = Guid.NewGuid();
        var otherRoom = await SeedRoomAsync();
        var roomId = await SeedRoomAsync();
        Assert.Equal(HttpStatusCode.Created, (await Post(anna, Request(otherRoom, "10:00", "11:00"))).StatusCode);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:30", "11:30", anna, Guid.NewGuid()));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.MembersOccupied);
        Assert.Equal("Membro già impegnato", problem.GetProperty("title").GetString());
        Assert.Equal("Uno o più membri hanno già una prenotazione in questa fascia.", problem.GetProperty("detail").GetString());
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task MemberAlreadyMemberOfAnotherGroup_Returns409AndCreatesNothing()
    {
        var luca = Guid.NewGuid();
        var otherRoom = await SeedRoomAsync();
        var roomId = await SeedRoomAsync();
        Assert.Equal(HttpStatusCode.Created, (await Post(Guid.NewGuid(), Request(otherRoom, "10:00", "11:00", luca))).StatusCode);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "10:30", luca));

        await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.MembersOccupied);
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task MemberWithAnAdjacentReservation_Returns201()
    {
        var anna = Guid.NewGuid();
        var otherRoom = await SeedRoomAsync();
        var roomId = await SeedRoomAsync();
        Assert.Equal(HttpStatusCode.Created, (await Post(anna, Request(otherRoom, "10:00", "11:00"))).StatusCode);

        var response = await Post(Guid.NewGuid(), Request(roomId, "11:00", "12:00", anna));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MemberWithACancelledReservation_Returns201()
    {
        var anna = Guid.NewGuid();
        var otherRoom = await SeedRoomAsync();
        var roomId = await SeedRoomAsync();
        var annaClient = _fixture.CreateClientFor(anna);
        var own = await annaClient.PostAsJsonAsync("/api/bookings", Request(otherRoom, "10:00", "11:00"));
        var ownId = (await own.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await annaClient.DeleteAsync($"/api/bookings/{ownId}")).StatusCode);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", anna));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OrganizerAndMemberBothBusy_ReportsTheOrganizerConflictFirst()
    {
        var mario = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var marioRoom = await SeedRoomAsync();
        var annaRoom = await SeedRoomAsync();
        var roomId = await SeedRoomAsync();
        Assert.Equal(HttpStatusCode.Created, (await Post(mario, Request(marioRoom, "10:00", "11:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(anna, Request(annaRoom, "10:00", "11:00"))).StatusCode);

        var response = await Post(mario, Request(roomId, "10:00", "11:00", anna));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.PersonOccupied);
        Assert.Equal("Hai già una prenotazione in questa fascia oraria.", problem.GetProperty("detail").GetString());
        await AssertNothingCreatedAsync(roomId);
    }

    // --- Membri non validi ---

    [Fact]
    public async Task MemberWithStaffRole_Returns422AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        var elena = Guid.NewGuid();
        _fixture.Identity.MarkNotStudent(elena);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", Guid.NewGuid(), elena));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.MembersNotStudents);
        Assert.Equal("Membri non validi", problem.GetProperty("title").GetString());
        Assert.Equal("Puoi aggiungere come membri solo studenti registrati.", problem.GetProperty("detail").GetString());
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task UnknownMember_Returns422AndCreatesNothing()
    {
        var roomId = await SeedRoomAsync();
        var unknown = Guid.NewGuid();
        _fixture.Identity.MarkNotStudent(unknown);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", unknown));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.MembersNotStudents);
        Assert.Equal("Puoi aggiungere come membri solo studenti registrati.", problem.GetProperty("detail").GetString());
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task OrganizerAmongTheMembers_Returns422WithoutCallingIdentity()
    {
        var roomId = await SeedRoomAsync();
        var mario = Guid.NewGuid();

        var response = await Post(mario, Request(roomId, "10:00", "11:00", mario));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.OrganizerAsMember);
        Assert.Equal("Membri non validi", problem.GetProperty("title").GetString());
        Assert.Equal("Non puoi aggiungerti come membro: sei già l'organizzatore.", problem.GetProperty("detail").GetString());
        Assert.Empty(_fixture.Identity.Requests);
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task SameMemberTwice_Returns422WithoutCallingIdentity()
    {
        var roomId = await SeedRoomAsync();
        var anna = Guid.NewGuid();

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", anna, anna));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.DuplicateMembers);
        Assert.Equal("Ogni studente può essere aggiunto una sola volta.", problem.GetProperty("detail").GetString());
        Assert.Empty(_fixture.Identity.Requests);
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task ElevenMembers_Returns422WithTheMaximumWithoutCallingIdentity()
    {
        var roomId = await SeedRoomAsync(capacity: 30);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", NewIds(11)));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.TooManyMembers);
        Assert.Equal("Membri non validi", problem.GetProperty("title").GetString());
        Assert.Equal("Puoi aggiungere al massimo 10 membri.", problem.GetProperty("detail").GetString());
        Assert.Equal(10, problem.GetProperty("limits").GetProperty("maxMembers").GetInt32());
        Assert.Empty(_fixture.Identity.Requests);
        await AssertNothingCreatedAsync(roomId);
    }

    // --- Staff ---

    [Fact]
    public async Task StaffWithAMember_Returns403WithoutCallingIdentity()
    {
        var roomId = await SeedRoomAsync();
        var staff = _fixture.CreateClientFor(Guid.NewGuid(), role: "Staff");

        var response = await staff.PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00", Guid.NewGuid()));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Forbidden, RejectionCodes.GroupBookingForbidden);
        Assert.Equal("Prenotazione di gruppo non consentita", problem.GetProperty("title").GetString());
        Assert.Equal("Il personale non può creare prenotazioni di gruppo.", problem.GetProperty("detail").GetString());
        Assert.Empty(_fixture.Identity.Requests);
        await AssertNothingCreatedAsync(roomId);
    }

    [Fact]
    public async Task StaffWithAnEmptyList_Returns201()
    {
        var roomId = await SeedRoomAsync();
        var staff = _fixture.CreateClientFor(Guid.NewGuid(), role: "Staff");

        var response = await staff.PostAsJsonAsync(
            "/api/bookings", new CreateReservationRequest(roomId, At("10:00"), At("11:00"), null, []));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!.Participants);
    }

    // --- Identity non disponibile ---

    [Theory]
    [InlineData(FakeIdentity.Behavior.NoResponse)]
    [InlineData(FakeIdentity.Behavior.ServerError)]
    [InlineData(FakeIdentity.Behavior.InvalidJson)]
    [InlineData(FakeIdentity.Behavior.Unreachable)]
    public async Task IdentityNotAvailable_Returns503AndCreatesNothing(FakeIdentity.Behavior behavior)
    {
        _fixture.Identity.Mode = behavior;
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00", Guid.NewGuid()));

        var problem = await ReadProblemAsync(response, HttpStatusCode.ServiceUnavailable, RejectionCodes.MembersCheckUnavailable);
        Assert.Equal("Servizio non disponibile", problem.GetProperty("title").GetString());
        Assert.Equal(
            "Non è possibile verificare i membri in questo momento. Riprova tra poco.",
            problem.GetProperty("detail").GetString());
        await AssertNothingCreatedAsync(roomId);
    }

    // --- Verifiche e dati di prova ---

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement.Clone();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    // Un rifiuto non lascia né la prenotazione né i membri nello spazio richiesto.
    private async Task AssertNothingCreatedAsync(Guid roomId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(0, await db.Reservations.CountAsync(reservation => reservation.RoomId == roomId));
        Assert.Equal(0, await db.Set<ReservationMember>().CountAsync(member => member.Reservation.RoomId == roomId));
    }

    private async Task<HttpResponseMessage> Post(Guid userId, CreateReservationRequest request)
    {
        using var client = _fixture.CreateClientFor(userId);
        return await client.PostAsJsonAsync("/api/bookings", request);
    }

    // Spazio aperto tutti i giorni 08:00-20:00, con un Id nuovo per ogni chiamata.
    private async Task<Guid> SeedRoomAsync(int capacity = 30)
    {
        var roomId = Guid.NewGuid();
        var room = new BookableRoom { RoomId = roomId, Name = "Aula test", Capacity = capacity, IsBookable = true, IsActive = true };

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

    private static Guid[] NewIds(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    private static CreateReservationRequest Request(Guid roomId, string start, string end, params Guid[] memberIds) =>
        new(roomId, At(start), At(end), Purpose: null, memberIds.Length == 0 ? null : memberIds);

    private static DateTimeOffset At(string time)
    {
        var local = Day.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
