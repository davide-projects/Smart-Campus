using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartCampus.Rooms.Domain;
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

    [Fact]
    public async Task RicercaSenzaFiltri_RestituisceGliSpaziAttiviDelSeed()
    {
        var rooms = await SearchAsync("/api/rooms/");

        Assert.Equal(12, rooms.Count);
        Assert.DoesNotContain(rooms, room => room.Name == "Aula dismessa");
        Assert.Contains(rooms, room => room.Name == "Aula A1");
    }

    [Fact]
    public async Task RicercaPerTipo_RestituisceSoloLeSaleStudio()
    {
        var rooms = await SearchAsync("/api/rooms/?type=StudyRoom");

        Assert.Equal(4, rooms.Count);
        Assert.All(rooms, room => Assert.Equal(RoomType.StudyRoom, room.Type));
    }

    [Fact]
    public async Task RicercaPerCapienzaMinima_IncludeGliSpaziCapienti()
    {
        var rooms = await SearchAsync("/api/rooms/?minCapacity=30");

        Assert.Equal(5, rooms.Count);
        Assert.All(rooms, room => Assert.True(room.Capacity >= 30));
    }

    [Fact]
    public async Task RicercaCombinata_ApplicaTipoCapienzaEdEdificio()
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
    public async Task RicercaPerEdificio_RestituisceSoloGliSpaziDellaBiblioteca()
    {
        var rooms = await SearchAsync($"/api/rooms/?buildingId={LibraryId}");

        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, room => Assert.Equal("Biblioteca", room.BuildingName));
    }

    [Fact]
    public async Task SpazioNonPrenotabile_RestaVisibileMaSegnalato()
    {
        var rooms = await SearchAsync("/api/rooms/");

        var readingRoom = Assert.Single(rooms, room => room.Name == "Sala lettura");
        Assert.False(readingRoom.IsBookable);
    }

    [Fact]
    public async Task LaRicerca_NonMostraMaiGliSpaziDismessi()
    {
        // Anche con i filtri che descrivono esattamente l'aula dismessa (Classroom, 50 posti).
        var rooms = await SearchAsync($"/api/rooms/?type=Classroom&minCapacity=50&buildingId={BuildingAId}");

        Assert.Empty(rooms);
    }

    [Fact]
    public async Task Risultati_OrdinatiPerEdificioEPoiPerNome()
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
    public async Task CapienzaMinimaNonValida_Restituisce400(string url)
    {
        var response = await fixture.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EdificioNonValido_Restituisce400()
    {
        var response = await fixture.Client.GetAsync($"/api/rooms/?buildingId={Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ConDatabaseAttivo_RispondeSano()
    {
        var response = await fixture.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthLive_RispondeSano()
    {
        var response = await fixture.Client.GetAsync("/health/live");

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
