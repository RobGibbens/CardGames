using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSettingsToOneOffEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ante",
                table: "LeagueOneOffEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GameTypeCode",
                table: "LeagueOneOffEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinBet",
                table: "LeagueOneOffEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TableName",
                table: "LeagueOneOffEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ante",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "GameTypeCode",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "MinBet",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "TableName",
                table: "LeagueOneOffEvents");
        }
    }
}
