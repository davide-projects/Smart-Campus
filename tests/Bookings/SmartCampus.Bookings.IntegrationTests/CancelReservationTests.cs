using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri del task #26 (US-08) su DELETE /api/bookings/{id}, con SQL Server reale e "adesso" fissato.
// I membri si inseriscono direttamente nel database: nessuna API li crea ancora (US-05).
// Ogni test usa spazi e utenti nuovi, così non vede i dati degli altri test.
[Collection(BookingsApiCollection.Name)]
public sealed class CancelReservationTests : IDisposable
{
    private const int Repetitions = 5;
    private const int ParallelRequests = 5;

    // Check-in contro cancellazione: più ripetizioni per far emergere un'eventuale gara (design della change 45-check-in, decisione 4).
    private const int CheckInRaceRepetitions = 20;

    private const string UsedDetail = "Non è possibile cancellare una prenotazione già usata.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // "Adesso" fisso: giovedì 5 novembre 2026, 10:30 UTC (11:30 ora di Roma).
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 30, 0, TimeSpan.Zero);

    // Dopodomani, sabato 7 novembre 2026, dalle 10:00 alle 11:00 ora di Roma.
    private static readonly DateOnly DayAfterTomorrow = new(2026, 11, 7);
    private static readonly DateTimeOffset DayAfterTomorrowAt10 = At(DayAfterTomorrow, "10:00");

    private readonly BookingsApiFixture _fixture;

    public CancelReservationTests(BookingsApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Clock.Set(Now);
    }

    // Riporta l'orologio all'ora reale per le altre classi della collezione.
    public void Dispose() => _fixture.Clock.Reset();

    // --- Cancellazione e suoi effetti ---

    [Fact]
    public async Task Cancel_ByOrganizer_Returns204AndCancelsAtTheRequestTime()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);

        var response = await DeleteAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(Now, stored.CancelledAt);
    }

    [Fact]
    public async Task Cancel_ThenSlotIsFreeAndAnotherStudentCanBookIt()
    {
        var organizer = Guid.NewGuid();
        var otherStudent = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);

        var cancelled = await DeleteAsync(ClientFor(organizer), reservation.Id);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        // La fascia delle 10:00 e quella delle 10:30 risultano libere per un altro studente.
        var availabilityResponse = await ClientFor(otherStudent)
            .GetAsync($"/api/bookings/availability?roomId={reservation.RoomId}&date={DayAfterTomorrow:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);
        var availability = (await availabilityResponse.Content.ReadFromJsonAsync<DailyAvailability>(Json))!;
        var cancelledSlots = availability.Slots
            .Where(slot => slot.Start >= reservation.Start && slot.End <= reservation.End)
            .ToList();
        Assert.Equal(2, cancelledSlots.Count);
        Assert.All(cancelledSlots, slot => Assert.True(slot.IsFree));

        // E la può prenotare: una sola Confirmed nella fascia, la sua.
        var created = await ClientFor(otherStudent).PostAsJsonAsync(
            "/api/bookings",
            new CreateReservationRequest(reservation.RoomId, reservation.Start, reservation.End, Purpose: null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var confirmed = await ConfirmedInSlotAsync(reservation.RoomId, reservation.Start, reservation.End);
        var only = Assert.Single(confirmed);
        Assert.Equal(otherStudent, only.OrganizerId);
    }

    [Fact]
    public async Task Cancel_ThenFormerParticipantsCanBookTheSameSlotAgain()
    {
        // INV-8 conta solo le Confirmed: la prenotazione cancellata non blocca più organizzatore e membro.
        // L'organizzatore riprenota lo stesso spazio, il membro un altro spazio nella stessa fascia (INV-1).
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10, members: [member]);
        var otherRoomId = await SeedRoomAsync();

        var cancelled = await DeleteAsync(ClientFor(organizer), reservation.Id);
        var organizerAgain = await ClientFor(organizer).PostAsJsonAsync(
            "/api/bookings",
            new CreateReservationRequest(reservation.RoomId, reservation.Start, reservation.End, Purpose: null));
        var memberAgain = await ClientFor(member).PostAsJsonAsync(
            "/api/bookings",
            new CreateReservationRequest(otherRoomId, reservation.Start, reservation.End, Purpose: null));

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Created, organizerAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, memberAgain.StatusCode);
        var confirmed = await ConfirmedInSlotAsync(reservation.RoomId, reservation.Start, reservation.End);
        Assert.Equal(organizer, Assert.Single(confirmed).OrganizerId);
    }

    [Fact]
    public async Task Cancel_MovesTheReservationFromUpcomingToCancelledForOrganizerAndMember()
    {
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10, members: [member]);

        var response = await DeleteAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var user in new[] { organizer, member })
        {
            var mine = await GetMineAsync(user);
            Assert.DoesNotContain(mine.Upcoming, item => item.Id == reservation.Id);
            Assert.Contains(mine.Cancelled, item => item.Id == reservation.Id);
        }
    }

    // --- Chi può cancellare ---

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancel_ByMemberOrStranger_Returns403AndLeavesTheReservationConfirmed(bool asMember)
    {
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var requester = asMember ? member : Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10, members: [member]);

        var response = await DeleteAsync(ClientFor(requester), reservation.Id);

        await AssertForbiddenAsync(response);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Null(stored.CancelledAt);
        foreach (var user in new[] { organizer, member })
        {
            var mine = await GetMineAsync(user);
            Assert.Contains(mine.Upcoming, item => item.Id == reservation.Id);
        }
    }

    [Fact]
    public async Task Cancel_ByStrangerOnAlreadyCancelledReservation_Returns403AndKeepsCancelledAt()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), DayAfterTomorrowAt10, ReservationStatus.Cancelled);
        var before = (await LoadAsync(reservation.Id)).CancelledAt;

        var response = await DeleteAsync(ClientFor(Guid.NewGuid()), reservation.Id);

        await AssertForbiddenAsync(response);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(before, stored.CancelledAt);
    }

    [Fact]
    public async Task Cancel_ByStrangerOnPastReservation_Returns403()
    {
        // Il controllo di chi può cancellare viene prima del limite di tempo.
        var reservation = await SeedReservationAsync(Guid.NewGuid(), Now.AddDays(-1));

        var response = await DeleteAsync(ClientFor(Guid.NewGuid()), reservation.Id);

        await AssertForbiddenAsync(response);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Null(stored.CancelledAt);
    }

    [Fact]
    public async Task Cancel_ByStaffWhoIsNotOrganizerNorMember_Returns204()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), DayAfterTomorrowAt10, members: [Guid.NewGuid()]);

        var response = await DeleteAsync(StaffClient(), reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(Now, stored.CancelledAt);
    }

    // --- Limite di tempo (A5) ---

    public static TheoryData<string, bool> StartedReservations => new()
    {
        { "iniziata", false },
        { "iniziata", true },
        { "inizio uguale ad adesso", false },
        { "inizio uguale ad adesso", true },
        { "passata", false },
        { "passata", true }
    };

    [Theory]
    [MemberData(nameof(StartedReservations))]
    public async Task Cancel_StartedOrPast_Returns422AndLeavesTheReservationConfirmed(string when, bool asStaff)
    {
        var start = when switch
        {
            "iniziata" => Now.AddMinutes(-15),
            "inizio uguale ad adesso" => Now,
            _ => Now.AddDays(-1)
        };
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, start);

        var response = await DeleteAsync(asStaff ? StaffClient() : ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Cancellazione non consentita", problem.Title);
        Assert.Equal("Non è possibile cancellare una prenotazione già iniziata o passata.", problem.Detail);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Null(stored.CancelledAt);

        // Una prenotazione passata resta tra le passate dell'organizzatore.
        if (when == "passata")
        {
            var mine = await GetMineAsync(organizer);
            Assert.Contains(mine.Past, item => item.Id == reservation.Id);
            Assert.DoesNotContain(mine.Cancelled, item => item.Id == reservation.Id);
        }
    }

    [Fact]
    public async Task Cancel_OneTickBeforeStart_Returns204()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
        var requestTime = reservation.Start.AddTicks(-1);
        _fixture.Clock.Set(requestTime);

        var response = await DeleteAsync(ClientFor(organizer), reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(requestTime, stored.CancelledAt);
    }

    // --- Cancellazione ripetuta ---

    [Fact]
    public async Task Cancel_Twice_Returns204AndKeepsTheFirstCancelledAt()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
        var client = ClientFor(organizer);

        var first = await DeleteAsync(client, reservation.Id);
        _fixture.Clock.Set(Now.AddHours(1));
        var second = await DeleteAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(Now, stored.CancelledAt);
    }

    [Fact]
    public async Task Cancel_AgainAfterTheStart_Returns204AndKeepsTheFirstCancelledAt()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
        var client = ClientFor(organizer);

        var first = await DeleteAsync(client, reservation.Id);
        _fixture.Clock.Set(reservation.Start.AddMinutes(30));
        var second = await DeleteAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(Now, stored.CancelledAt);
    }

    // --- Prenotazione inesistente ---

    [Fact]
    public async Task Cancel_UnknownId_Returns404AndChangesNothing()
    {
        // Copre anche la prenotazione la cui POST non ha ancora fatto commit: per la DELETE non esiste.
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);

        var response = await DeleteAsync(ClientFor(organizer), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Prenotazione non trovata", problem.Title);
        Assert.Equal("La prenotazione richiesta non esiste.", problem.Detail);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    // --- Accesso ---

    [Fact]
    public async Task Cancel_WithoutToken_Returns401AndChangesNothing()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), DayAfterTomorrowAt10);

        var response = await DeleteAsync(_fixture.Factory.CreateClient(), reservation.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    [Fact]
    public async Task Cancel_WithTokenSignedWithAnotherKey_Returns401AndChangesNothing()
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", BookingsApiFixture.CreateToken(organizer.ToString(), signingKey: BookingsApiFixture.OtherSigningKey));

        var response = await DeleteAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    [Fact]
    public async Task Cancel_WithValidTokenWithoutSub_Returns401WithMessageAndChangesNothing()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), DayAfterTomorrowAt10);
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", BookingsApiFixture.CreateToken(subject: null));

        var response = await DeleteAsync(client, reservation.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Accesso richiesto", problem.Title);
        Assert.Equal("Effettua l'accesso per cancellare la prenotazione.", problem.Detail);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    // --- Cancellazioni contemporanee ---
    // La scrittura unica non si verifica qui (con il TestClock fisso ogni scrittura avrebbe lo stesso valore):
    // la garantisce la WHERE dell'UPDATE (design, decisione 4). Il test controlla esiti e stato finale.

    [Fact]
    public async Task Cancel_OrganizerAndStaffInParallel_AllReturn204()
    {
        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var organizer = Guid.NewGuid();
            var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);

            var responses = await DeleteInParallelAsync(reservation.Id, [ClientFor(organizer), StaffClient()]);

            await AssertAllCancelledAsync(responses, reservation.Id);
        }
    }

    [Fact]
    public async Task Cancel_SameOrganizerFiveTimesInParallel_AllReturn204()
    {
        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var organizer = Guid.NewGuid();
            var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
            var clients = Enumerable.Range(0, ParallelRequests).Select(_ => ClientFor(organizer)).ToList();

            var responses = await DeleteInParallelAsync(reservation.Id, clients);

            await AssertAllCancelledAsync(responses, reservation.Id);
        }
    }

    // --- Prenotazione usata (US-14) ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_FutureReservationWithCheckIn_Returns422AndLeavesItConfirmedInUpcoming(bool asStaff)
    {
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, DayAfterTomorrowAt10);
        var checkedInAt = Now.AddMinutes(-10);
        await SeedCheckInAsync(reservation.Id, checkedInAt);

        var response = await DeleteAsync(asStaff ? StaffClient() : ClientFor(organizer), reservation.Id);

        await AssertUsedAsync(response);
        var stored = await LoadAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Null(stored.CancelledAt);
        var item = Assert.Single((await GetMineAsync(organizer)).Upcoming, item => item.Id == reservation.Id);
        Assert.Equal(checkedInAt, item.CheckedInAt);
    }

    [Fact]
    public async Task Cancel_StartedReservationWithCheckIn_Returns422AsUsed()
    {
        // Il controllo sull'uso viene prima del limite di tempo.
        var organizer = Guid.NewGuid();
        var reservation = await SeedReservationAsync(organizer, Now.AddMinutes(-30));
        await SeedCheckInAsync(reservation.Id, Now.AddMinutes(-35));

        var response = await DeleteAsync(ClientFor(organizer), reservation.Id);

        await AssertUsedAsync(response);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    [Fact]
    public async Task Cancel_ByStrangerOnUsedReservation_Returns403()
    {
        var reservation = await SeedReservationAsync(Guid.NewGuid(), DayAfterTomorrowAt10);
        await SeedCheckInAsync(reservation.Id, Now);

        var response = await DeleteAsync(ClientFor(Guid.NewGuid()), reservation.Id);

        await AssertForbiddenAsync(response);
        Assert.Equal(ReservationStatus.Confirmed, (await LoadAsync(reservation.Id)).Status);
    }

    // Con il clock 5 minuti prima dell'inizio, l'organizzatore cancella e un membro fa il check-in in parallelo:
    // al massimo una delle due riesce e mai una prenotazione cancellata ha un check-in.
    [Fact]
    public async Task CancelAndCheckInInParallel_OnlyOneSucceedsAndNeverCancelledWithCheckIn()
    {
        var start = DayAfterTomorrowAt10;
        _fixture.Clock.Set(start.AddMinutes(-5));

        for (var repetition = 1; repetition <= CheckInRaceRepetitions; repetition++)
        {
            var organizer = Guid.NewGuid();
            var member = Guid.NewGuid();
            var reservation = await SeedReservationAsync(organizer, start, members: [member]);
            var organizerClient = ClientFor(organizer);
            var memberClient = ClientFor(member);
            var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            HttpResponseMessage delete;
            HttpResponseMessage checkIn;
            try
            {
                var deleteTask = Task.Run(async () =>
                {
                    await go.Task;
                    return await DeleteAsync(organizerClient, reservation.Id);
                });
                var checkInTask = Task.Run(async () =>
                {
                    await go.Task;
                    return await memberClient.PostAsync($"/api/bookings/{reservation.Id}/check-in", content: null);
                });

                go.SetResult();
                delete = await deleteTask;
                checkIn = await checkInTask;
            }
            finally
            {
                organizerClient.Dispose();
                memberClient.Dispose();
            }

            var stored = await LoadAsync(reservation.Id);
            var records = await AttendanceAsync(reservation.Id);

            if (delete.StatusCode == HttpStatusCode.NoContent)
            {
                // Ha vinto la cancellazione.
                Assert.Equal(HttpStatusCode.UnprocessableEntity, checkIn.StatusCode);
                var problem = await ReadProblemAsync(checkIn);
                Assert.Equal("Check-in non consentito", problem.Title);
                Assert.Equal("Non è possibile fare il check-in di una prenotazione cancellata.", problem.Detail);
                AssertCode(problem, RejectionCodes.CheckInCancelled);
                Assert.Equal(ReservationStatus.Cancelled, stored.Status);
                Assert.Empty(records);
            }
            else
            {
                // Ha vinto il check-in.
                Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
                await AssertUsedAsync(delete);
                Assert.Equal(ReservationStatus.Confirmed, stored.Status);
                Assert.Equal(AttendanceType.CheckIn, Assert.Single(records).Type);
            }
        }
    }

    // --- Supporto ---

    private static async Task AssertUsedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Cancellazione non consentita", problem.Title);
        Assert.Equal(UsedDetail, problem.Detail);
        AssertCode(problem, RejectionCodes.CancellationAlreadyUsed);
    }

    // Codice di motivo (US-06): proprietà "code" del ProblemDetails.
    private static void AssertCode(ProblemDetails problem, string code)
    {
        Assert.True(problem.Extensions.TryGetValue("code", out var value));
        Assert.Equal(code, Assert.IsType<JsonElement>(value).GetString());
    }

    // Check-in inserito direttamente, per i test che non riguardano l'endpoint del check-in.
    private async Task SeedCheckInAsync(Guid reservationId, DateTimeOffset recordedAt)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            Type = AttendanceType.CheckIn,
            RecordedAt = recordedAt,
            RecordedBy = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<AttendanceRecord>> AttendanceAsync(Guid reservationId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.ReservationId == reservationId)
            .ToListAsync();
    }

    private async Task AssertAllCancelledAsync(IReadOnlyList<HttpResponseMessage> responses, Guid reservationId)
    {
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        var stored = await LoadAsync(reservationId);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(Now, stored.CancelledAt);
    }

    // Ogni richiesta ha il proprio HttpClient; tutte attendono lo stesso segnale e partono insieme.
    private static async Task<IReadOnlyList<HttpResponseMessage>> DeleteInParallelAsync(
        Guid reservationId,
        IReadOnlyList<HttpClient> clients)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var tasks = clients.Select(client => Task.Run(async () =>
            {
                await start.Task;
                return await DeleteAsync(client, reservationId);
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

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid reservationId) =>
        client.DeleteAsync($"/api/bookings/{reservationId}");

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Cancellazione non consentita", problem.Title);
        Assert.Equal("Non puoi cancellare questa prenotazione.", problem.Detail);
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

    private HttpClient ClientFor(Guid userId) => _fixture.CreateClientFor(userId);

    // Personale del campus che non è né organizzatore né membro.
    private HttpClient StaffClient() => _fixture.CreateClientFor(Guid.NewGuid(), role: "Staff");

    private async Task<Reservation> LoadAsync(Guid reservationId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.AsNoTracking().SingleAsync(reservation => reservation.Id == reservationId);
    }

    private async Task<List<Reservation>> ConfirmedInSlotAsync(Guid roomId, DateTimeOffset start, DateTimeOffset end)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == roomId &&
                reservation.Status == ReservationStatus.Confirmed &&
                reservation.Start < end &&
                reservation.End > start)
            .ToListAsync();
    }

    // Spazio nuovo aperto tutti i giorni 08:00-20:00.
    private async Task<Guid> SeedRoomAsync()
    {
        var roomId = Guid.NewGuid();
        var room = new BookableRoom
        {
            RoomId = roomId,
            Name = "Aula A1",
            BuildingName = "Edificio A",
            Capacity = 30,
            IsBookable = true,
            IsActive = true
        };

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

    // Spazio nuovo con una prenotazione di un'ora; con membri diventa di gruppo
    // (INV-5: partecipanti = organizzatore + membri).
    private async Task<Reservation> SeedReservationAsync(
        Guid organizerId,
        DateTimeOffset start,
        ReservationStatus status = ReservationStatus.Confirmed,
        Guid[]? members = null)
    {
        var roomId = await SeedRoomAsync();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            OrganizerId = organizerId,
            OrganizerName = BookingsApiFixture.DefaultUserName,
            Start = start,
            End = start.AddHours(1),
            Participants = 1 + (members?.Length ?? 0),
            Status = status,
            CreatedAt = Now.AddDays(-7),
            CancelledAt = status == ReservationStatus.Cancelled ? Now.AddDays(-1) : null
        };

        foreach (var member in members ?? [])
        {
            reservation.Members.Add(new ReservationMember { ReservationId = reservation.Id, UserId = member });
        }

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
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
