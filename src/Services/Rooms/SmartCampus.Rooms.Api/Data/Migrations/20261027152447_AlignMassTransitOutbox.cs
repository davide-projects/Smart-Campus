using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Rooms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignMassTransitOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mantiene la colonna legacy BusName, così la migrazione non elimina metadati dell'outbox.
            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage");
        }
    }
}
