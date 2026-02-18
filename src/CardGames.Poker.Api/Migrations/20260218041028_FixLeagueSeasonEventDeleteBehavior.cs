using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixLeagueSeasonEventDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueSeasonEvents_Leagues_LeagueId",
                table: "LeagueSeasonEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueSeasonEvents_Leagues_LeagueId",
                table: "LeagueSeasonEvents",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueSeasonEvents_Leagues_LeagueId",
                table: "LeagueSeasonEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueSeasonEvents_Leagues_LeagueId",
                table: "LeagueSeasonEvents",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
