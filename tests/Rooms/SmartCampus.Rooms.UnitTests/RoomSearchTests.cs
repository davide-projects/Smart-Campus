using SmartCampus.Rooms.Models;
using SmartCampus.Rooms.Features.Rooms;

namespace SmartCampus.Rooms.UnitTests;

// Regole della ricerca pubblica, verificate su una lista in memoria: niente database, niente HTTP.
public sealed class RoomSearchTests
{
    private static readonly Guid BuildingAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuildingBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Room[] Rooms =
    [
        CreateRoom("Aula A1", RoomType.Classroom, 30, BuildingAId),
        CreateRoom("Aula A2", RoomType.Classroom, 24, BuildingAId),
        CreateRoom("Aula B1", RoomType.Classroom, 35, BuildingBId),
        CreateRoom("Laboratorio Informatica 1", RoomType.Laboratory, 20, BuildingAId),
        CreateRoom("Sala studio A", RoomType.StudyRoom, 12, BuildingAId),
        CreateRoom("Sala studio B", RoomType.StudyRoom, 40, BuildingBId),
        CreateRoom("Aula dismessa", RoomType.Classroom, 50, BuildingAId, isActive: false),
        CreateRoom("Laboratorio dismesso", RoomType.Laboratory, 60, BuildingBId, isActive: false)
    ];

    [Fact]
    public void Search_WithoutFilters_ReturnsOnlyActiveRooms()
    {
        var result = Search(new RoomFilter());

        Assert.Equal(6, result.Count);
        Assert.DoesNotContain(result, room => room.Name.Contains("dismess", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_ByType_ReturnsOnlyMatchingRoomType()
    {
        var result = Search(new RoomFilter(Type: RoomType.StudyRoom));

        Assert.Equal(2, result.Count);
        Assert.All(result, room => Assert.Equal(RoomType.StudyRoom, room.Type));
    }

    [Fact]
    public void Search_ByMinimumCapacity_IncludesBoundary()
    {
        var result = Search(new RoomFilter(MinCapacity: 30));

        // Aula A1 (30) è inclusa: il filtro è "almeno", non "più di".
        Assert.Equal(3, result.Count);
        Assert.All(result, room => Assert.True(room.Capacity >= 30));
        Assert.Contains(result, room => room.Name == "Aula A1");
    }

    [Fact]
    public void Search_ByMaximumCapacity_IncludesBoundary()
    {
        var result = Search(new RoomFilter(MaxCapacity: 24));

        // Aula A2 (24) è inclusa: il filtro è "al massimo", non "meno di".
        Assert.Equal(3, result.Count);
        Assert.All(result, room => Assert.True(room.Capacity <= 24));
        Assert.Contains(result, room => room.Name == "Aula A2");
    }

    [Fact]
    public void Search_ByCapacityRange_IncludesBothBoundaries()
    {
        var result = Search(new RoomFilter(MinCapacity: 20, MaxCapacity: 30));

        Assert.Equal(3, result.Count);
        Assert.All(result, room => Assert.True(room.Capacity >= 20 && room.Capacity <= 30));
    }

    [Fact]
    public void Search_ByBuilding_ReturnsOnlyRoomsInBuilding()
    {
        var result = Search(new RoomFilter(BuildingId: BuildingBId));

        // In edificio B ci sono due spazi attivi: il laboratorio dismesso resta fuori.
        Assert.Equal(2, result.Count);
        Assert.All(result, room => Assert.Equal(BuildingBId, room.BuildingId));
    }

    [Fact]
    public void Search_WithCombinedFilters_AppliesAllConditions()
    {
        var result = Search(new RoomFilter(RoomType.Classroom, MinCapacity: 30, BuildingId: BuildingAId));

        var room = Assert.Single(result);
        Assert.Equal("Aula A1", room.Name);
    }

    [Fact]
    public void Search_WhenOnlyInactiveRoomsMatch_ReturnsEmpty()
    {
        // Anche chiedendo esplicitamente il tipo e la capienza di uno spazio dismesso, non compare.
        var result = Search(new RoomFilter(RoomType.Classroom, MinCapacity: 45));

        Assert.Empty(result);
    }

    [Fact]
    public void Search_WithEquipmentRoomIds_ComposesWithExistingFilters()
    {
        var matchingRoomIds = new[] { Rooms[0].Id, Rooms[2].Id, Rooms[5].Id };
        var result = Search(new RoomFilter(RoomType.Classroom, MinCapacity: 30, BuildingId: BuildingAId), matchingRoomIds);

        var room = Assert.Single(result);
        Assert.Equal("Aula A1", room.Name);
    }

    private static List<Room> Search(RoomFilter filter, IReadOnlyCollection<Guid>? matchingRoomIds = null) =>
        RoomSearch.ApplyEquipmentFilter(RoomSearch.Apply(Rooms.AsQueryable(), filter), matchingRoomIds).ToList();

    private static Room CreateRoom(
        string name,
        RoomType type,
        int capacity,
        Guid buildingId,
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Capacity = capacity,
            BuildingId = buildingId,
            Floor = 0,
            IsBookable = true,
            IsActive = isActive
        };
}
