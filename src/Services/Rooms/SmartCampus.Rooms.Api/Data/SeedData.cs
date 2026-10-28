using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

// Dati sintetici di sviluppo, versionati insieme alla migrazione.
// Nessun dato personale reale: servono solo a rendere il prodotto dimostrabile.
internal static class SeedData
{
    private static readonly Guid BuildingAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuildingBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LibraryId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid ClassroomA1Id = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClassroomA2Id = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid ClassroomA3Id = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    private static readonly Guid DecommissionedRoomId = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    private static readonly Guid ComputerLab1Id = Guid.Parse("a1000000-0000-0000-0000-000000000005");
    private static readonly Guid ComputerLab2Id = Guid.Parse("a1000000-0000-0000-0000-000000000006");
    private static readonly Guid StudyRoomAId = Guid.Parse("a1000000-0000-0000-0000-000000000007");

    private static readonly Guid ClassroomB1Id = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClassroomB2Id = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid ElectronicsLabId = Guid.Parse("b1000000-0000-0000-0000-000000000003");
    private static readonly Guid StudyRoomBId = Guid.Parse("b1000000-0000-0000-0000-000000000004");

    private static readonly Guid LibraryStudyRoomId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid ReadingRoomId = Guid.Parse("c1000000-0000-0000-0000-000000000002");

    private static readonly Building[] Buildings =
    [
        new() { Id = BuildingAId, Name = "Edificio A", Code = "A" },
        new() { Id = BuildingBId, Name = "Edificio B", Code = "B" },
        new() { Id = LibraryId, Name = "Biblioteca", Code = "LIB" }
    ];

    private static readonly Room[] Rooms =
    [
        new()
        {
            Id = ClassroomA1Id, BuildingId = BuildingAId, Name = "Aula A1",
            Type = RoomType.Classroom, Capacity = 30, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ClassroomA2Id, BuildingId = BuildingAId, Name = "Aula A2",
            Type = RoomType.Classroom, Capacity = 24, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ClassroomA3Id, BuildingId = BuildingAId, Name = "Aula A3",
            Type = RoomType.Classroom, Capacity = 40, Floor = 1, IsBookable = true, IsActive = true
        },
        new()
        {
            // Dismessa: resta in archivio, ma la ricerca non deve mostrarla (US-11).
            Id = DecommissionedRoomId, BuildingId = BuildingAId, Name = "Aula dismessa",
            Type = RoomType.Classroom, Capacity = 50, Floor = 2, IsBookable = false, IsActive = false
        },
        new()
        {
            Id = ComputerLab1Id, BuildingId = BuildingAId, Name = "Laboratorio Informatica 1",
            Type = RoomType.Laboratory, Capacity = 20, Floor = 1, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ComputerLab2Id, BuildingId = BuildingAId, Name = "Laboratorio Informatica 2",
            Type = RoomType.Laboratory, Capacity = 18, Floor = 1, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = StudyRoomAId, BuildingId = BuildingAId, Name = "Sala studio A",
            Type = RoomType.StudyRoom, Capacity = 12, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ClassroomB1Id, BuildingId = BuildingBId, Name = "Aula B1",
            Type = RoomType.Classroom, Capacity = 35, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ClassroomB2Id, BuildingId = BuildingBId, Name = "Aula B2",
            Type = RoomType.Classroom, Capacity = 28, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = ElectronicsLabId, BuildingId = BuildingBId, Name = "Laboratorio Elettronica",
            Type = RoomType.Laboratory, Capacity = 16, Floor = 1, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = StudyRoomBId, BuildingId = BuildingBId, Name = "Sala studio B",
            Type = RoomType.StudyRoom, Capacity = 25, Floor = 1, IsBookable = true, IsActive = true
        },
        new()
        {
            Id = LibraryStudyRoomId, BuildingId = LibraryId, Name = "Sala studio Biblioteca",
            Type = RoomType.StudyRoom, Capacity = 60, Floor = 0, IsBookable = true, IsActive = true
        },
        new()
        {
            // Visibile ma non prenotabile: serve a verificare il comportamento in US-10.
            Id = ReadingRoomId, BuildingId = LibraryId, Name = "Sala lettura",
            Type = RoomType.StudyRoom, Capacity = 40, Floor = 1, IsBookable = false, IsActive = true
        }
    ];

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Building>().HasData(Buildings);
        modelBuilder.Entity<Room>().HasData(Rooms);
        modelBuilder.Entity<OpeningHours>().HasData(BuildOpeningHours());
    }

    private static List<OpeningHours> BuildOpeningHours()
    {
        var weekdays = new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        var openingHours = new List<OpeningHours>(Rooms.Length * weekdays.Length + 2);

        foreach (var room in Rooms)
        {
            // Il laboratorio di elettronica ha un orario diverso dagli altri spazi.
            var opensAt = room.Id == ElectronicsLabId ? new TimeOnly(9, 0) : new TimeOnly(8, 0);
            var closesAt = room.Id == ElectronicsLabId ? new TimeOnly(18, 0) : new TimeOnly(20, 0);

            foreach (var day in weekdays)
            {
                openingHours.Add(new OpeningHours
                {
                    Id = DeterministicId($"opening-hours:{room.Id}:{day}"),
                    RoomId = room.Id,
                    DayOfWeek = day,
                    OpensAt = opensAt,
                    ClosesAt = closesAt
                });
            }
        }

        // La biblioteca è aperta anche il sabato mattina.
        foreach (var roomId in new[] { LibraryStudyRoomId, ReadingRoomId })
        {
            openingHours.Add(new OpeningHours
            {
                Id = DeterministicId($"opening-hours:{roomId}:{DayOfWeek.Saturday}"),
                RoomId = roomId,
                DayOfWeek = DayOfWeek.Saturday,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(13, 0)
            });
        }

        return openingHours;
    }

    // GUID stabile per le righe generate: se cambiasse a ogni build, la migrazione cambierebbe da sola.
    private static Guid DeterministicId(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));

        return new Guid(hash);
    }
}
