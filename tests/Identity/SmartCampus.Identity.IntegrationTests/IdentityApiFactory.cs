using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace SmartCampus.Identity.IntegrationTests;

//Avvia l'API Identity contro un SQL Server reale in un container (nessun provider in-memory)
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "chiave-di-test-di-almeno-32-byte-0123456789";
    public const string SeedPassword = "Test-Password-2026!";

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "SmartCampus_Identity_Tests"
        }.ConnectionString;

        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Seed:DefaultPassword", SeedPassword);
    }
}
