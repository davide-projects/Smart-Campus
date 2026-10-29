using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.IntegrationTests;

// US-06: ogni rifiuto restituisce un ProblemDetails con il codice di motivo e i limiti da rispettare;
// con lo spazio occupato la risposta include anche le fasce libere dello stesso spazio nello stesso giorno
// (o un elenco vuoto), verificato sull'endpoint reale con SQL Server (Testcontainers).
// "members-occupied" e "capacity-exceeded" arrivano con le prenotazioni di gruppo (US-05).
[Collection(BookingsApiCollection.Name)]
public sealed class RejectionResponseTests(BookingsApiFixture fixture)
{
    // Stesse opzioni JSON dell'API: gli orari si leggono come DateTimeOffset.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // Dopodomani: sempre nel futuro e sempre entro i 14 giorni (A2), qualunque sia la data di esecuzione.
    private static readonly DateOnly Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).Date).AddDays(2);

    // --- Spazio occupato: le fasce libere ---

    [Fact]
    public async Task OccupiedRoom_Returns409WithTheFreeRangesOfTheSameDay()
    {
        var roomId = await SeedRoomAsync();
        await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:30", "11:30"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.RoomOccupied);
        Assert.Equal("Lo spazio è già prenotato in questa fascia.", problem.GetProperty("detail").GetString());
        var freeSlots = problem.GetProperty("freeSlots").Deserialize<List<FreeRange>>(Json)!;
        Assert.Equal(
            new[] { new FreeRange(At("08:00"), At("10:00")), new FreeRange(At("11:00"), At("20:00")) },
            freeSlots);
    }

    [Fact]
    public async Task OccupiedRoomWithNoFreeTimeThatDay_Returns409WithAnEmptyList()
    {
        var roomId = await SeedRoomAsync(opensAt: 10, closesAt: 11);
        await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.RoomOccupied);
        var freeSlots = problem.GetProperty("freeSlots");
        Assert.Equal(JsonValueKind.Array, freeSlots.ValueKind);
        Assert.Equal(0, freeSlots.GetArrayLength());
    }

    // --- Persona occupata ---

    [Fact]
    public async Task SamePersonInAnotherRoomAtTheSameTime_Returns409WithThePersonOccupiedCode()
    {
        var firstRoom = await SeedRoomAsync();
        var secondRoom = await SeedRoomAsync();
        var userId = Guid.NewGuid();
        await Post(userId, Request(firstRoom, "10:00", "11:00"));

        var response = await Post(userId, Request(secondRoom, "10:30", "11:30"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict, RejectionCodes.PersonOccupied);
        Assert.False(problem.TryGetProperty("freeSlots", out _));
    }

    // --- Orari di apertura e stato dello spazio ---

    [Fact]
    public async Task OutsideOpeningHours_Returns422WithTheOpeningTimesInTheLimits()
    {
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "19:00", "21:00"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.OutsideOpeningHours);
        var limits = problem.GetProperty("limits");
        Assert.Equal("08:00", limits.GetProperty("opensAt").GetString());
        Assert.Equal("20:00", limits.GetProperty("closesAt").GetString());
    }

    [Fact]
    public async Task ClosedDay_Returns422WithTheRoomClosedCode()
    {
        var roomId = await SeedRoomAsync(openDays: Enum.GetValues<DayOfWeek>().Where(day => day != Day.DayOfWeek));

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.RoomClosed);
    }

    [Fact]
    public async Task NotBookableRoom_Returns422WithTheRoomNotBookableCode()
    {
        var roomId = await SeedRoomAsync(isBookable: false);

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:00", "11:00"));

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.RoomNotBookable);
    }

    [Fact]
    public async Task StartAndEndOnDifferentDays_Returns422WithTheDifferentDaysCode()
    {
        var roomId = await SeedRoomAsync();
        var request = new CreateReservationRequest(roomId, At("23:00"), AtOn(Day.AddDays(1), "01:00"), Purpose: null);

        var response = await Post(Guid.NewGuid(), request);

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.DifferentDays);
    }

    // --- Regole sulla richiesta: codice e limiti ---

    [Fact]
    public async Task EndNotAfterStart_Returns422WithTheInvalidTimeRangeCode()
    {
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "11:00", "11:00"));

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.InvalidTimeRange);
    }

    [Fact]
    public async Task InThePast_Returns422WithTheInThePastCode()
    {
        var roomId = await SeedRoomAsync();
        var request = new CreateReservationRequest(roomId, AtOn(Day.AddDays(-3), "10:00"), AtOn(Day.AddDays(-3), "11:00"), Purpose: null);

        var response = await Post(Guid.NewGuid(), request);

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.InThePast);
    }

    [Fact]
    public async Task NotOnHalfHour_Returns422WithTheSlotLengthInTheLimits()
    {
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "10:15", "11:00"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.SlotBoundary);
        Assert.Equal(30, problem.GetProperty("limits").GetProperty("slotMinutes").GetInt32());
    }

    [Fact]
    public async Task PurposeTooLong_Returns422WithTheMaximumLengthInTheLimits()
    {
        var roomId = await SeedRoomAsync();
        var request = new CreateReservationRequest(roomId, At("10:00"), At("11:00"), new string('x', 201));

        var response = await Post(Guid.NewGuid(), request);

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.PurposeTooLong);
        Assert.Equal(200, problem.GetProperty("limits").GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public async Task LongerThanFourHours_Returns422WithTheMaximumDurationInTheLimits()
    {
        var roomId = await SeedRoomAsync();

        var response = await Post(Guid.NewGuid(), Request(roomId, "08:00", "12:30"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.DurationExceeded);
        Assert.Equal(240, problem.GetProperty("limits").GetProperty("maxDurationMinutes").GetInt32());
    }

    [Fact]
    public async Task MoreThanFourteenDaysAhead_Returns422WithTheMaximumAdvanceInTheLimits()
    {
        var roomId = await SeedRoomAsync();
        var request = new CreateReservationRequest(roomId, AtOn(Day.AddDays(20), "10:00"), AtOn(Day.AddDays(20), "11:00"), Purpose: null);

        var response = await Post(Guid.NewGuid(), request);

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.TooFarAhead);
        Assert.Equal(14, problem.GetProperty("limits").GetProperty("maxAdvanceDays").GetInt32());
    }

    // --- Cancellazione ---

    [Fact]
    public async Task CancellationByAStranger_Returns403WithTheForbiddenCode()
    {
        var roomId = await SeedRoomAsync();
        var reservationId = await SeedReservationAsync(roomId, organizerId: Guid.NewGuid(), start: At("10:00"));

        var response = await fixture.CreateClientFor(Guid.NewGuid()).DeleteAsync($"/api/bookings/{reservationId}");

        await ReadProblemAsync(response, HttpStatusCode.Forbidden, RejectionCodes.CancellationForbidden);
    }

    [Fact]
    public async Task CancellationOfAStartedReservation_Returns422WithTheTooLateCode()
    {
        var roomId = await SeedRoomAsync();
        var organizerId = Guid.NewGuid();
        var reservationId = await SeedReservationAsync(roomId, organizerId, start: DateTimeOffset.UtcNow.AddHours(-1));

        var response = await fixture.CreateClientFor(organizerId).DeleteAsync($"/api/bookings/{reservationId}");

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, RejectionCodes.CancellationTooLate);
    }

    // --- Verifiche e dati di prova ---

    // Controlla stato e codice di motivo, e restituisce il corpo perché il test possa leggere limiti e fasce.
    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement.Clone();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    private Task<HttpResponseMessage> Post(Guid userId, CreateReservationRequest request) =>
        fixture.CreateClientFor(userId).PostAsJsonAsync("/api/bookings", request);

    // Spazio con un Id nuovo per ogni test; di default aperto tutti i giorni 08:00-20:00.
    private async Task<Guid> SeedRoomAsync(
        int opensAt = 8,
        int closesAt = 20,
        bool isBookable = true,
        IEnumerable<DayOfWeek>? openDays = null)
    {
        var roomId = Guid.NewGuid();
        var room = new BookableRoom { RoomId = roomId, Name = "Aula test", Capacity = 30, IsBookable = isBookable, IsActive = true };

        foreach (var day in openDays ?? Enum.GetValues<DayOfWeek>())
        {
            room.OpeningHours.Add(new BookableRoomOpeningHours
            {
                RoomId = roomId,
                DayOfWeek = day,
                OpensAt = new TimeOnly(opensAt, 0),
                ClosesAt = new TimeOnly(closesAt, 0)
            });
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.BookableRooms.Add(room);
        await db.SaveChangesAsync();

        return roomId;
    }

    // Prenotazione confermata inserita direttamente nel database, per provare la cancellazione.
    private async Task<Guid> SeedReservationAsync(Guid roomId, Guid organizerId, DateTimeOffset start)
    {
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            OrganizerId = organizerId,
            Start = start,
            End = start.AddHours(1),
            Participants = 1,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        return reservation.Id;
    }

    private static CreateReservationRequest Request(Guid roomId, string start, string end) =>
        new(roomId, At(start), At(end), Purpose: null);

    private static DateTimeOffset At(string time) => AtOn(Day, time);

    private static DateTimeOffset AtOn(DateOnly day, string time)
    {
        var local = day.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
