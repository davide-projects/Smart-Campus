using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartCampus.Rooms.Data;

// Usata solo da "dotnet ef" per creare le migrazioni: non serve un database attivo
// e non viene mai usata a runtime (l'applicazione legge ConnectionStrings:Database).
public sealed class RoomsDbContextFactory : IDesignTimeDbContextFactory<RoomsDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=localhost,1433;Database=SmartCampus_Rooms;Integrated Security=True;TrustServerCertificate=True";

    public RoomsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<RoomsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new RoomsDbContext(options);
    }
}
