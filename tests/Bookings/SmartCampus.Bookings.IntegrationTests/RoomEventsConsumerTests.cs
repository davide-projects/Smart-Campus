using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data.Context;
using SmartCampus.Contracts;

namespace SmartCampus.Bookings.IntegrationTests;

// Criteri di completamento della #9: la copia locale degli spazi segue gli eventi di Rooms
// e un evento ricevuto due volte non crea duplicati.
public sealed class RoomEventsConsumerTests(BookingsApiFixture fixture) : IClassFixture<BookingsApiFixture>
{
    [Fact]
    public async Task RoomUpdated_AfterRoomCreated_UpdatesTheLocalCopy()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var harness = fixture.Factory.Services.GetTestHarness();

        // Act
        await harness.Bus.Publish(new RoomCreated(roomId, "Aula A1", 30, IsBookable: true));
        Assert.True(await harness.Consumed.Any<RoomCreated>(x => x.Context.Message.RoomId == roomId));

        await harness.Bus.Publish(new RoomUpdated(roomId, "Aula A1", 40, IsBookable: true, IsActive: true));
        Assert.True(await harness.Consumed.Any<RoomUpdated>(x => x.Context.Message.RoomId == roomId));

        // Assert
        var room = await FindRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal(40, room.Capacity);
        Assert.True(room.IsActive);
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

    // Legge la copia locale con un DbContext nuovo, così vede i dati realmente salvati nel database.
    private async Task<Api.Entities.BookableRoom?> FindRoomAsync(Guid roomId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.BookableRooms.AsNoTracking().SingleOrDefaultAsync(room => room.RoomId == roomId);
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