using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Rooms.Domain;

namespace SmartCampus.Rooms.Data.Configurations;

internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");

        builder.HasKey(equipment => equipment.Id);

        builder.Property(equipment => equipment.Name)
            .HasMaxLength(100)
            .IsRequired();

        // "Proiettore" è una sola voce di catalogo, riusata da più spazi.
        builder.HasIndex(equipment => equipment.Name)
            .IsUnique();
    }
}
