using System.Net;

namespace SmartCampus.Bookings.IntegrationTests;

[Collection(BookingsApiCollection.Name)]
public sealed class HealthTests(BookingsApiFixture fixture)
{
    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
