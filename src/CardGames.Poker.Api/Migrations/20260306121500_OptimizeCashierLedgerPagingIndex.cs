using CardGames.Poker.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CardsDbContext))]
    [Migration("20260306121500_OptimizeCashierLedgerPagingIndex")]
    public partial class OptimizeCashierLedgerPagingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerChipLedgerEntries_PlayerId_OccurredAtUtc",
                table: "PlayerChipLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChipLedgerEntries_PlayerId_OccurredAtUtc_Id",
                table: "PlayerChipLedgerEntries",
                columns: new[] { "PlayerId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerChipLedgerEntries_PlayerId_OccurredAtUtc_Id",
                table: "PlayerChipLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChipLedgerEntries_PlayerId_OccurredAtUtc",
                table: "PlayerChipLedgerEntries",
                columns: new[] { "PlayerId", "OccurredAtUtc" });
        }
    }
}
