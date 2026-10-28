using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartCampus.Rooms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveEquipmentToMongoDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomEquipment");

            migrationBuilder.DropTable(
                name: "Equipment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "IX_Equipment_Name",
                table: "Equipment",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomEquipment_EquipmentId",
                table: "RoomEquipment",
                column: "EquipmentId");
        }
    }
}
