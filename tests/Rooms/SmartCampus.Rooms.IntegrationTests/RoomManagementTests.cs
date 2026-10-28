using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Contracts;
using SmartCampus.Rooms.Features.Rooms;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.IntegrationTests;

public sealed class RoomManagementTests(RoomsApiFixture fixture) : IClassFixture<RoomsApiFixture>
{
    private static readonly Guid BuildingAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ClassroomA1Id = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ProjectorId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid WhiteboardId = Guid.Parse("e1000000-0000-0000-0000-000000000003");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Staff_CanCreateAndUpdateRoom_AndEventsArePublished()
    {
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
            var unauthenticatedResponse = await fixture.Client.PostAsJsonAsync("/api/rooms/", ValidRequest(30));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

            fixture.AuthorizeAsRole("Student");
            var studentResponse = await fixture.Client.PostAsJsonAsync("/api/rooms/", ValidRequest(30));
            Assert.Equal(HttpStatusCode.Forbidden, studentResponse.StatusCode);
            var studentUpdateResponse = await fixture.Client.PutAsJsonAsync(
                $"/api/rooms/{ClassroomA1Id}", ValidRequest(30));
            Assert.Equal(HttpStatusCode.Forbidden, studentUpdateResponse.StatusCode);

            fixture.AuthorizeAsRole("Staff");
            var invalidCapacityResponse = await fixture.Client.PostAsJsonAsync("/api/rooms/", ValidRequest(0));
            Assert.Equal(HttpStatusCode.BadRequest, invalidCapacityResponse.StatusCode);
            using (var validation = JsonDocument.Parse(await invalidCapacityResponse.Content.ReadAsStringAsync()))
            {
                Assert.Contains("Capacity", validation.RootElement.GetProperty("errors").EnumerateObject()
                    .Select(error => error.Name));
            }

            var invalidHoursResponse = await fixture.Client.PostAsJsonAsync(
                "/api/rooms/",
                ValidRequest(30) with
                {
                    OpeningHours = [new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(9, 0))]
                });
            Assert.Equal(HttpStatusCode.BadRequest, invalidHoursResponse.StatusCode);

            var createResponse = await fixture.Client.PostAsJsonAsync("/api/rooms/", ValidRequest(30));
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            using var createdBody = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
            var roomId = createdBody.RootElement.GetProperty("id").GetGuid();

            var createdEvent = await ReadEventForRoomAsync(createdEvents.Reader, roomId, message => message.RoomId);
            Assert.Equal("Integration room", createdEvent.Name);
            Assert.Equal(30, createdEvent.Capacity);
            Assert.Equal(new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                Assert.Single(createdEvent.OpeningHours));

            var duplicateCreateResponse = await fixture.Client.PostAsJsonAsync(
                "/api/rooms/", ValidRequest(12) with { Name = "Integration room" });
            Assert.Equal(HttpStatusCode.Conflict, duplicateCreateResponse.StatusCode);

            var secondCreateResponse = await fixture.Client.PostAsJsonAsync(
                "/api/rooms/", ValidRequest(12) with { Name = "Another integration room" });
            Assert.Equal(HttpStatusCode.Created, secondCreateResponse.StatusCode);
            using var secondCreatedBody = JsonDocument.Parse(await secondCreateResponse.Content.ReadAsStringAsync());
            var secondRoomId = secondCreatedBody.RootElement.GetProperty("id").GetGuid();

            var duplicateUpdateResponse = await fixture.Client.PutAsJsonAsync(
                $"/api/rooms/{secondRoomId}", ValidRequest(12) with { Name = "Integration room" });
            Assert.Equal(HttpStatusCode.Conflict, duplicateUpdateResponse.StatusCode);

            var updateResponse = await fixture.Client.PutAsJsonAsync(
                $"/api/rooms/{roomId}",
                ValidRequest(42) with
                {
                    Name = "Updated integration room",
                    EquipmentIds = [WhiteboardId],
                    OpeningHours = [new RoomOpeningHours(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0))]
                });
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var updatedEvent = await ReadEventForRoomAsync(updatedEvents.Reader, roomId, message => message.RoomId);
            Assert.True(updatedEvent.IsActive);
            Assert.Equal("Updated integration room", updatedEvent.Name);
            Assert.Equal(42, updatedEvent.Capacity);
            Assert.Equal(DayOfWeek.Tuesday, Assert.Single(updatedEvent.OpeningHours).DayOfWeek);

            var detailsResponse = await fixture.Client.GetAsync($"/api/rooms/{roomId}");
            Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
            var details = await detailsResponse.Content.ReadFromJsonAsync<RoomDetailResponse>(JsonOptions);
            Assert.NotNull(details);
            Assert.Equal("Updated integration room", details.Name);
            Assert.Equal(42, details.Capacity);
            Assert.Equal([WhiteboardId], details.Equipment.Select(item => item.Id));
            Assert.Equal(new TimeOnly(10, 0), Assert.Single(details.OpeningHours,
                hours => hours.DayOfWeek == DayOfWeek.Tuesday).OpensAt);
        }
        finally
        {
            fixture.Client.DefaultRequestHeaders.Authorization = null;
            await receiverBus.StopAsync();
        }
    }

    private static RoomWriteRequest ValidRequest(int capacity) => new(
        BuildingAId,
        "Integration room",
        RoomType.Classroom,
        capacity,
        1,
        true,
        [ProjectorId],
        [new RoomOpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]);

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
}
