using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueBlindSettingsToEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ante",
                table: "LeagueSeasonEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BigBlind",
                table: "LeagueSeasonEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameTypeCode",
                table: "LeagueSeasonEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinBet",
                table: "LeagueSeasonEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmallBlind",
                table: "LeagueSeasonEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BigBlind",
                table: "LeagueOneOffEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmallBlind",
                table: "LeagueOneOffEvents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ante",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "BigBlind",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "GameTypeCode",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "MinBet",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "SmallBlind",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "BigBlind",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "SmallBlind",
                table: "LeagueOneOffEvents");
        }
    }
}
