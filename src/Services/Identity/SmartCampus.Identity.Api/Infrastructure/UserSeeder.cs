using Microsoft.EntityFrameworkCore;

using SmartCampus.Identity.Api.Auth;
using SmartCampus.Identity.Api.Domain;

namespace SmartCampus.Identity.Api.Infrastructure;

public sealed record SeedUser(Guid Id, string Username, string DisplayName, UserRole Role);

// Utenti sintetici di sviluppo. Gli identificativi sono fissi, così i dati di seed degli altri servizi
// (es. membri di una prenotazione di gruppo) possono farvi riferimento.
public static class SeedUsers
{
    public static readonly Guid Student1Id = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Student2Id = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid Student3Id = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid Student4Id = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    public static readonly Guid Student5Id = Guid.Parse("a0000000-0000-0000-0000-000000000005");
    public static readonly Guid Staff1Id = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid Staff2Id = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    public static IReadOnlyList<SeedUser> All { get; } =
    [
        new(Student1Id, "student1", "Mario Rossi", UserRole.Student),
        new(Student2Id, "student2", "Anna Bianchi", UserRole.Student),
        new(Student3Id, "student3", "Luca Verdi", UserRole.Student),
        new(Student4Id, "student4", "Giulia Neri", UserRole.Student),
        new(Student5Id, "student5", "Paolo Gialli", UserRole.Student),
        new(Staff1Id, "staff1", "Elena Conti", UserRole.Staff),
        new(Staff2Id, "staff2", "Marco Russo", UserRole.Staff)
    ];
}

// Carica gli utenti sintetici se la tabella è vuota. La password arriva dalla configurazione, mai dal codice
public sealed class UserSeeder(IdentityContext db, IPasswordService passwords, ILogger<UserSeeder> logger)
{
    public async Task SeedAsync(string password, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogDebug("Utenti già presenti: seed saltato.");
            return;
        }

        foreach (var seed in SeedUsers.All)
        {
            db.Users.Add(new User(seed.Id, seed.Username, seed.DisplayName, passwords.Hash(password), seed.Role));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Caricati {Count} utenti sintetici.", SeedUsers.All.Count);
    }
}
