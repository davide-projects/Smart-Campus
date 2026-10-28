using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;

namespace SmartCampus.Bookings.IntegrationTests;

// Ricerca degli spazi liberi in una fascia oraria: aperti per tutta la fascia e senza prenotazioni
// confermate sovrapposte. Le adiacenti (che finiscono quando la fascia comincia, o cominciano quando
// finisce) non bloccano lo spazio. Ogni test usa un tipo di spazio proprio, così nel database
// condiviso con gli altri test la ricerca vede solo i suoi spazi.
[Collection(BookingsApiCollection.Name)]
public sealed class AvailabilitySearchTests(BookingsApiFixture fixture)
{
    // Giugno: in Italia l'offset è +02:00, lontano dai passaggi di ora legale.
    private static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    private static readonly DateOnly Day = new(2031, 6, 10);

    // La ricerca è per tutti gli utenti autenticati: il client di prova è uno studente.
    private readonly HttpClient _client = fixture.CreateClientFor(Guid.NewGuid());

    [Fact]
    public async Task Search_ExcludesRoomsWithOverlappingConfirmedReservations()
    {
        var type = NewType();
        var free = NewRoom(type);
        var endsInsideTheSlot = NewRoom(type);
        var startsInsideTheSlot = NewRoom(type);
        var insideTheSlot = NewRoom(type);
        var coveringTheSlot = NewRoom(type);

        await SeedAsync(db =>
        {
            db.BookableRooms.AddRange(free, endsInsideTheSlot, startsInsideTheSlot, insideTheSlot, coveringTheSlot);
            db.Reservations.AddRange(
                Reserve(endsInsideTheSlot, At(9), At(11)),       // inizia prima e finisce dentro la fascia 10-12
                Reserve(startsInsideTheSlot, At(11), At(13)),    // inizia dentro e finisce dopo
                Reserve(insideTheSlot, At(10, 30), At(11)),      // tutta dentro
                Reserve(coveringTheSlot, At(9), At(13)));        // la copre per intero
        });

        var rooms = await SearchAsync(type, "10:00", "12:00");

        var found = Assert.Single(rooms);
        Assert.Equal(free.RoomId, found.RoomId);
    }

    [Fact]
    public async Task Search_IncludesRoomsWithAdjacentReservations()
    {
        var type = NewType();
        var endsWhenTheSlotStarts = NewRoom(type);
        var startsWhenTheSlotEnds = NewRoom(type);
        var adjacentOnBothSides = NewRoom(type);

        await SeedAsync(db =>
        {
            db.BookableRooms.AddRange(endsWhenTheSlotStarts, startsWhenTheSlotEnds, adjacentOnBothSides);
            db.Reservations.AddRange(
                Reserve(endsWhenTheSlotStarts, At(8), At(10)),   // finisce alle 10:00, la fascia comincia alle 10:00
                Reserve(startsWhenTheSlotEnds, At(12), At(14)),  // comincia alle 12:00, la fascia finisce alle 12:00
                Reserve(adjacentOnBothSides, At(8), At(10)),
                Reserve(adjacentOnBothSides, At(12), At(14)));
        });

        var rooms = await SearchAsync(type, "10:00", "12:00");

        Assert.Equivalent(
            new[] { endsWhenTheSlotStarts.RoomId, startsWhenTheSlotEnds.RoomId, adjacentOnBothSides.RoomId },
            rooms.Select(room => room.RoomId).ToArray());
    }

    [Fact]
    public async Task Search_IgnoresCancelledReservations()
    {
        var type = NewType();
        var room = NewRoom(type);

        await SeedAsync(db =>
        {
            db.BookableRooms.Add(room);
            db.Reservations.Add(Reserve(room, At(10), At(12), ReservationStatus.Cancelled));
        });

        var rooms = await SearchAsync(type, "10:00", "12:00");

        Assert.Equal(room.RoomId, Assert.Single(rooms).RoomId);
    }

    [Fact]
    public async Task Search_ExcludesRoomsNotOpenForTheWholeSlot()
    {
        var type = NewType();
        var openForTheWholeSlot = NewRoom(type, opensAt: 10, closesAt: 12);   // chiude proprio quando la fascia finisce
        var closesBeforeTheEnd = NewRoom(type, opensAt: 8, closesAt: 11);
        var opensAfterTheStart = NewRoom(type, opensAt: 11, closesAt: 18);
        var closedThatDay = NewRoom(type, openOn: Day.AddDays(1).DayOfWeek);

        await SeedAsync(db => db.BookableRooms.AddRange(openForTheWholeSlot, closesBeforeTheEnd, opensAfterTheStart, closedThatDay));

        var rooms = await SearchAsync(type, "10:00", "12:00");

        Assert.Equal(openForTheWholeSlot.RoomId, Assert.Single(rooms).RoomId);
    }

    [Fact]
    public async Task Search_ExcludesInactiveAndNotBookableRooms()
    {
        var type = NewType();
        var bookable = NewRoom(type);
        var inactive = NewRoom(type, isActive: false);
        var notBookable = NewRoom(type, isBookable: false);

        await SeedAsync(db => db.BookableRooms.AddRange(bookable, inactive, notBookable));

        var rooms = await SearchAsync(type, "10:00", "12:00");

        Assert.Equal(bookable.RoomId, Assert.Single(rooms).RoomId);
    }

    [Fact]
    public async Task Search_FiltersByMinimumCapacity()
    {
        var type = NewType();
        var small = NewRoom(type, capacity: 10);
        var exactlyEnough = NewRoom(type, capacity: 20);
        var large = NewRoom(type, capacity: 30);

        await SeedAsync(db => db.BookableRooms.AddRange(small, exactlyEnough, large));

        var rooms = await SearchAsync(type, "10:00", "12:00", minCapacity: 20);

        Assert.Equivalent(
            new[] { exactlyEnough.RoomId, large.RoomId },
            rooms.Select(room => room.RoomId).ToArray());
    }

    [Fact]
    public async Task Search_FiltersByType()
    {
        var wantedType = NewType();
        var otherType = NewType();
        var wanted = NewRoom(wantedType);
        var other = NewRoom(otherType);

        await SeedAsync(db => db.BookableRooms.AddRange(wanted, other));

        var rooms = await SearchAsync(wantedType, "10:00", "12:00");

        var found = Assert.Single(rooms);
        Assert.Equal(wanted.RoomId, found.RoomId);
        Assert.Equal(wantedType, found.Type);
    }

    [Theory]
    [InlineData("12:00", "10:00")]
    [InlineData("10:00", "10:00")]
    public async Task Search_WithEndNotAfterStart_ReturnsBadRequest(string start, string end)
    {
        var response = await _client.GetAsync(
            $"/api/bookings/availability/search?date={Day:yyyy-MM-dd}&start={start}&end={end}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutToken_ReturnsUnauthorized()
    {
        var response = await fixture.Factory.CreateClient().GetAsync(
            $"/api/bookings/availability/search?date={Day:yyyy-MM-dd}&start=10:00&end=12:00");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<List<AvailableRoomResponse>> SearchAsync(string type, string start, string end, int? minCapacity = null)
    {
        var url = $"/api/bookings/availability/search?date={Day:yyyy-MM-dd}&start={start}&end={end}&type={type}";

        if (minCapacity is not null)
        {
            url += $"&minCapacity={minCapacity}";
        }

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AvailableRoomResponse>>())!;
    }

    // Inserisce i dati di prova direttamente nel database del test.
    private async Task SeedAsync(Action<BookingDbContext> add)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        add(db);
        await db.SaveChangesAsync();
    }

    // Un tipo diverso per ogni test: i filtri della ricerca vedono solo gli spazi creati dal test.
    private static string NewType() => "T" + Guid.NewGuid().ToString("N")[..20];

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(Day.ToDateTime(new TimeOnly(hour, minute)), CampusOffset);

    private static BookableRoom NewRoom(
        string type,
        int capacity = 30,
        int opensAt = 8,
        int closesAt = 20,
        DayOfWeek? openOn = null,
        bool isActive = true,
        bool isBookable = true)
    {
        var room = new BookableRoom
        {
            RoomId = Guid.NewGuid(),
            Name = $"Test room {Guid.NewGuid():N}",
            Type = type,
            Capacity = capacity,
            IsBookable = isBookable,
            IsActive = isActive
        };

        room.OpeningHours.Add(new BookableRoomOpeningHours
        {
            RoomId = room.RoomId,
            DayOfWeek = openOn ?? Day.DayOfWeek,
            OpensAt = new TimeOnly(opensAt, 0),
            ClosesAt = new TimeOnly(closesAt, 0)
        });

        return room;
    }

    private static Reservation Reserve(
        BookableRoom room,
        DateTimeOffset start,
        DateTimeOffset end,
        ReservationStatus status = ReservationStatus.Confirmed) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = room.RoomId,
        OrganizerId = Guid.NewGuid(),
        Start = start,
        End = end,
        Participants = 1,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        CancelledAt = status == ReservationStatus.Cancelled ? DateTimeOffset.UtcNow : null
    };
}
