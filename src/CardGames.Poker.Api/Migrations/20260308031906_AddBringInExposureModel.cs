using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBringInExposureModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HandNumber",
                table: "PlayerChipLedgerEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BringInAmount",
                table: "GamePlayers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChipLedgerEntries_HandSettlement_Idempotency",
                table: "PlayerChipLedgerEntries",
                columns: new[] { "PlayerId", "ReferenceId", "HandNumber", "Type" },
                filter: "[Type] = 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerChipLedgerEntries_HandSettlement_Idempotency",
                table: "PlayerChipLedgerEntries");

            migrationBuilder.DropColumn(
                name: "HandNumber",
                table: "PlayerChipLedgerEntries");

            migrationBuilder.DropColumn(
                name: "BringInAmount",
                table: "GamePlayers");
        }
    }
}
