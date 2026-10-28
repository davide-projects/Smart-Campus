using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartCampus.Bookings.Api.Data.Context;

namespace SmartCampus.Bookings.IntegrationTests;

// Task #23, punto 2.1: la migrazione che aggiunge BuildingName e OrganizerName non riempie le righe esistenti.
// Usa un database a parte nello stesso container, per partire dalla versione precedente dello schema.
[Collection(BookingsApiCollection.Name)]
public sealed class BuildingAndOrganizerNamesMigrationTests(BookingsApiFixture fixture)
{
    private const string MigrationName = "AddBuildingAndOrganizerNames";

    [Fact]
    public async Task Migration_OnDatabaseWithRoomsAndReservations_LeavesTheNewColumnsNull()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"BookingsMigration_{Guid.NewGuid():N}"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<BookingDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new BookingDbContext(options);

        try
        {
            // Arrange: schema fermo alla migrazione precedente, con uno spazio e una prenotazione.
            var migrations = db.Database.GetMigrations().ToList();
            var index = migrations.FindIndex(migration => migration.EndsWith($"_{MigrationName}", StringComparison.Ordinal));
            Assert.True(index > 0, $"Migrazione {MigrationName} non trovata o senza una versione precedente.");

            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[index - 1]);

            var roomId = Guid.NewGuid();
            var reservationId = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO BookableRooms (RoomId, Name, Capacity, IsBookable, IsActive)
                VALUES ({roomId}, N'Aula esistente', 30, 1, 1);

                INSERT INTO Reservations (Id, RoomId, OrganizerId, Start, [End], Participants, Purpose, Status, CreatedAt, CancelledAt)
                VALUES ({reservationId}, {roomId}, {Guid.NewGuid()}, '2026-11-02T10:00:00+01:00', '2026-11-02T11:00:00+01:00',
                        1, NULL, N'Confirmed', '2026-10-27T09:00:00+00:00', NULL);
                """);

            // Act: applica la nuova migrazione (e le eventuali successive).
            await migrator.MigrateAsync();

            // Assert: le righe esistenti restano con NULL nelle nuove colonne.
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());

            var room = await db.BookableRooms.AsNoTracking().SingleAsync(existing => existing.RoomId == roomId);
            var reservation = await db.Reservations.AsNoTracking().SingleAsync(existing => existing.Id == reservationId);

            Assert.Null(room.BuildingName);
            Assert.Equal("Aula esistente", room.Name);
            Assert.Null(reservation.OrganizerName);
            Assert.Equal(roomId, reservation.RoomId);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
