using System.Net;
using System.Net.Http.Json;
using System.Text;

using SmartCampus.Identity.Api.Endpoints;
using SmartCampus.Identity.Api.Infrastructure;

namespace SmartCampus.Identity.IntegrationTests;

/// <summary>Verifica interna degli studenti usata da Bookings: POST /internal/users/students/check.</summary>
public class StudentsCheckTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private const string CheckUrl = "/internal/users/students/check";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Check_AllStudents_ReturnsThemInStudents()
    {
        var response = await CheckAsync(await StudentTokenAsync(), SeedUsers.Student2Id, SeedUsers.Student3Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StudentsCheckResponse>();
        Assert.Equal(2, body!.Students.Count);
        Assert.Contains(body.Students, student => student.Id == SeedUsers.Student2Id && student.DisplayName == "Anna Bianchi");
        Assert.Contains(body.Students, student => student.Id == SeedUsers.Student3Id && student.DisplayName == "Luca Verdi");
        Assert.Empty(body.NotStudents);
    }

    [Fact]
    public async Task Check_StaffAndUnknownId_AreInNotStudents()
    {
        var unknownId = Guid.Parse("c0000000-0000-0000-0000-000000000099");

        var response = await CheckAsync(
            await StudentTokenAsync(), SeedUsers.Student2Id, SeedUsers.Staff1Id, unknownId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StudentsCheckResponse>();
        var student = Assert.Single(body!.Students);
        Assert.Equal(SeedUsers.Student2Id, student.Id);
        Assert.Equal(2, body.NotStudents.Count);
        Assert.Contains(SeedUsers.Staff1Id, body.NotStudents);
        Assert.Contains(unknownId, body.NotStudents);
    }

    [Fact]
    public async Task Check_RepeatedId_AppearsOnce()
    {
        var unknownId = Guid.Parse("c0000000-0000-0000-0000-000000000098");

        var response = await CheckAsync(
            await StudentTokenAsync(), SeedUsers.Student2Id, SeedUsers.Student2Id, unknownId, unknownId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StudentsCheckResponse>();
        Assert.Equal(SeedUsers.Student2Id, Assert.Single(body!.Students).Id);
        Assert.Equal(unknownId, Assert.Single(body.NotStudents));
    }

    [Fact]
    public async Task Check_WithoutToken_Returns401WithoutLists()
    {
        var response = await CheckAsync(token: null, SeedUsers.Student2Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("students", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_WithTokenSignedWithAnotherKey_Returns401()
    {
        var forged = TestAuth.CreateToken(
            TestAuth.OtherSigningKey, SeedUsers.Student1Id, "Student", DateTime.UtcNow.AddHours(1));

        var response = await CheckAsync(forged, SeedUsers.Student2Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithExpiredToken_Returns401()
    {
        var expired = TestAuth.CreateToken(
            IdentityApiFactory.SigningKey, SeedUsers.Student1Id, "Student", DateTime.UtcNow.AddMinutes(-10));

        var response = await CheckAsync(expired, SeedUsers.Student2Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_WithStudentToken_Returns200()
    {
        var response = await CheckAsync(await StudentTokenAsync(), SeedUsers.Student3Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Check_EmptyUserIds_Returns400()
    {
        var response = await CheckAsync(await StudentTokenAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Indica almeno un utente da verificare.", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"userIds\": null }")]
    [InlineData("")]
    public async Task Check_MissingUserIdsOrBody_Returns400(string json)
    {
        var response = await SendRawAsync(await StudentTokenAsync(), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Indica almeno un utente da verificare.", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{ \"userIds\": [\"abc\"] }")]
    [InlineData("{ \"userIds\": ")]
    public async Task Check_NotGuidOrInvalidJson_Returns400(string json)
    {
        var response = await SendRawAsync(await StudentTokenAsync(), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Indica almeno un utente da verificare.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Check_UnderApiIdentity_Returns404()
    {
        using var request = TestAuth.CreateRequest(
            HttpMethod.Post,
            "/api/identity/internal/users/students/check",
            await StudentTokenAsync(),
            new StudentsCheckRequest([SeedUsers.Student2Id]));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Utilità ----

    private Task<string> StudentTokenAsync() => TestAuth.GetTokenAsync(_client, "student1");

    private async Task<HttpResponseMessage> CheckAsync(string? token, params Guid[] userIds)
    {
        using var request = TestAuth.CreateRequest(HttpMethod.Post, CheckUrl, token, new StudentsCheckRequest(userIds));
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendRawAsync(string token, string json)
    {
        using var request = TestAuth.CreateRequest(HttpMethod.Post, CheckUrl, token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }
}
