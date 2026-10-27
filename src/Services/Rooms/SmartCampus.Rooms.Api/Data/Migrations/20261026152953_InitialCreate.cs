using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartCampus.Rooms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    IsBookable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Capacity_Positive", "[Capacity] > 0");
                    table.ForeignKey(
                        name: "FK_Rooms_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningHours", x => x.Id);
                    table.CheckConstraint("CK_OpeningHours_TimeRange", "[ClosesAt] > [OpensAt]");
                    table.ForeignKey(
                        name: "FK_OpeningHours_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomEquipment",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomEquipment", x => new { x.RoomId, x.EquipmentId });
                    table.ForeignKey(
                        name: "FK_RoomEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomEquipment_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Buildings",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "A", "Edificio A" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "B", "Edificio B" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "LIB", "Biblioteca" }
                });

            migrationBuilder.InsertData(
                table: "Equipment",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-000000000001"), "Proiettore" },
                    { new Guid("e1000000-0000-0000-0000-000000000002"), "PC" },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), "Lavagna" },
                    { new Guid("e1000000-0000-0000-0000-000000000004"), "Stampante 3D" },
                    { new Guid("e1000000-0000-0000-0000-000000000005"), "Oscilloscopio" }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "BuildingId", "Capacity", "Floor", "IsActive", "IsBookable", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), new Guid("11111111-1111-1111-1111-111111111111"), 30, 0, true, true, "Aula A1", "Classroom" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), new Guid("11111111-1111-1111-1111-111111111111"), 24, 0, true, true, "Aula A2", "Classroom" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), new Guid("11111111-1111-1111-1111-111111111111"), 40, 1, true, true, "Aula A3", "Classroom" },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), new Guid("11111111-1111-1111-1111-111111111111"), 50, 2, false, false, "Aula dismessa", "Classroom" },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), new Guid("11111111-1111-1111-1111-111111111111"), 20, 1, true, true, "Laboratorio Informatica 1", "Laboratory" },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), new Guid("11111111-1111-1111-1111-111111111111"), 18, 1, true, true, "Laboratorio Informatica 2", "Laboratory" },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), new Guid("11111111-1111-1111-1111-111111111111"), 12, 0, true, true, "Sala studio A", "StudyRoom" },
                    { new Guid("b1000000-0000-0000-0000-000000000001"), new Guid("22222222-2222-2222-2222-222222222222"), 35, 0, true, true, "Aula B1", "Classroom" },
                    { new Guid("b1000000-0000-0000-0000-000000000002"), new Guid("22222222-2222-2222-2222-222222222222"), 28, 0, true, true, "Aula B2", "Classroom" },
                    { new Guid("b1000000-0000-0000-0000-000000000003"), new Guid("22222222-2222-2222-2222-222222222222"), 16, 1, true, true, "Laboratorio Elettronica", "Laboratory" },
                    { new Guid("b1000000-0000-0000-0000-000000000004"), new Guid("22222222-2222-2222-2222-222222222222"), 25, 1, true, true, "Sala studio B", "StudyRoom" },
                    { new Guid("c1000000-0000-0000-0000-000000000001"), new Guid("33333333-3333-3333-3333-333333333333"), 60, 0, true, true, "Sala studio Biblioteca", "StudyRoom" },
                    { new Guid("c1000000-0000-0000-0000-000000000002"), new Guid("33333333-3333-3333-3333-333333333333"), 40, 1, true, false, "Sala lettura", "StudyRoom" }
                });

            migrationBuilder.InsertData(
                table: "OpeningHours",
                columns: new[] { "Id", "ClosesAt", "DayOfWeek", "OpensAt", "RoomId" },
                values: new object[,]
                {
                    { new Guid("021d4509-51ad-df74-d139-a3412accf213"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("06301647-227b-ba6c-38f5-539a04a7f429"), new TimeOnly(18, 0, 0), "Thursday", new TimeOnly(9, 0, 0), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("06d43fd6-5379-9f24-d3e5-e86e1a571c50"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("0b356d28-0f29-9558-d200-5dda6e4b272a"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("0c3ed898-d3ac-22d4-8508-0702e7fa576c"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("1235cf2b-945e-6541-58ee-c0e411b5f6af"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("14bdfb14-cf94-0816-0faf-786ad9138b5c"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("23104ecb-2ec1-443c-d880-65cfee3d01de"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("2adc5352-e050-d6c7-e561-0dc904028b50"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000004") },
                    { new Guid("320b14b2-a063-7540-684c-8f174f87042b"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("33ccbbd6-0cf3-4125-521a-d271493822f3"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("33e575aa-e8c2-00b6-2878-318b629059e9"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("36a0469c-a5e9-833e-74bd-91d9968cda5d"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("43abdad6-416f-f87f-5156-8dfdf43c04a2"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000004") },
                    { new Guid("46050383-6fef-ccac-1432-5351239f83f8"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("46c91ef0-13cc-f0ca-c858-2d8e4c20321c"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("4a7de8ea-633c-f6db-73eb-5d373084510b"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("507380b7-2ab0-6f15-da92-5261e89408b3"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("5660af95-d25d-e533-07c6-97efae3fe0ce"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("5f85272e-fcac-50a3-79cd-b0c87432c202"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("6431cc63-77a4-f4bf-5d8c-cb94cb813119"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("661b1650-7966-84b8-9f48-faa5163423ee"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000004") },
                    { new Guid("66a99586-6b86-eede-ae68-838fdc092388"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("6a34091e-8439-2ffd-d5dd-c85a7aba8491"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("6bfbafec-91af-52bb-8f58-d0faf16957f1"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("6ee96b48-729b-9d16-4bf0-e297a4eaa669"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("7e256b12-339f-ad74-dcbf-11f004f0c7c3"), new TimeOnly(13, 0, 0), "Saturday", new TimeOnly(9, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("8c6ce37c-fe2d-9d70-6e27-96ec76b64d5e"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("9223a57e-a7f1-d123-db65-76b8157282db"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("928be0f7-f4cc-b548-e00c-00d640dc7d3f"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("94fe546c-0f8e-52bc-d227-c355b5258d66"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("9a37a956-a8ee-58aa-8c03-179080099be9"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("9d0345f5-5d07-8e5b-5aaf-f0b979f02ac1"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("a1802513-d3d7-a1cd-9b10-d9aa65ccdd8f"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("a2ac1a44-aac0-36a5-7051-2389cbc32bda"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("b02794c0-1a0a-9c53-539a-a318538fb0b8"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("b0d29492-f880-6b89-4bf1-a73526c816cf"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("b1b237c5-0edb-2162-0ed4-69ce98628d33"), new TimeOnly(18, 0, 0), "Friday", new TimeOnly(9, 0, 0), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("b5cb4f50-3050-f01b-d8aa-48ce94e67233"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000004") },
                    { new Guid("b6c7fc4e-17be-e0a8-4310-cc5ba1995f6a"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("b9bb13dc-f5e2-ce51-f14a-e96e40c9ee38"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("bcf300eb-c341-205a-cf32-616fbc4ee606"), new TimeOnly(18, 0, 0), "Wednesday", new TimeOnly(9, 0, 0), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("bdf0fdc5-f84a-c7c6-66b5-43b8f65b5e2b"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("c20a6220-c50c-a116-b96f-0819dd80ed9a"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("c2abdd58-9c48-deb8-98af-6a7f0240f028"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("c84d8cd3-c917-81f1-5547-1a3487399188"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("cd51fd15-26ab-a5a9-00ff-651cad45133a"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("cd5724d8-9650-3794-8753-6267f86844ed"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("cd88d352-5432-130c-036d-3d1eedc8cc14"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("ce7fa8f0-26ee-82a6-e067-242ca293c591"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("cf2f4704-43e6-0436-410e-8844075ee2d9"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("d2bec9bd-08e1-ecbb-955f-a59b3bf842c2"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("d3db95fa-38b5-9c26-9eb6-3f67d687f124"), new TimeOnly(13, 0, 0), "Saturday", new TimeOnly(9, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("d7d96c10-a707-e42f-7f74-5282bd3da12c"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("d845b83a-bc82-6a01-885a-5368ad6690d7"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("de4ad0ff-1af1-3e06-7abd-56c4bc151565"), new TimeOnly(20, 0, 0), "Tuesday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("e37d289e-0dc4-f91e-10e7-c2526d6b7adb"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("ef98fcb3-fd42-4b14-49db-1b84a16bf801"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000002") },
                    { new Guid("f132b65a-fba3-cd69-0619-972dc3d111aa"), new TimeOnly(18, 0, 0), "Tuesday", new TimeOnly(9, 0, 0), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("f2fcded2-e5a3-d8da-afdd-d0ec908983aa"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("f3e11e4d-6b78-0079-db3b-2edc2b53b3cc"), new TimeOnly(20, 0, 0), "Friday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("f43e41c1-da53-4999-d9a5-0d9d74088489"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("f5bb7a63-cc85-cb8b-c93f-44420257a2bc"), new TimeOnly(20, 0, 0), "Thursday", new TimeOnly(8, 0, 0), new Guid("c1000000-0000-0000-0000-000000000001") },
                    { new Guid("f782918f-eee7-4049-01e5-d8eb963682cb"), new TimeOnly(20, 0, 0), "Wednesday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000004") },
                    { new Guid("fb23f290-239b-43f0-cbaf-426f03326697"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("a1000000-0000-0000-0000-000000000007") },
                    { new Guid("fc829cb4-ff95-eac0-2c67-fe78e4c87047"), new TimeOnly(18, 0, 0), "Monday", new TimeOnly(9, 0, 0), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("fc9b515e-819c-7b0e-83ab-9033b53ca816"), new TimeOnly(20, 0, 0), "Monday", new TimeOnly(8, 0, 0), new Guid("b1000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.InsertData(
                table: "RoomEquipment",
                columns: new[] { "EquipmentId", "RoomId" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-000000000001"), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("a1000000-0000-0000-0000-000000000001") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("a1000000-0000-0000-0000-000000000002") },
                    { new Guid("e1000000-0000-0000-0000-000000000001"), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("a1000000-0000-0000-0000-000000000003") },
                    { new Guid("e1000000-0000-0000-0000-000000000001"), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("e1000000-0000-0000-0000-000000000002"), new Guid("a1000000-0000-0000-0000-000000000005") },
                    { new Guid("e1000000-0000-0000-0000-000000000002"), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("e1000000-0000-0000-0000-000000000004"), new Guid("a1000000-0000-0000-0000-000000000006") },
                    { new Guid("e1000000-0000-0000-0000-000000000001"), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("b1000000-0000-0000-0000-000000000001") },
                    { new Guid("e1000000-0000-0000-0000-000000000003"), new Guid("b1000000-0000-0000-0000-000000000002") },
                    { new Guid("e1000000-0000-0000-0000-000000000002"), new Guid("b1000000-0000-0000-0000-000000000003") },
                    { new Guid("e1000000-0000-0000-0000-000000000005"), new Guid("b1000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_Code",
                table: "Buildings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name",
                table: "Equipment",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningHours_RoomId_DayOfWeek",
                table: "OpeningHours",
                columns: new[] { "RoomId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomEquipment_EquipmentId",
                table: "RoomEquipment",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId_Name",
                table: "Rooms",
                columns: new[] { "BuildingId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_IsActive_Type_Capacity",
                table: "Rooms",
                columns: new[] { "IsActive", "Type", "Capacity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningHours");

            migrationBuilder.DropTable(
                name: "RoomEquipment");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Buildings");
        }
    }
}
