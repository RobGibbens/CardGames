using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentBuyInToLeagueEventsAndGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TournamentBuyIn",
                table: "LeagueSeasonEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentBuyIn",
                table: "LeagueOneOffEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentBuyIn",
                table: "Games",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TournamentBuyIn",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "TournamentBuyIn",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "TournamentBuyIn",
                table: "Games");
        }
    }
}
