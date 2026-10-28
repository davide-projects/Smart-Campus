using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace SmartCampus.Rooms.Data;

// Copia verso MongoDB il catalogo e le assegnazioni rimasti nelle tabelle SQL Equipment e
// RoomEquipment. L'import DEVE avvenire prima della migrazione MoveEquipmentToMongoDb:
// quella migrazione elimina proprio queste due tabelle e, se l'import avvenisse dopo,
// i dati storici non sarebbero più leggibili. Per questo Program.cs migra a passi
// fino alla migrazione precedente, chiama ImportAsync e poi completa lo schema.
internal sealed class LegacyEquipmentDataImporter(RoomsDbContext db, EquipmentStore equipmentStore)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeConnection = connection.State != System.Data.ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var equipment = await ReadEquipmentAsync(connection, cancellationToken);
            var assignments = await ReadAssignmentsAsync(connection, cancellationToken);
            await equipmentStore.ImportLegacyDataAsync(equipment, assignments, cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<EquipmentRecord>> ReadEquipmentAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Id], [Name] FROM [dbo].[Equipment]";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var equipment = new List<EquipmentRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            equipment.Add(new EquipmentRecord(reader.GetGuid(0), reader.GetString(1)));
        }

        return equipment;
    }

    private static async Task<IReadOnlyList<RoomEquipmentAssignment>> ReadAssignmentsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [RoomId], [EquipmentId] FROM [dbo].[RoomEquipment]";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<RoomEquipmentAssignment>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new RoomEquipmentAssignment(reader.GetGuid(0), reader.GetGuid(1)));
        }

        return assignments;
    }
}
