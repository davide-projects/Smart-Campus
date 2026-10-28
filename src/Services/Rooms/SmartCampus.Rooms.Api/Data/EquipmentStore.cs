using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace SmartCampus.Rooms.Data;

internal sealed class EquipmentStore
{
    private readonly IMongoCollection<MongoEquipmentDocument> _equipment;
    private readonly IMongoCollection<MongoRoomEquipmentDocument> _roomEquipment;

    public EquipmentStore(IMongoDatabase database)
    {
        _equipment = database.GetCollection<MongoEquipmentDocument>("Equipment");
        _roomEquipment = database.GetCollection<MongoRoomEquipmentDocument>("RoomEquipment");
    }

    public async Task<IReadOnlyList<EquipmentRecord>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var stringIds = ids.Select(id => id.ToString("D")).ToArray();
        var documents = await _equipment
            .Find(Builders<MongoEquipmentDocument>.Filter.In(item => item.Id, stringIds))
            .ToListAsync(cancellationToken);

        return documents.Select(item => new EquipmentRecord(Guid.Parse(item.Id), item.Name)).ToArray();
    }

    public async Task<IReadOnlyList<EquipmentRecord>> GetForRoomAsync(
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var equipmentByRoom = await GetForRoomsAsync([roomId], cancellationToken);
        return equipmentByRoom.TryGetValue(roomId, out var equipment) ? equipment : [];
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<EquipmentRecord>>> GetForRoomsAsync(
        IReadOnlyCollection<Guid> roomIds,
        CancellationToken cancellationToken)
    {
        if (roomIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<EquipmentRecord>>();
        }

        var roomIdStrings = roomIds.Select(id => id.ToString("D")).ToArray();
        var mappings = await _roomEquipment
            .Find(Builders<MongoRoomEquipmentDocument>.Filter.In(item => item.Id, roomIdStrings))
            .ToListAsync(cancellationToken);

        if (mappings.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<EquipmentRecord>>();
        }

        var equipmentIds = mappings
            .SelectMany(mapping => mapping.EquipmentIds)
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();
        var equipmentById = (await GetByIdsAsync(equipmentIds, cancellationToken))
            .ToDictionary(item => item.Id);

        return mappings.ToDictionary(
            mapping => Guid.Parse(mapping.Id),
            // Un'assegnazione che punta a un'attrezzatura non più nel catalogo viene ignorata
            // invece di far fallire l'intera richiesta con un'eccezione.
            mapping => (IReadOnlyList<EquipmentRecord>)mapping.EquipmentIds
                .Select(id => equipmentById.TryGetValue(Guid.Parse(id), out var record) ? record : null)
                .Where(record => record is not null)
                .Select(record => record!)
                .ToArray());
    }

    public async Task<IReadOnlySet<Guid>> FindRoomIdsWithAllEquipmentAsync(
        IReadOnlyCollection<Guid> equipmentIds,
        CancellationToken cancellationToken)
    {
        if (equipmentIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var stringIds = equipmentIds.Select(id => id.ToString("D")).ToArray();
        var mappings = await _roomEquipment
            .Find(Builders<MongoRoomEquipmentDocument>.Filter.All(item => item.EquipmentIds, stringIds))
            .Project(item => item.Id)
            .ToListAsync(cancellationToken);

        return mappings.Select(Guid.Parse).ToHashSet();
    }

    public async Task SetRoomEquipmentAsync(
        Guid roomId,
        IReadOnlyCollection<EquipmentRecord> equipment,
        CancellationToken cancellationToken)
    {
        // Anche a lista vuota si salva il documento invece di cancellarlo: EnsureSeededAsync fa
        // l'upsert delle assegnazioni di seed con SetOnInsert, quindi cancellare il documento di
        // uno spazio del seed ne farebbe tornare le attrezzature al riavvio successivo.
        var document = new MongoRoomEquipmentDocument
        {
            Id = roomId.ToString("D"),
            EquipmentIds = equipment.Select(item => item.Id.ToString("D")).OrderBy(value => value).ToList()
        };

        await _roomEquipment.ReplaceOneAsync(
            item => item.Id == document.Id,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task ImportLegacyDataAsync(
        IReadOnlyCollection<EquipmentRecord> equipment,
        IReadOnlyCollection<RoomEquipmentAssignment> assignments,
        CancellationToken cancellationToken)
    {
        await _equipment.DeleteManyAsync(FilterDefinition<MongoEquipmentDocument>.Empty, cancellationToken);
        var equipmentDocuments = equipment
            .Select(item => new MongoEquipmentDocument { Id = item.Id.ToString("D"), Name = item.Name })
            .ToArray();
        if (equipmentDocuments.Length > 0)
        {
            await _equipment.InsertManyAsync(equipmentDocuments, cancellationToken: cancellationToken);
        }

        await _roomEquipment.DeleteManyAsync(FilterDefinition<MongoRoomEquipmentDocument>.Empty, cancellationToken);
        foreach (var grouping in assignments.GroupBy(item => item.RoomId))
        {
            var document = new MongoRoomEquipmentDocument
            {
                Id = grouping.Key.ToString("D"),
                EquipmentIds = grouping.Select(item => item.EquipmentId.ToString("D")).Distinct().OrderBy(value => value).ToList()
            };
            await _roomEquipment.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        foreach (var item in EquipmentSeedData.Catalog)
        {
            var document = new MongoEquipmentDocument { Id = item.Id.ToString("D"), Name = item.Name };
            await _equipment.UpdateOneAsync(
                current => current.Id == document.Id,
                Builders<MongoEquipmentDocument>.Update.SetOnInsert(current => current.Name, document.Name),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }

        foreach (var grouping in EquipmentSeedData.Assignments.GroupBy(item => item.RoomId))
        {
            var document = new MongoRoomEquipmentDocument
            {
                Id = grouping.Key.ToString("D"),
                EquipmentIds = grouping.Select(item => item.EquipmentId.ToString("D")).OrderBy(value => value).ToList()
            };
            await _roomEquipment.UpdateOneAsync(
                current => current.Id == document.Id,
                Builders<MongoRoomEquipmentDocument>.Update.SetOnInsert(current => current.EquipmentIds, document.EquipmentIds),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }

        var index = new CreateIndexModel<MongoRoomEquipmentDocument>(
            Builders<MongoRoomEquipmentDocument>.IndexKeys.Ascending(item => item.EquipmentIds));
        await _roomEquipment.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    private sealed class MongoEquipmentDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MongoRoomEquipmentDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("equipmentIds")]
        public List<string> EquipmentIds { get; set; } = [];
    }
}
