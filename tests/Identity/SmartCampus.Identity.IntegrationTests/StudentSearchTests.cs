using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SmartCampus.Identity.Api.Domain;
using SmartCampus.Identity.Api.Endpoints;
using SmartCampus.Identity.Api.Infrastructure;

namespace SmartCampus.Identity.IntegrationTests;

/// <summary>Ricerca degli studenti per il modulo di prenotazione di gruppo: GET /api/identity/students.</summary>
public class StudentSearchTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private const string MinLengthMessage = "Scrivi almeno 2 caratteri per cercare.";

    // Studenti sintetici aggiunti solo per il test del limite di 10 risultati. Due hanno lo stesso nome
    // visualizzato, per verificare l'ordine secondario per username; studx02 è inserito prima di studx01.
    private static readonly (string Username, string DisplayName)[] ExtraStudents =
    [
        ("studx02", "Alessia Moro"),
        ("studx01", "Alessia Moro"),
        ("studx03", "Bruno Sala"),
        ("studx04", "Carla Fontana"),
        ("studx05", "Davide Riva"),
        ("studx06", "Ester Galli"),
        ("studx07", "Zeno Valli"),
        ("studx08", "Zoe Marchi")
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_ByDisplayName_ReturnsMatchingStudent()
    {
        var students = await SearchOkAsync("bia");

        var student = Assert.Single(students);
        Assert.Equal(SeedUsers.Student2Id, student.Id);
        Assert.Equal("student2", student.Username);
        Assert.Equal("Anna Bianchi", student.DisplayName);
    }

    [Fact]
    public async Task Search_ByUsername_IgnoresCase()
    {
        var students = await SearchOkAsync("STUDENT3");

        var student = Assert.Single(students);
        Assert.Equal(SeedUsers.Student3Id, student.Id);
        Assert.Equal("Luca Verdi", student.DisplayName);
    }

    [Fact]
    public async Task Search_TrimsLeadingAndTrailingSpaces()
    {
        var students = await SearchOkAsync("  bia  ");

        Assert.Equal(SeedUsers.Student2Id, Assert.Single(students).Id);
    }

    [Fact]
    public async Task Search_StaffAreExcluded()
    {
        var students = await SearchOkAsync("Conti");

        Assert.Empty(students);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyArray()
    {
        var students = await SearchOkAsync("nessuno-corrisponde");

        Assert.Empty(students);
    }

    [Fact]
    public async Task Search_ExcludesTheSearcher()
    {
        // Mario Rossi (student1) cerca il proprio cognome.
        var students = await SearchOkAsync("Rossi");

        Assert.DoesNotContain(students, student => student.Id == SeedUsers.Student1Id);
        Assert.Empty(students);
    }

    [Fact]
    public async Task Search_ResponseContainsOnlyIdUsernameAndDisplayName()
    {
        var response = await SearchAsync(await TestAuth.GetTokenAsync(_client, "student1"), "bia");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(json.RootElement.EnumerateArray());
        var names = item.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["displayName", "id", "username"], names);
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    [InlineData("   ")]
    public async Task Search_TooShort_Returns400OnSearchField(string search)
    {
        var response = await SearchAsync(await TestAuth.GetTokenAsync(_client, "student1"), search);

        await AssertMinLengthProblemAsync(response);
    }

    [Fact]
    public async Task Search_WithoutSearchParameter_Returns400()
    {
        using var request = TestAuth.CreateRequest(
            HttpMethod.Get, "/api/identity/students", await TestAuth.GetTokenAsync(_client, "student1"));

        var response = await _client.SendAsync(request);

        await AssertMinLengthProblemAsync(response);
    }

    [Fact]
    public async Task Search_MoreThanTenMatches_ReturnsFirstTenInOrder()
    {
        await AddExtraStudentsAsync();

        var students = await SearchOkAsync("stud");

        Assert.Equal(
            [
                "studx01", "studx02", "student2", "studx03", "studx04",
                "studx05", "studx06", "student4", "student3", "student5"
            ],
            students.Select(student => student.Username).ToArray());
    }

    [Fact]
    public async Task Search_WithoutToken_Returns401()
    {
        var response = await SearchAsync(token: null, "bia");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_AsStaff_Returns403WithoutResults()
    {
        var response = await SearchAsync(await TestAuth.GetTokenAsync(_client, "staff1"), "bia");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Ricerca non consentita", problem!.Title);
        Assert.Equal("Solo gli studenti possono cercare altri studenti.", problem.Detail);
        Assert.DoesNotContain("Bianchi", await response.Content.ReadAsStringAsync());
    }

    // ---- Utilità ----

    private async Task<List<StudentSummary>> SearchOkAsync(string search)
    {
        var response = await SearchAsync(await TestAuth.GetTokenAsync(_client, "student1"), search);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<StudentSummary>>())!;
    }

    private async Task<HttpResponseMessage> SearchAsync(string? token, string search)
    {
        using var request = TestAuth.CreateRequest(
            HttpMethod.Get, $"/api/identity/students?search={Uri.EscapeDataString(search)}", token);
        return await _client.SendAsync(request);
    }

    private static async Task AssertMinLengthProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([MinLengthMessage], problem!.Errors["search"]);
    }

    private async Task AddExtraStudentsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityContext>();

        if (await db.Users.AnyAsync(user => user.Username.StartsWith("studx")))
        {
            return;
        }

        foreach (var (username, displayName) in ExtraStudents)
        {
            // L'hash non serve: questi utenti non fanno login.
            db.Users.Add(new User(Guid.NewGuid(), username, displayName, "hash-non-usato", UserRole.Student));
        }

        await db.SaveChangesAsync();
    }
}
