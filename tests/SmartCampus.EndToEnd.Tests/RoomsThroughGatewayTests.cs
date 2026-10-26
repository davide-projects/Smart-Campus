using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCampus.EndToEnd.Tests;

// US-01 vista dall'esterno: si chiama solo il gateway, come farà il frontend.
public sealed class RoomsThroughGatewayTests(GatewayRoomsFixture fixture) : IClassFixture<GatewayRoomsFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Gateway_InoltraLaRicercaSpazi_RestituisceIlSeed()
    {
        var rooms = await GetRoomsAsync("/api/rooms/");

        Assert.Equal(12, rooms.Count);
        Assert.Contains(rooms, room => room.Name == "Aula A1" && room.BuildingName == "Edificio A");
    }

    [Fact]
    public async Task Gateway_InoltraIFiltri_AlServizioRooms()
    {
        var rooms = await GetRoomsAsync("/api/rooms/?type=Laboratory");

        Assert.Equal(3, rooms.Count);
        Assert.All(rooms, room => Assert.Equal("Laboratory", room.Type));
    }

    [Fact]
    public async Task Gateway_RiportaLaValidazioneDelServizio()
    {
        var response = await fixture.Client.GetAsync("/api/rooms/?minCapacity=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_RottaNonConfigurata_Restituisce404()
    {
        var response = await fixture.Client.GetAsync("/api/sconosciuto/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<RoomDto>> GetRoomsAsync(string url)
    {
        var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomDto>>(JsonOptions);

        Assert.NotNull(rooms);
        return rooms;
    }

    // Copia del contratto pubblico: il test end-to-end non deve dipendere dai tipi interni del servizio.
    private sealed record RoomDto(
        Guid Id,
        string Name,
        string Type,
        int Capacity,
        int Floor,
        bool IsBookable,
        Guid BuildingId,
        string BuildingName);
}
