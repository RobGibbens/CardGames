using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContinuousPlayIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Games_CurrentPhase_Status_NextHandStartsAt",
                table: "Games",
                columns: new[] { "CurrentPhase", "Status", "NextHandStartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_NextHandStartsAt",
                table: "Games",
                column: "NextHandStartsAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_CurrentPhase_Status_NextHandStartsAt",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_NextHandStartsAt",
                table: "Games");
        }
    }
}
