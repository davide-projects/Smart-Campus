using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Contracts;
using SmartCampus.Rooms.Data;
using SmartCampus.Rooms.Features.Rooms;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.IntegrationTests;

public sealed class RoomEventOutboxTests(RoomsApiFixture fixture) : IClassFixture<RoomsApiFixture>
{
    private static readonly Guid BuildingAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LibraryId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Spazi seed attivi con il nome del relativo edificio (l'«Aula dismessa» pubblica RoomDeactivated).
    private static readonly IReadOnlyDictionary<Guid, string> ActiveSeedRoomBuildings = new Dictionary<Guid, string>
    {
        [Guid.Parse("a1000000-0000-0000-0000-000000000001")] = "Edificio A",
        [Guid.Parse("a1000000-0000-0000-0000-000000000002")] = "Edificio A",
        [Guid.Parse("a1000000-0000-0000-0000-000000000003")] = "Edificio A",
        [Guid.Parse("a1000000-0000-0000-0000-000000000005")] = "Edificio A",
        [Guid.Parse("a1000000-0000-0000-0000-000000000006")] = "Edificio A",
        [Guid.Parse("a1000000-0000-0000-0000-000000000007")] = "Edificio A",
        [Guid.Parse("b1000000-0000-0000-0000-000000000001")] = "Edificio B",
        [Guid.Parse("b1000000-0000-0000-0000-000000000002")] = "Edificio B",
        [Guid.Parse("b1000000-0000-0000-0000-000000000003")] = "Edificio B",
        [Guid.Parse("b1000000-0000-0000-0000-000000000004")] = "Edificio B",
        [Guid.Parse("c1000000-0000-0000-0000-000000000001")] = "Biblioteca",
        [Guid.Parse("c1000000-0000-0000-0000-000000000002")] = "Biblioteca"
    };

    [Fact]
    public async Task RoomChanges_ArePublishedAfterRabbitMqRecovers()
    {
        Assert.Equal(1, await fixture.GetSeedDispatchCountAsync());

        var createdEvents = Channel.CreateUnbounded<RoomCreated>();
        var updatedEvents = Channel.CreateUnbounded<RoomUpdated>();
        var deactivatedEvents = Channel.CreateUnbounded<RoomDeactivated>();
        var receiverServices = new ServiceCollection();
        receiverServices.AddSingleton(createdEvents);
        receiverServices.AddSingleton(updatedEvents);
        receiverServices.AddSingleton(deactivatedEvents);
        receiverServices.AddMassTransit(configure =>
        {
            configure.AddConsumer<CapturingRoomEventsConsumer>();
            configure.UsingRabbitMq((context, rabbitMq) =>
            {
                rabbitMq.Host(
                    "localhost",
                    fixture.RabbitMq.GetMappedPublicPort(5672),
                    "/",
                    host =>
                    {
                        host.Username("rooms-integration");
                        host.Password("rooms-integration");
                    });

                rabbitMq.ReceiveEndpoint($"rooms-events-{Guid.NewGuid():N}", endpoint =>
                    endpoint.ConfigureConsumer<CapturingRoomEventsConsumer>(context));
            });
        });

        await using var receiverProvider = receiverServices.BuildServiceProvider();
        var receiverBus = receiverProvider.GetRequiredService<IBusControl>();
        await receiverBus.StartAsync();

        try
        {
            fixture.AuthorizeAsRole("Staff");
            await fixture.RabbitMq.PauseAsync();
            var createResponse = await fixture.Client.PostAsJsonAsync(
                "/api/rooms/",
                new RoomWriteRequest(
                    BuildingAId,
                    "Integration room",
                    RoomType.Classroom,
                    20,
                    1,
                    true,
                    [],
                    [new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]));

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var createdBody = await createResponse.Content.ReadFromJsonAsync<CreatedRoomResponse>();
            Assert.NotNull(createdBody);

            await fixture.RabbitMq.UnpauseAsync();

            var createdEvent = await ReadEventForRoomAsync(
                createdEvents.Reader,
                createdBody.Id,
                message => message.RoomId);
            Assert.Equal(createdBody.Id, createdEvent.RoomId);
            Assert.Equal("Integration room", createdEvent.Name);
            Assert.Equal(20, createdEvent.Capacity);
            Assert.True(createdEvent.IsBookable);
            Assert.Equal(new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                Assert.Single(createdEvent.OpeningHours));
            Assert.Equal("Edificio A", createdEvent.BuildingName);

            var updateResponse = await fixture.Client.PutAsJsonAsync(
                $"/api/rooms/{createdEvent.RoomId}",
                new RoomWriteRequest(
                    BuildingAId,
                    "Updated integration room",
                    RoomType.Classroom,
                    24,
                    1,
                    true,
                    [],
                    [new RoomOpeningHours(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0))]));

            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
            var updatedEvent = await ReadEventForRoomAsync(
                updatedEvents.Reader,
                createdEvent.RoomId,
                message => message.RoomId);
            Assert.Equal(createdEvent.RoomId, updatedEvent.RoomId);
            Assert.Equal("Updated integration room", updatedEvent.Name);
            Assert.Equal(24, updatedEvent.Capacity);
            Assert.True(updatedEvent.IsActive);
            Assert.Equal(DayOfWeek.Tuesday, Assert.Single(updatedEvent.OpeningHours).DayOfWeek);
            Assert.Equal("Edificio A", updatedEvent.BuildingName);

            var deactivateResponse = await fixture.Client.PostAsync(
                $"/api/rooms/{createdEvent.RoomId}/deactivate",
                content: null);

            Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);
            var deactivatedEvent = await ReadEventForRoomAsync(
                deactivatedEvents.Reader,
                createdEvent.RoomId,
                message => message.RoomId);
            Assert.Equal(createdEvent.RoomId, deactivatedEvent.RoomId);
        }
        finally
        {
            await receiverBus.StopAsync();
        }
    }

    [Fact]
    public async Task UpdateRoom_MovedToAnotherBuilding_PublishesNewBuildingName()
    {
        var createdEvents = Channel.CreateUnbounded<RoomCreated>();
        var updatedEvents = Channel.CreateUnbounded<RoomUpdated>();
        await using var receiverProvider = BuildReceiver(createdEvents, updatedEvents);
        var receiverBus = receiverProvider.GetRequiredService<IBusControl>();
        await receiverBus.StartAsync();
        try
        {
            fixture.AuthorizeAsRole("Staff");
            var createResponse = await fixture.Client.PostAsJsonAsync(
                "/api/rooms/",
                new RoomWriteRequest(BuildingAId, "Room moving building", RoomType.StudyRoom, 10, 0, true, [], []));
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var createdBody = await createResponse.Content.ReadFromJsonAsync<CreatedRoomResponse>();
            Assert.NotNull(createdBody);

            var createdEvent = await ReadEventForRoomAsync(createdEvents.Reader, createdBody.Id, message => message.RoomId);
            Assert.Equal("Edificio A", createdEvent.BuildingName);

            var updateResponse = await fixture.Client.PutAsJsonAsync(
                $"/api/rooms/{createdBody.Id}",
                new RoomWriteRequest(LibraryId, "Room moving building", RoomType.StudyRoom, 10, 0, true, [], []));
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var updatedEvent = await ReadEventForRoomAsync(updatedEvents.Reader, createdBody.Id, message => message.RoomId);
            Assert.Equal("Biblioteca", updatedEvent.BuildingName);
            Assert.Equal("Room moving building", updatedEvent.Name);
            Assert.Equal(10, updatedEvent.Capacity);
            Assert.True(updatedEvent.IsActive);
        }
        finally
        {
            await receiverBus.StopAsync();
        }
    }

    [Fact]
    public async Task SeedRooms_PublishedOnStartup_ContainBuildingName()
    {
        var received = new Dictionary<Guid, RoomCreated>();
        while (!ActiveSeedRoomBuildings.Keys.All(received.ContainsKey))
        {
            var message = await fixture.SeedRoomCreatedEvents.Reader.ReadAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(30));
            received[message.RoomId] = message;
        }

        foreach (var (roomId, buildingName) in ActiveSeedRoomBuildings)
        {
            Assert.Equal(buildingName, received[roomId].BuildingName);
        }
    }

    [Fact]
    public async Task CreateRoom_WithNameAlreadyUsedInBuilding_ReturnsConflict()
    {
        fixture.AuthorizeAsRole("Staff");
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/rooms/",
            new RoomWriteRequest(BuildingAId, "Aula A1", RoomType.Classroom, 20, 1, true, [], []));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Nome spazio già utilizzato", problem.Title);
    }

    [Fact]
    public async Task UpdateRoom_WithNameAlreadyUsedInBuilding_ReturnsConflictWithoutChangingRoom()
    {
        fixture.AuthorizeAsRole("Staff");
        var createResponse = await fixture.Client.PostAsJsonAsync(
            "/api/rooms/",
            new RoomWriteRequest(BuildingAId, "Room for duplicate test", RoomType.Classroom, 20, 1, true, [], []));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdBody = await createResponse.Content.ReadFromJsonAsync<CreatedRoomResponse>();
        Assert.NotNull(createdBody);

        var updateResponse = await fixture.Client.PutAsJsonAsync(
            $"/api/rooms/{createdBody.Id}",
            new RoomWriteRequest(BuildingAId, "Aula A1", RoomType.Classroom, 20, 1, true, [], []));

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        var detailResponse = await fixture.Client.GetAsync($"/api/rooms/{createdBody.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.Equal("Room for duplicate test", detail.RootElement.GetProperty("name").GetString());
    }

    private ServiceProvider BuildReceiver(Channel<RoomCreated> createdEvents, Channel<RoomUpdated> updatedEvents)
    {
        var receiverServices = new ServiceCollection();
        receiverServices.AddSingleton(createdEvents);
        receiverServices.AddSingleton(updatedEvents);
        receiverServices.AddSingleton(Channel.CreateUnbounded<RoomDeactivated>());
        receiverServices.AddMassTransit(configure =>
        {
            configure.AddConsumer<CapturingRoomEventsConsumer>();
            configure.UsingRabbitMq((context, rabbitMq) =>
            {
                rabbitMq.Host(
                    "localhost",
                    fixture.RabbitMq.GetMappedPublicPort(5672),
                    "/",
                    host =>
                    {
                        host.Username("rooms-integration");
                        host.Password("rooms-integration");
                    });

                rabbitMq.ReceiveEndpoint($"rooms-events-{Guid.NewGuid():N}", endpoint =>
                    endpoint.ConfigureConsumer<CapturingRoomEventsConsumer>(context));
            });
        });

        return receiverServices.BuildServiceProvider();
    }

    private static async Task<TMessage> ReadEventForRoomAsync<TMessage>(
        ChannelReader<TMessage> events,
        Guid roomId,
        Func<TMessage, Guid> getRoomId)
    {
        while (true)
        {
            var message = await events.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            if (getRoomId(message) == roomId)
            {
                return message;
            }
        }
    }

    private sealed record CreatedRoomResponse(Guid Id);
}

internal sealed class CapturingRoomEventsConsumer(
    Channel<RoomCreated> createdEvents,
    Channel<RoomUpdated> updatedEvents,
    Channel<RoomDeactivated> deactivatedEvents) :
    IConsumer<RoomCreated>,
    IConsumer<RoomUpdated>,
    IConsumer<RoomDeactivated>
{
    public Task Consume(ConsumeContext<RoomCreated> context)
    {
        createdEvents.Writer.TryWrite(context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<RoomUpdated> context)
    {
        updatedEvents.Writer.TryWrite(context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<RoomDeactivated> context)
    {
        deactivatedEvents.Writer.TryWrite(context.Message);
        return Task.CompletedTask;
    }
}
