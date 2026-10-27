using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartCampus.Bookings.Api.Data.Context;

// Usata solo da "dotnet ef" per creare le migrazioni: non serve un database attivo
// e non viene mai usata a runtime (l'applicazione legge ConnectionStrings:Database).
public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=localhost,1433;Database=SmartCampus_Bookings;Integrated Security=True;TrustServerCertificate=True";

    public BookingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BookingDbContext(options);
    }
}
