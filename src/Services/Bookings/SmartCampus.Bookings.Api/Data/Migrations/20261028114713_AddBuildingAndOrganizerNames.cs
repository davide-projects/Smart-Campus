using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Bookings.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingAndOrganizerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizerName",
                table: "Reservations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingName",
                table: "BookableRooms",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizerName",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "BuildingName",
                table: "BookableRooms");
        }
    }
}
