using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// Prova su SQL Server reale (Testcontainers) che i due lock della POST reggono con richieste contemporanee:
// UPDLOCK sulla riga dello spazio (INV-1) e sp_getapplock sulle persone (INV-8). Design della change
// 12-prenotare-uno-spazio, decisione 2, e per i membri del gruppo della change 16-prenotare-per-un-gruppo, decisione 4.
// Ogni ripetizione usa spazi e utenti nuovi, così l'ordine dei test non conta.
[Collection(BookingsApiCollection.Name)]
public sealed class ConcurrentReservationTests(BookingsApiFixture fixture)
{
    private const int Repetitions = 5;
    private const int ParallelUsers = 20;

    // Attesa massima del lock sulla persona nella POST (@LockTimeout = 10000).
    private static readonly TimeSpan UserLockTimeout = TimeSpan.FromSeconds(10);

    // Stesse opzioni JSON dell'API: lo stato arriva come stringa ("Confirmed").
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // Dopodomani: sempre nel futuro e sempre entro i 14 giorni (A2), qualunque sia la data di esecuzione.
    private static readonly DateOnly Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).Date).AddDays(2);

    [Fact]
    public async Task Create_TwentyUsersOnTheSameFreeSlot_ConfirmsExactlyOne()
    {
        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var roomId = await SeedRoomAsync();
            var requests = Enumerable.Range(0, ParallelUsers)
                .Select(_ => (UserId: Guid.NewGuid(), Body: Request(roomId, "10:00", "11:00")))
                .ToList();

            var results = await SendInParallelAsync(requests);

            Assert.Single(results, result => result.Response.StatusCode == HttpStatusCode.Created);

            var conflicts = results.Where(result => result.Response.StatusCode != HttpStatusCode.Created).ToList();
            Assert.Equal(ParallelUsers - 1, conflicts.Count);
            foreach (var conflict in conflicts)
            {
                Assert.Equal(HttpStatusCode.Conflict, conflict.Response.StatusCode);
                var problem = await ReadProblemAsync(conflict.Response);
                Assert.Equal("Spazio già prenotato", problem.Title);
            }

            Assert.Equal(1, await CountConfirmedInSlotAsync(roomId, "10:00", "11:00"));
        }
    }

    [Fact]
    public async Task Create_SameUserOnTwoFreeRoomsAtTheSameTime_ConfirmsOnlyOne()
    {
        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var userId = Guid.NewGuid();
            var firstRoom = await SeedRoomAsync();
            var secondRoom = await SeedRoomAsync();

            var results = await SendInParallelAsync(
            [
                (userId, Request(firstRoom, "10:00", "11:00")),
                (userId, Request(secondRoom, "10:00", "11:00"))
            ]);

            Assert.Single(results, result => result.Response.StatusCode == HttpStatusCode.Created);

            var rejected = Assert.Single(results, result => result.Response.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, rejected.Response.StatusCode);
            var problem = await ReadProblemAsync(rejected.Response);

            // Con due sole richieste l'attesa del lock sulla persona non deve arrivare a 10 secondi:
            // «Prenotazione in corso» è accettato solo se la richiesta ha davvero atteso così a lungo.
            if (problem.Title == "Prenotazione in corso")
            {
                Assert.True(
                    rejected.Elapsed > UserLockTimeout,
                    $"«Prenotazione in corso» dopo {rejected.Elapsed.TotalSeconds:F1} s, prima del timeout del lock.");
            }
            else
            {
                Assert.Equal("Hai già una prenotazione", problem.Title);
            }

            Assert.Equal(1, await CountConfirmedForOrganizerInSlotAsync(userId, "10:00", "11:00"));
        }
    }

    [Fact]
    public async Task Create_WhileAnotherTransactionHoldsTheUserLock_Returns409AfterTheTimeout()
    {
        var userId = Guid.NewGuid();
        var roomId = await SeedRoomAsync();

        // Connection string e nome del database usati dal servizio: la connessione separata deve
        // prendere il lock nello stesso database, altrimenti la POST non lo vedrebbe.
        string connectionString;
        string serviceDatabase;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            connectionString = db.Database.GetConnectionString()!;
            serviceDatabase = await db.Database.SqlQuery<string>($"SELECT DB_NAME() AS [Value]").SingleAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(serviceDatabase, connection.Database);

        // Stessa risorsa e stessa modalità della POST: il lock resta finché la transazione è aperta,
        // cioè per tutta l'attesa della richiesta (oltre i 10 secondi del timeout).
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
                SELECT @result;
                """;
            command.Parameters.AddWithValue("@resource", $"user:{userId}");
            var lockResult = (int)(await command.ExecuteScalarAsync())!;
            Assert.True(lockResult >= 0, $"sp_getapplock ha restituito {lockResult}.");
        }

        var response = await ClientFor(userId).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00"));

        await transaction.RollbackAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Prenotazione in corso", problem.Title);
        Assert.Equal("Hai un'altra prenotazione in corso, riprova tra qualche secondo.", problem.Detail);
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    [Fact]
    public async Task Create_MemberOfAGroupReservationInAnotherRoom_Returns409AndCreatesNothing()
    {
        var memberId = Guid.NewGuid();
        var groupRoom = await SeedRoomAsync();
        var requestedRoom = await SeedRoomAsync();

        // La prenotazione di gruppo si inserisce direttamente nel database: qui interessa solo il conflitto dell'organizzatore.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservationId = Guid.NewGuid();
            db.Reservations.Add(new Reservation
            {
                Id = reservationId,
                RoomId = groupRoom,
                OrganizerId = Guid.NewGuid(),
                OrganizerName = "Organizzatore di prova",
                Start = At("10:00"),
                End = At("11:00"),
                Participants = 2,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow,
                Members = [new ReservationMember { ReservationId = reservationId, UserId = memberId }]
            });
            await db.SaveChangesAsync();
        }

        var response = await ClientFor(memberId).PostAsJsonAsync("/api/bookings", Request(requestedRoom, "10:00", "10:30"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Hai già una prenotazione in questa fascia oraria.", problem.Detail);
        Assert.Equal(0, await CountReservationsAsync(requestedRoom));
    }

    // --- Prenotazioni di gruppo (US-05) ---

    [Fact]
    public async Task Create_SameMemberInTwoGroupsAtTheSameTime_ConfirmsOnlyOne()
    {
        fixture.Identity.Reset();

        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var anna = Guid.NewGuid();
            var firstRoom = await SeedRoomAsync();
            var secondRoom = await SeedRoomAsync();

            var results = await SendInParallelAsync(
            [
                (Guid.NewGuid(), Request(firstRoom, "10:00", "11:00", anna)),
                (Guid.NewGuid(), Request(secondRoom, "10:00", "11:00", anna))
            ]);

            Assert.Single(results, result => result.Response.StatusCode == HttpStatusCode.Created);
            var rejected = Assert.Single(results, result => result.Response.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, rejected.Response.StatusCode);

            // Con due sole richieste l'attesa del lock non deve arrivare a 10 secondi.
            var problem = await ReadProblemAsync(rejected.Response);
            if (problem.Title == "Prenotazione in corso")
            {
                Assert.True(
                    rejected.Elapsed > UserLockTimeout,
                    $"«Prenotazione in corso» dopo {rejected.Elapsed.TotalSeconds:F1} s, prima del timeout del lock.");
            }
            else
            {
                Assert.Equal("Membro già impegnato", problem.Title);
            }

            Assert.Equal(1, await CountConfirmedForPersonInSlotAsync(anna, "10:00", "11:00"));
        }
    }

    [Fact]
    public async Task Create_CrossedOrganizers_ConfirmsOneWithoutWaitingForTheLockTimeout()
    {
        fixture.Identity.Reset();

        for (var repetition = 1; repetition <= Repetitions; repetition++)
        {
            var mario = Guid.NewGuid();
            var anna = Guid.NewGuid();
            var marioRoom = await SeedRoomAsync();
            var annaRoom = await SeedRoomAsync();

            // Mario prenota con Anna come membro e, nello stesso istante, Anna prenota con Mario come membro:
            // i lock sulle persone in ordine di id evitano che le due richieste si blocchino a vicenda.
            var results = await SendInParallelAsync(
            [
                (mario, Request(marioRoom, "10:00", "11:00", anna)),
                (anna, Request(annaRoom, "10:00", "11:00", mario))
            ]);

            Assert.Single(results, result => result.Response.StatusCode == HttpStatusCode.Created);
            var rejected = Assert.Single(results, result => result.Response.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, rejected.Response.StatusCode);
            Assert.All(results, result => Assert.True(
                result.Elapsed < UserLockTimeout,
                $"Richiesta durata {result.Elapsed.TotalSeconds:F1} s: ha atteso il timeout del lock."));

            Assert.Equal(1, await CountConfirmedForPersonInSlotAsync(mario, "10:00", "11:00"));
            Assert.Equal(1, await CountConfirmedForPersonInSlotAsync(anna, "10:00", "11:00"));
        }
    }

    [Fact]
    public async Task Create_WhileAnotherTransactionHoldsAMemberLock_Returns409WithTheMemberMessage()
    {
        fixture.Identity.Reset();
        var anna = Guid.NewGuid();
        var roomId = await SeedRoomAsync();

        await using var connection = await OpenServiceDatabaseConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await HoldUserLockAsync(connection, transaction, anna);

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/bookings", Request(roomId, "10:00", "11:00", anna));

        await transaction.RollbackAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal("Prenotazione in corso", problem.GetProperty("title").GetString());
        Assert.Equal(
            "Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.",
            problem.GetProperty("detail").GetString());
        Assert.Equal(RejectionCodes.MemberBookingInProgress, problem.GetProperty("code").GetString());
        Assert.Equal(0, await CountReservationsAsync(roomId));
    }

    // Connessione separata allo stesso database del servizio: il lock deve stare dove la POST lo cerca.
    private async Task<SqlConnection> OpenServiceDatabaseConnectionAsync()
    {
        string connectionString;
        string serviceDatabase;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            connectionString = db.Database.GetConnectionString()!;
            serviceDatabase = await db.Database.SqlQuery<string>($"SELECT DB_NAME() AS [Value]").SingleAsync();
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(serviceDatabase, connection.Database);
        return connection;
    }

    // Stessa risorsa e stessa modalità della POST: il lock resta finché la transazione è aperta.
    private static async Task HoldUserLockAsync(SqlConnection connection, SqlTransaction transaction, Guid userId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
            SELECT @result;
            """;
        command.Parameters.AddWithValue("@resource", $"user:{userId}");
        var lockResult = (int)(await command.ExecuteScalarAsync())!;
        Assert.True(lockResult >= 0, $"sp_getapplock ha restituito {lockResult}.");
    }

    // Prenotazioni confermate della persona, come organizzatore o come membro, sovrapposte alla fascia [start, end).
    private async Task<int> CountConfirmedForPersonInSlotAsync(Guid userId, string start, string end)
    {
        var (from, to) = (At(start), At(end));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.CountAsync(reservation =>
            (reservation.OrganizerId == userId || reservation.Members.Any(member => member.UserId == userId)) &&
            reservation.Status == ReservationStatus.Confirmed &&
            reservation.Start < to &&
            reservation.End > from);
    }

    // --- Richieste in parallelo ---

    private sealed record ParallelResult(HttpResponseMessage Response, TimeSpan Elapsed);

    // Ogni richiesta ha il proprio HttpClient e il proprio token; tutte attendono lo stesso segnale
    // e partono insieme, così si sovrappongono davvero nel tempo. Elapsed misura l'attesa di ciascuna.
    private async Task<IReadOnlyList<ParallelResult>> SendInParallelAsync(
        IReadOnlyList<(Guid UserId, CreateReservationRequest Body)> requests)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clients = requests.Select(request => ClientFor(request.UserId)).ToList();

        try
        {
            var tasks = requests.Select((request, index) => Task.Run(async () =>
            {
                await start.Task;
                var stopwatch = Stopwatch.StartNew();
                var response = await clients[index].PostAsJsonAsync("/api/bookings", request.Body);
                return new ParallelResult(response, stopwatch.Elapsed);
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

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.NotNull(problem);
        return problem;
    }

    // --- Dati di prova ---

    // Spazio aperto tutti i giorni 08:00-20:00, con un Id nuovo per ogni chiamata.
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

    // Prenotazioni confermate dello spazio che si sovrappongono alla fascia [start, end).
    private async Task<int> CountConfirmedInSlotAsync(Guid roomId, string start, string end)
    {
        var (from, to) = (At(start), At(end));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.CountAsync(reservation =>
            reservation.RoomId == roomId &&
            reservation.Status == ReservationStatus.Confirmed &&
            reservation.Start < to &&
            reservation.End > from);
    }

    // Prenotazioni confermate dell'organizzatore, in qualunque spazio, che si sovrappongono alla fascia [start, end).
    private async Task<int> CountConfirmedForOrganizerInSlotAsync(Guid organizerId, string start, string end)
    {
        var (from, to) = (At(start), At(end));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.Reservations.CountAsync(reservation =>
            reservation.OrganizerId == organizerId &&
            reservation.Status == ReservationStatus.Confirmed &&
            reservation.Start < to &&
            reservation.End > from);
    }

    // Client con un token valido, firmato con la chiave che Bookings usa nei test.
    private HttpClient ClientFor(Guid userId) => fixture.CreateClientFor(userId);

    private static CreateReservationRequest Request(Guid roomId, string start, string end, params Guid[] memberIds) =>
        new(roomId, At(start), At(end), Purpose: null, memberIds.Length == 0 ? null : memberIds);

    private static DateTimeOffset At(string time)
    {
        var local = Day.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
