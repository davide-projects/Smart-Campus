using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Rooms.Domain;

namespace SmartCampus.Rooms.Data.Configurations;

internal sealed class RoomEquipmentConfiguration : IEntityTypeConfiguration<RoomEquipment>
{
    public void Configure(EntityTypeBuilder<RoomEquipment> builder)
    {
        builder.ToTable("RoomEquipment");

        // Chiave composta: la stessa attrezzatura non può comparire due volte nello stesso spazio.
        builder.HasKey(link => new { link.RoomId, link.EquipmentId });

        builder.HasOne(link => link.Room)
            .WithMany(room => room.RoomEquipment)
            .HasForeignKey(link => link.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Equipment)
            .WithMany(equipment => equipment.Rooms)
            .HasForeignKey(link => link.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
