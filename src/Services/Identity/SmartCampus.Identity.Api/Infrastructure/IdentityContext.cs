using Microsoft.EntityFrameworkCore;

using SmartCampus.Identity.Api.Domain;

namespace SmartCampus.Identity.Api.Infrastructure;

// Database del servizio Identity (SmartCampus_Identity)
public sealed class IdentityContext(DbContextOptions<IdentityContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).ValueGeneratedNever();

            entity.Property(user => user.Username).HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.Username).IsUnique();

            entity.Property(user => user.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();

            // Il ruolo è salvato come testo ("Student" / "Staff").
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        });
    }
}
