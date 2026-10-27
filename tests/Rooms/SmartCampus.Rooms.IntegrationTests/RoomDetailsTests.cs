using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartCampus.Rooms.Models;
using SmartCampus.Rooms.Features.Rooms;

namespace SmartCampus.Rooms.IntegrationTests;

public sealed class RoomDetailsTests(RoomsApiFixture fixture) : IClassFixture<RoomsApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid ClassroomA1Id = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ReadingRoomId = Guid.Parse("c1000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Details_ForActiveRoom_ReturnsAllInformation()
    {
        var room = await GetRoomAsync(ClassroomA1Id);

        Assert.Equal("Aula A1", room.Name);
        Assert.Equal(RoomType.Classroom, room.Type);
        Assert.Equal("Edificio A", room.BuildingName);
        Assert.Equal("A", room.BuildingCode);
        Assert.Equal(0, room.Floor);
        Assert.Equal(30, room.Capacity);
        Assert.True(room.IsBookable);
        Assert.Equal(["Lavagna", "Proiettore"], room.Equipment.Select(item => item.Name));

        Assert.Equal(7, room.OpeningHours.Count);
        Assert.All(room.OpeningHours.Where(hours =>
            hours.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday), hours =>
        {
            Assert.False(hours.IsClosed);
            Assert.Equal(new TimeOnly(8, 0), hours.OpensAt);
            Assert.Equal(new TimeOnly(20, 0), hours.ClosesAt);
        });

        Assert.All(room.OpeningHours.Where(hours =>
            hours.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday), hours =>
        {
            Assert.True(hours.IsClosed);
            Assert.Null(hours.OpensAt);
            Assert.Null(hours.ClosesAt);
        });
    }

    [Fact]
    public async Task Details_ForNonBookableRoom_IndicatesItCannotBeBooked()
    {
        var room = await GetRoomAsync(ReadingRoomId);

        Assert.Equal("Sala lettura", room.Name);
        Assert.False(room.IsBookable);
    }

    [Fact]
    public async Task Details_ForUnknownRoom_ReturnsNotFoundProblemDetails()
    {
        var response = await fixture.Client.GetAsync($"/api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Spazio non trovato", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal((int)HttpStatusCode.NotFound, problem.RootElement.GetProperty("status").GetInt32());
    }

    private async Task<RoomDetailResponse> GetRoomAsync(Guid roomId)
    {
        var response = await fixture.Client.GetAsync($"/api/rooms/{roomId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var room = await response.Content.ReadFromJsonAsync<RoomDetailResponse>(JsonOptions);
        Assert.NotNull(room);
        return room;
    }
}
