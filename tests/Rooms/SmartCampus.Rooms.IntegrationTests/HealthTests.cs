using System.Net;

namespace SmartCampus.Rooms.IntegrationTests;

public class HealthTests(RoomsApiFixture fixture) : IClassFixture<RoomsApiFixture>
{
    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await fixture.Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
