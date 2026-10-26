using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Rooms.Domain;

namespace SmartCampus.Rooms.Data.Configurations;

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");

        builder.HasKey(building => building.Id);

        builder.Property(building => building.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(building => building.Code)
            .HasMaxLength(10)
            .IsRequired();

        // Il codice è quello usato dal personale: due edifici non possono averlo uguale.
        builder.HasIndex(building => building.Code)
            .IsUnique();
    }
}
