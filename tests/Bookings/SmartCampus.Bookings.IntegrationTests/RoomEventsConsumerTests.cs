using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using MassTransit;
using SmartCampus.Contracts;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri di completamento della #9: la copia locale degli spazi segue gli eventi di Rooms
// e un evento ricevuto due volte non crea duplicati.
[Collection(BookingsApiCollection.Name)]
public sealed class RoomEventsConsumerTests(BookingsApiFixture fixture)
{
    [Fact]
    public async Task RoomUpdated_AfterRoomCreated_UpdatesTheLocalCopy()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        var mondayHours = new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        var tuesdayHours = new RoomOpeningHours(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0));

        // Act
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true)
        {
            OpeningHours = [mondayHours]
        });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1", 40, IsBookable: true, IsActive: true)
        {
            OpeningHours = [tuesdayHours]
        });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomUpdated>(x => x.Context.Message.RoomId == roomId).Any());

        // Assert
        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal(40, room.Capacity);
        Assert.True(room.IsActive);
        Assert.Collection(room.OpeningHours,
            hours =>
            {
                Assert.Equal(DayOfWeek.Tuesday, hours.DayOfWeek);
                Assert.Equal(tuesdayHours.OpensAt, hours.OpensAt);
                Assert.Equal(tuesdayHours.ClosesAt, hours.ClosesAt);
            });
    }

    [Fact]
    public async Task RoomCreated_ReceivedTwice_DoesNotCreateDuplicates()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        var message = new RoomCreated(roomId, "Aula B2", 25, IsBookable: true);

        // Act: lo stesso evento consegnato due volte
        await harness.Bus.Publish(message);
        await harness.Bus.Publish(message);

        // Aspetta che il consumer abbia elaborato ENTRAMBE le consegne.
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Count() == 2);

        // Assert
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var count = await db.BookableRooms.CountAsync(room => room.RoomId == roomId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RoomDeactivated_ForKnownRoom_MarksItInactive()
    {
        // Arrange: lo spazio esiste nella copia locale
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula C1", 20, IsBookable: true));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        // Act
        await harness.Bus.Publish(new RoomDeactivated(roomId));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomDeactivated>(x => x.Context.Message.RoomId == roomId).Any());

        // Assert
        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.False(room.IsActive);
    }

    [Fact]
    public async Task RoomDeactivated_ForUnknownRoom_IsIgnoredWithoutErrors()
    {
        // Arrange: nessuno spazio con questo Id
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();

        // Act
        await harness.Bus.Publish(new RoomDeactivated(roomId));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomDeactivated>(x => x.Context.Message.RoomId == roomId).Any());

        // Assert: nessuna riga creata e nessun errore (MassTransit pubblica un Fault quando un consumer fallisce)
        Assert.Null(await FindRoomAsync(roomId));
        Assert.False(await harness.Published.Any<Fault<RoomDeactivated>>(x => x.Context.Message.Message.RoomId == roomId));
    }

    // --- #23: nome dell'edificio nella copia locale ---

    [Fact]
    public async Task RoomCreated_WithBuildingName_SavesIt()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();

        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true) { BuildingName = "Edificio A" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("Edificio A", room.BuildingName);
    }

    [Fact]
    public async Task RoomUpdated_WithAnotherBuilding_ReplacesIt()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true) { BuildingName = "Edificio A" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1", 30, IsBookable: true, IsActive: true) { BuildingName = "Biblioteca" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomUpdated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("Biblioteca", room.BuildingName);
    }

    [Fact]
    public async Task RoomUpdated_WithoutBuildingName_KeepsTheKnownBuildingAndUpdatesTheRest()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true) { BuildingName = "Edificio A" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        // Evento senza il campo, per esempio accodato da una versione di Rooms precedente.
        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1 rinnovata", 45, IsBookable: true, IsActive: true));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomUpdated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("Edificio A", room.BuildingName);
        Assert.Equal("Aula A1 rinnovata", room.Name);
        Assert.Equal(45, room.Capacity);
    }

    [Fact]
    public async Task RoomCreated_WithBuildingNameReceivedTwice_KeepsOneRoomWithTheBuilding()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        var message = new RoomCreated(roomId, "Aula A2", 30, IsBookable: true) { BuildingName = "Edificio A" };

        await harness.Bus.Publish(message);
        await harness.Bus.Publish(message);
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Count() == 2);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var rooms = await db.BookableRooms.AsNoTracking().Where(room => room.RoomId == roomId).ToListAsync();
        var room = Assert.Single(rooms);
        Assert.Equal("Edificio A", room.BuildingName);
    }

    [Fact]
    public async Task RoomDeactivated_KeepsTheBuildingName()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A3", 30, IsBookable: true) { BuildingName = "Edificio A" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        await harness.Bus.Publish(new RoomDeactivated(roomId));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomDeactivated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.False(room.IsActive);
        Assert.Equal("Edificio A", room.BuildingName);
    }

    // --- #43: tipo dello spazio nella copia locale ---

    [Fact]
    public async Task RoomCreated_WithType_SavesIt()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();

        await harness.Bus.Publish(new RoomCreated(roomId, "Laboratorio L1", 20, IsBookable: true) { Type = "Laboratory" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("Laboratory", room.Type);
    }

    [Fact]
    public async Task RoomUpdated_WithAnotherType_ReplacesIt()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true) { Type = "Classroom" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1", 30, IsBookable: true, IsActive: true) { Type = "StudyRoom" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomUpdated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("StudyRoom", room.Type);
    }

    [Fact]
    public async Task RoomUpdated_WithoutType_KeepsTheKnownTypeAndUpdatesTheRest()
    {
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true) { Type = "Classroom" });
        await WaitUntilAsync(() => harness.Consumed.Select<RoomCreated>(x => x.Context.Message.RoomId == roomId).Any());

        // Evento senza il campo, per esempio accodato da una versione di Rooms precedente.
        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1 rinnovata", 45, IsBookable: true, IsActive: true));
        await WaitUntilAsync(() => harness.Consumed.Select<RoomUpdated>(x => x.Context.Message.RoomId == roomId).Any());

        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal("Classroom", room.Type);
        Assert.Equal("Aula A1 rinnovata", room.Name);
        Assert.Equal(45, room.Capacity);
    }

    // Legge la copia locale con un DbContext nuovo, così vede i dati realmente salvati nel database.
    private async Task<Api.Entities.BookableRoom?> FindRoomAsync(Guid roomId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.BookableRooms
            .AsNoTracking()
            .Include(room => room.OpeningHours)
            .SingleOrDefaultAsync(room => room.RoomId == roomId);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("La condizione non si è verificata in tempo.");
            await Task.Delay(100);
        }
    }
}
