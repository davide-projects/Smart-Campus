using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartCampus.Rooms.Models;
using SmartCampus.Rooms.Features.Rooms;

namespace SmartCampus.Rooms.IntegrationTests;

public sealed class RoomSearchTests(RoomsApiFixture fixture) : IClassFixture<RoomsApiFixture>
{
    
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid BuildingAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LibraryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ProjectorId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid WhiteboardId = Guid.Parse("e1000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task Search_WithoutFilters_ReturnsSeededActiveRooms()
    {
        var rooms = await SearchAsync("/api/rooms/");

        Assert.Equal(12, rooms.Count);
        Assert.DoesNotContain(rooms, room => room.Name == "Aula dismessa");
        Assert.Contains(rooms, room => room.Name == "Aula A1");
    }

    [Fact]
    public async Task Search_ByType_ReturnsOnlyStudyRooms()
    {
        var rooms = await SearchAsync("/api/rooms/?type=StudyRoom");

        Assert.Equal(4, rooms.Count);
        Assert.All(rooms, room => Assert.Equal(RoomType.StudyRoom, room.Type));
    }

    [Fact]
    public async Task Search_ByMinimumCapacity_ReturnsRoomsWithSufficientCapacity()
    {
        var rooms = await SearchAsync("/api/rooms/?minCapacity=30");

        Assert.Equal(5, rooms.Count);
        Assert.All(rooms, room => Assert.True(room.Capacity >= 30));
    }

    [Fact]
    public async Task Search_WithCombinedFilters_AppliesTypeCapacityAndBuilding()
    {
        var rooms = await SearchAsync(
            $"/api/rooms/?type=Classroom&minCapacity=30&buildingId={BuildingAId}");

        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, room =>
        {
            Assert.Equal(RoomType.Classroom, room.Type);
            Assert.True(room.Capacity >= 30);
            Assert.Equal(BuildingAId, room.BuildingId);
        });
    }

    [Fact]
    public async Task Search_ByBuilding_ReturnsOnlyLibraryRooms()
    {
        var rooms = await SearchAsync($"/api/rooms/?buildingId={LibraryId}");

        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, room => Assert.Equal("Biblioteca", room.BuildingName));
    }

    [Fact]
    public async Task Search_ByEquipment_ReturnsOnlyRoomsWithThatEquipment()
    {
        var rooms = await SearchAsync($"/api/rooms/?equipmentIds={ProjectorId}");

        Assert.Equal(4, rooms.Count);
        Assert.Equal(
            ["Aula A1", "Aula A3", "Laboratorio Informatica 1", "Aula B1"],
            rooms.Select(room => room.Name));

        var classroom = Assert.Single(rooms, room => room.Name == "Aula A1");
        Assert.Contains(classroom.Equipment, item => item.Id == ProjectorId && item.Name == "Proiettore");
        Assert.Contains(classroom.Equipment, item => item.Id == WhiteboardId && item.Name == "Lavagna");
    }

    [Fact]
    public async Task Search_ByMultipleEquipmentRequiresEveryRequestedItem()
    {
        var rooms = await SearchAsync($"/api/rooms/?equipmentIds={ProjectorId}&equipmentIds={WhiteboardId}");

        Assert.Equal(["Aula A1", "Aula A3", "Aula B1"], rooms.Select(room => room.Name));
    }

    [Fact]
    public async Task Search_WithEquipmentAndOtherFiltersAppliesEveryFilter()
    {
        var rooms = await SearchAsync(
            $"/api/rooms/?type=Classroom&minCapacity=30&buildingId={BuildingAId}&equipmentIds={ProjectorId}");

        Assert.Equal(["Aula A1", "Aula A3"], rooms.Select(room => room.Name));
    }

    [Fact]
    public async Task Search_ByUnknownEquipmentReturnsNoRooms()
    {
        var rooms = await SearchAsync($"/api/rooms/?equipmentIds={Guid.NewGuid()}");

        Assert.Empty(rooms);
    }

    [Fact]
    public async Task Search_WhenRoomIsNotBookable_IncludesItAndMarksIt()
    {
        var rooms = await SearchAsync("/api/rooms/");

        var readingRoom = Assert.Single(rooms, room => room.Name == "Sala lettura");
        Assert.False(readingRoom.IsBookable);
    }

    [Fact]
    public async Task Search_NeverReturnsDecommissionedRooms()
    {
        // Anche con i filtri che descrivono esattamente l'aula dismessa (Classroom, 50 posti).
        var rooms = await SearchAsync($"/api/rooms/?type=Classroom&minCapacity=50&buildingId={BuildingAId}");

        Assert.Empty(rooms);
    }

    [Fact]
    public async Task SearchResults_AreOrderedByBuildingThenName()
    {
        var rooms = await SearchAsync("/api/rooms/");

        var expected = rooms
            .OrderBy(room => room.BuildingName, StringComparer.Ordinal)
            .ThenBy(room => room.Name, StringComparer.Ordinal)
            .Select(room => room.Id);

        Assert.Equal(expected, rooms.Select(room => room.Id));
    }

    [Theory]
    [InlineData("/api/rooms/?minCapacity=0")]
    [InlineData("/api/rooms/?minCapacity=-5")]
    public async Task Search_WithInvalidMinimumCapacity_ReturnsBadRequest(string url)
    {
        var response = await fixture.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithInvalidBuildingId_ReturnsBadRequest()
    {
        var response = await fixture.Client.GetAsync($"/api/rooms/?buildingId={Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithInvalidEquipmentId_ReturnsBadRequest()
    {
        var response = await fixture.Client.GetAsync($"/api/rooms/?equipmentIds={Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/rooms/?maxCapacity=0")]
    [InlineData("/api/rooms/?maxCapacity=-5")]
    public async Task Search_WithInvalidMaximumCapacity_ReturnsBadRequest(string url)
    {
        var response = await fixture.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithMaximumCapacityBelowMinimum_ReturnsBadRequest()
    {
        var response = await fixture.Client.GetAsync("/api/rooms/?minCapacity=30&maxCapacity=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_WhenDatabaseIsAvailable_ReturnsOk()
    {
        var response = await fixture.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<RoomSummaryResponse>> SearchAsync(string url)
    {
        var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomSummaryResponse>>(JsonOptions);

        Assert.NotNull(rooms);
        return rooms;
    }
}
