using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri del task #46 (US-14) su POST /api/bookings/{id}/check-in, con SQL Server reale e "adesso" fissato.
// Membri e record NoShow si inseriscono direttamente nel database: nessuna API li crea ancora (US-05, US-15).
// Ogni test usa spazi e utenti nuovi, così non vede i dati degli altri test.
[Collection(BookingsApiCollection.Name)]
public sealed class CheckInTests : IDisposable
{
    private const int Repetitions = 5;
    private const int ParallelRequests = 5;

    private const string NotAllowedTitle = "Check-in non consentito";
    private const string OutsideWindow = "Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.";
    private const string AlreadyCheckedIn = "Il check-in di questa prenotazione è già stato registrato.";
    private const string CancelledDetail = "Non è possibile fare il check-in di una prenotazione cancellata.";
    private const string NoShowDetail = "La prenotazione è già stata segnata come non usata.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // Prenotazioni di oggi, giovedì 5 novembre 2026, dalle 10:00 alle 11:00 ora di Roma.
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateTimeOffset TodayAt10 = At(Today, "10:00");

    private readonly BookingsApiFixture _fixture;

    public CheckInTests(BookingsApiFixture fixture)
    {
        _fixture = fixture;
        SetNow(At(Today, "10:05"));
    }

    // Riporta l'orologio all'ora reale per le altre classi della collezione.
    public void Dispose() => _fixture.Clock.Reset();

    // --- Check-in riuscito ---

    [Fact]
    public async Task CheckIn_ByOrganizerInsideTheWindow_Returns200AndRecordsOneCheckIn()
    {
        var now = SetNow(At(Today, "10:05"));
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);

        var response = await CheckInAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>(Json);
        Assert.Equal(now, body!.CheckedInAt);
        var record = Assert.Single(await RecordsAsync(reservation.Id));
        Assert.Equal(AttendanceType.CheckIn, record.Type);
        Assert.Equal(now, record.RecordedAt);
        Assert.Equal(organizer, record.RecordedBy);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    [Theory]
    [InlineData("09:45", false)]
    [InlineData("10:15", true)]
    public async Task CheckIn_AtTheEdgesOfTheWindow_Returns200(string time, bool asMember)
    {
        SetNow(At(Today, time));
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10, members: [member]);
        var requester = asMember ? member : organizer;

        var response = await CheckInAsync(ClientFor(requester), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(requester, Assert.Single(await RecordsAsync(reservation.Id)).RecordedBy);
    }

    [Fact]
    public async Task CheckIn_ByMemberOfAGroup_Returns200WithTheMemberAsRecordedBy()
    {
        SetNow(At(Today, "09:50"));
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10, members: [member]);

        var response = await CheckInAsync(ClientFor(member), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(member, Assert.Single(await RecordsAsync(reservation.Id)).RecordedBy);
    }

    // --- Finestra ---

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task CheckIn_OneSecondOutsideTheWindow_Returns422WithoutRecord(int direction)
    {
        var edge = direction < 0 ? At(Today, "09:45") : At(Today, "10:15");
        SetNow(edge.AddSeconds(direction));
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);

        var response = await CheckInAsync(ClientFor(organizer), reservation.Id);

        await AssertUnprocessableAsync(response, OutsideWindow, RejectionCodes.CheckInOutsideWindow);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    [Fact]
    public async Task CheckIn_ReservationOfTomorrow_Returns422AndIsNotUsed()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10.AddDays(1));

        var response = await CheckInAsync(ClientFor(organizer), reservation.Id);

        await AssertUnprocessableAsync(response, OutsideWindow, RejectionCodes.CheckInOutsideWindow);
        Assert.Empty(await RecordsAsync(reservation.Id));
        var mine = await GetMineAsync(organizer);
        Assert.Null(Assert.Single(mine.Upcoming).CheckedInAt);
    }

    [Fact]
    public async Task CheckIn_ByStaffStrangerOutsideTheWindow_Returns200WithStaffAsRecordedBy()
    {
        var now = SetNow(At(Today, "10:40"));
        var staff = Guid.NewGuid();
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10);

        var response = await CheckInAsync(StaffClient(staff), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = Assert.Single(await RecordsAsync(reservation.Id));
        Assert.Equal(staff, record.RecordedBy);
        Assert.Equal(now, record.RecordedAt);
    }

    // --- Un solo check-in ---

    [Fact]
    public async Task CheckIn_TwiceBySamePerson_Returns422AndKeepsTheFirstRecord()
    {
        var first = SetNow(At(Today, "10:00"));
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);
        var client = ClientFor(organizer);

        var firstResponse = await CheckInAsync(client, reservation.Id);
        SetNow(At(Today, "10:02"));
        var secondResponse = await CheckInAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        await AssertUnprocessableAsync(secondResponse, AlreadyCheckedIn, RejectionCodes.CheckInAlreadyRecorded);
        var record = Assert.Single(await RecordsAsync(reservation.Id));
        Assert.Equal(first, record.RecordedAt);
        Assert.Equal(organizer, record.RecordedBy);
    }

    [Fact]
    public async Task CheckIn_ByOrganizerAfterAMember_Returns422AndKeepsTheMemberAsRecordedBy()
    {
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10, members: [member]);

        var byMember = await CheckInAsync(ClientFor(member), reservation.Id);
        var byOrganizer = await CheckInAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, byMember.StatusCode);
        await AssertUnprocessableAsync(byOrganizer, AlreadyCheckedIn, RejectionCodes.CheckInAlreadyRecorded);
        Assert.Equal(member, Assert.Single(await RecordsAsync(reservation.Id)).RecordedBy);
    }

    [Fact]
    public async Task CheckIn_ByStaffAfterAStudent_Returns422()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);

        var byStudent = await CheckInAsync(ClientFor(organizer), reservation.Id);
        var byStaff = await CheckInAsync(StaffClient(), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, byStudent.StatusCode);
        await AssertUnprocessableAsync(byStaff, AlreadyCheckedIn, RejectionCodes.CheckInAlreadyRecorded);
        Assert.Equal(organizer, Assert.Single(await RecordsAsync(reservation.Id)).RecordedBy);
    }

    // --- Chi può fare il check-in ---

    [Fact]
    public async Task CheckIn_ByStranger_Returns403WithoutRecord()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10, members: [Guid.NewGuid()]);

        var response = await CheckInAsync(ClientFor(Guid.NewGuid()), reservation.Id);

        await AssertForbiddenAsync(response);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    [Fact]
    public async Task CheckIn_ByStrangerOnCancelledReservation_Returns403()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10, ReservationStatus.Cancelled);

        var response = await CheckInAsync(ClientFor(Guid.NewGuid()), reservation.Id);

        await AssertForbiddenAsync(response);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    // --- Check-in valido per tutto il gruppo ---

    [Fact]
    public async Task CheckIn_ByMember_IsSeenByOrganizerAndOtherMembers()
    {
        var now = SetNow(At(Today, "09:55"));
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10, members: [member, otherMember]);

        var response = await CheckInAsync(ClientFor(member), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var user in new[] { organizer, otherMember })
        {
            var item = Assert.Single((await GetMineAsync(user)).Upcoming);
            Assert.Equal(reservation.Id, item.Id);
            Assert.Equal(now, item.CheckedInAt);
        }
    }

    [Fact]
    public async Task CheckIn_ByOrganizer_IsSeenByTheMember()
    {
        var now = SetNow(At(Today, "10:00"));
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10, members: [member]);

        var response = await CheckInAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(now, Assert.Single((await GetMineAsync(member)).Upcoming).CheckedInAt);
    }

    // --- Prenotazione cancellata o no-show ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckIn_OnCancelledReservation_Returns422WithoutRecord(bool asStaff)
    {
        SetNow(At(Today, "09:50"));
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10, ReservationStatus.Cancelled);

        var response = await CheckInAsync(asStaff ? StaffClient() : ClientFor(organizer), reservation.Id);

        await AssertUnprocessableAsync(response, CancelledDetail, RejectionCodes.CheckInCancelled);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckIn_OnNoShowReservation_Returns422WithoutCheckIn(bool asStaff)
    {
        SetNow(At(Today, "10:10"));
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);
        await SeedAttendanceAsync(reservation.Id, AttendanceType.NoShow);

        var response = await CheckInAsync(asStaff ? StaffClient() : ClientFor(organizer), reservation.Id);

        await AssertUnprocessableAsync(response, NoShowDetail, RejectionCodes.CheckInNoShow);
        Assert.Equal(AttendanceType.NoShow, Assert.Single(await RecordsAsync(reservation.Id)).Type);
    }

    // --- Prenotazione inesistente ---

    [Fact]
    public async Task CheckIn_UnknownId_Returns404WithoutRecord()
    {
        var unknownId = Guid.NewGuid();

        var response = await CheckInAsync(ClientFor(Guid.NewGuid()), unknownId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Prenotazione non trovata", problem.Title);
        Assert.Equal("La prenotazione richiesta non esiste.", problem.Detail);
        Assert.Empty(await RecordsAsync(unknownId));
    }

    // --- Accesso ---

    public static TheoryData<string> InvalidTokens => new() { "nessuno", "altra chiave", "scaduto", "senza firma" };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task CheckIn_WithoutValidToken_Returns401WithoutRecord(string token)
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, TodayAt10);
        var client = _fixture.Factory.CreateClient();
        var value = token switch
        {
            "altra chiave" => BookingsApiFixture.CreateToken(organizer.ToString(), signingKey: BookingsApiFixture.OtherSigningKey),
            "scaduto" => BookingsApiFixture.CreateToken(organizer.ToString(), lifetime: TimeSpan.FromMinutes(-10)),
            "senza firma" => BookingsApiFixture.CreateUnsignedToken(organizer),
            _ => null
        };
        if (value is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
        }

        var response = await CheckInAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    [Fact]
    public async Task CheckIn_WithValidTokenWithoutSub_Returns401WithMessageWithoutRecord()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10);
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", BookingsApiFixture.CreateToken(subject: null));

        var response = await CheckInAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Accesso richiesto", problem.Title);
        Assert.Equal("Effettua l'accesso per fare il check-in.", problem.Detail);
        Assert.Empty(await RecordsAsync(reservation.Id));
    }

    // --- Check-in contemporanei ---

    [Fact]
    public async Task CheckIn_OrganizerAndMemberInParallel_OnlyOneSucceeds()
    {
        SetNow(At(Today, "10:00"));

        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var organizer = Guid.NewGuid();
            var member = Guid.NewGuid();
            var reservation = await SeedReservationAsync(organizer, TodayAt10, members: [member]);

            var responses = await CheckInInParallelAsync(reservation.Id, [ClientFor(organizer), ClientFor(member)]);

            await AssertOnlyOneCheckInAsync(responses, reservation.Id);
        }
    }

    [Fact]
    public async Task CheckIn_SameOrganizerFiveTimesInParallel_OnlyOneSucceeds()
    {
        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var organizer = Guid.NewGuid();
            var reservation = await SeedReservationAsync(organizer, TodayAt10);
            var clients = Enumerable.Range(0, ParallelRequests).Select(_ => ClientFor(organizer)).ToList();

            var responses = await CheckInInParallelAsync(reservation.Id, clients);

            await AssertOnlyOneCheckInAsync(responses, reservation.Id);
        }
    }

    // --- Indice unico ---

    [Fact]
    public async Task SecondAttendanceRecordForTheSameReservation_IsRejectedByTheDatabase()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), TodayAt10);
        await SeedAttendanceAsync(reservation.Id, AttendanceType.NoShow);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => SeedAttendanceAsync(reservation.Id, AttendanceType.CheckIn));

        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sqlException.Number, new[] { 2601, 2627 });
        Assert.Equal(AttendanceType.NoShow, Assert.Single(await RecordsAsync(reservation.Id)).Type);
    }

    // --- Supporto ---

    private async Task AssertOnlyOneCheckInAsync(IReadOnlyList<HttpResponseMessage> responses, Guid reservationId)
    {
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        foreach (var rejected in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
        {
            await AssertUnprocessableAsync(rejected, AlreadyCheckedIn, RejectionCodes.CheckInAlreadyRecorded);
        }

        Assert.Equal(AttendanceType.CheckIn, Assert.Single(await RecordsAsync(reservationId)).Type);
    }

    // Ogni richiesta ha il proprio HttpClient; tutte attendono lo stesso segnale e partono insieme.
    private static async Task<IReadOnlyList<HttpResponseMessage>> CheckInInParallelAsync(
        Guid reservationId,
        IReadOnlyList<HttpClient> clients)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var tasks = clients.Select(client => Task.Run(async () =>
            {
                await start.Task;
                return await CheckInAsync(client, reservationId);
            })).ToList();

            start.SetResult();
            return await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, Guid reservationId) =>
        client.PostAsync($"/api/bookings/{reservationId}/check-in", content: null);

    private static async Task AssertUnprocessableAsync(HttpResponseMessage response, string detail, string code)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(NotAllowedTitle, problem.Title);
        Assert.Equal(detail, problem.Detail);
        AssertCode(problem, code);
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(NotAllowedTitle, problem.Title);
        Assert.Equal("Non puoi fare il check-in di questa prenotazione.", problem.Detail);
        AssertCode(problem, RejectionCodes.CheckInForbidden);
    }

    // Codice di motivo (US-06): proprietà "code" del ProblemDetails.
    private static void AssertCode(ProblemDetails problem, string code)
    {
        Assert.True(problem.Extensions.TryGetValue("code", out var value));
        Assert.Equal(code, Assert.IsType<JsonElement>(value).GetString());
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.NotNull(problem);
        return problem;
    }

    private async Task<MyReservationsResponse> GetMineAsync(Guid userId)
    {
        var response = await ClientFor(userId).GetAsync("/api/bookings/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MyReservationsResponse>(Json))!;
    }

    private DateTimeOffset SetNow(DateTimeOffset now)
    {
        _fixture.Clock.Set(now);
        return now;
    }

    private HttpClient ClientFor(Guid userId) => _fixture.CreateClientFor(userId);

    // Personale del campus che non è né organizzatore né membro.
    private HttpClient StaffClient(Guid? staffId = null) =>
        _fixture.CreateClientFor(staffId ?? Guid.NewGuid(), role: "Staff");

    private async Task<Reservation> LoadAsync(Guid reservationId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.AsNoTracking().SingleAsync(reservation => reservation.Id == reservationId);
    }

    private async Task<List<AttendanceRecord>> RecordsAsync(Guid reservationId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.ReservationId == reservationId)
            .ToListAsync();
    }

    // Record di presenza inserito direttamente: nessuna API crea ancora i NoShow (US-15).
    private async Task SeedAttendanceAsync(Guid reservationId, AttendanceType type)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            Type = type,
            RecordedAt = TodayAt10.AddMinutes(20),
            RecordedBy = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
    }

    // Spazio nuovo con una prenotazione di un'ora; con membri diventa di gruppo
    // (INV-5: partecipanti = organizzatore + membri).
    private async Task<Reservation> SeedReservationAsync(
        Guid organizerId,
        DateTimeOffset start,
        ReservationStatus status = ReservationStatus.Confirmed,
        Guid[]? members = null)
    {
        var room = new BookableRoom
        {
            RoomId = Guid.NewGuid(),
            Name = "Aula A1",
            BuildingName = "Edificio A",
            Capacity = 30,
            IsBookable = true,
            IsActive = true
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = room.RoomId,
            OrganizerId = organizerId,
            OrganizerName = BookingsApiFixture.DefaultUserName,
            Start = start,
            End = start.AddHours(1),
            Participants = 1 + (members?.Length ?? 0),
            Status = status,
            CreatedAt = start.AddDays(-7),
            CancelledAt = status == ReservationStatus.Cancelled ? start.AddDays(-1) : null
        };

        foreach (var member in members ?? [])
        {
            reservation.Members.Add(new ReservationMember { ReservationId = reservation.Id, UserId = member });
        }

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.BookableRooms.Add(room);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        return reservation;
    }

    private static DateTimeOffset At(DateOnly date, string time)
    {
        var local = date.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
