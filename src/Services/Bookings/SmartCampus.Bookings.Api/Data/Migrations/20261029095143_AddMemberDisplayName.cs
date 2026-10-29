using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Bookings.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "ReservationMembers",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "ReservationMembers");
        }
    }
}
