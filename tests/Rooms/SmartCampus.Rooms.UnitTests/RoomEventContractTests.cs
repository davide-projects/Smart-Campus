using System.Text.Json;
using MassTransit.Serialization;
using SmartCampus.Contracts;

namespace SmartCampus.Rooms.UnitTests;

// Compatibilità dei contratti degli spazi: un evento senza BuildingName o Type resta leggibile.
public sealed class RoomEventContractTests
{
    // Stesse opzioni con cui MassTransit legge i messaggi in Bookings.
    private static readonly JsonSerializerOptions JsonOptions = SystemTextJsonMessageSerializer.Options;

    [Fact]
    public void RoomCreated_WithoutBuildingName_IsReadWithNullBuildingName()
    {
        const string json = """
            {"roomId":"a1000000-0000-0000-0000-000000000001","name":"Aula A1","capacity":30,"isBookable":true,"openingHours":[]}
            """;

        var message = JsonSerializer.Deserialize<RoomCreated>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.Equal("Aula A1", message.Name);
        Assert.Null(message.BuildingName);
    }

    [Fact]
    public void RoomUpdated_WithoutBuildingName_IsReadWithNullBuildingName()
    {
        const string json = """
            {"roomId":"a1000000-0000-0000-0000-000000000001","name":"Aula A1","capacity":30,"isBookable":true,"isActive":true,"openingHours":[]}
            """;

        var message = JsonSerializer.Deserialize<RoomUpdated>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.True(message.IsActive);
        Assert.Null(message.BuildingName);
    }

    [Fact]
    public void RoomCreated_WithoutType_IsReadWithNullType()
    {
        const string json = """
            {"roomId":"a1000000-0000-0000-0000-000000000001","name":"Aula A1","capacity":30,"isBookable":true,"openingHours":[]}
            """;

        var message = JsonSerializer.Deserialize<RoomCreated>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.Null(message.Type);
    }

    [Fact]
    public void RoomUpdated_WithType_IsReadWithTheType()
    {
        const string json = """
            {"roomId":"a1000000-0000-0000-0000-000000000001","name":"Aula A1","capacity":30,"isBookable":true,"isActive":true,"type":"Laboratory","openingHours":[]}
            """;

        var message = JsonSerializer.Deserialize<RoomUpdated>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.Equal("Laboratory", message.Type);
    }
}
