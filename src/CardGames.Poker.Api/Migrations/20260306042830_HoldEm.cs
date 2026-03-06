using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class HoldEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BigBlind",
                table: "DealersChoiceHandLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmallBlind",
                table: "DealersChoiceHandLogs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BigBlind",
                table: "DealersChoiceHandLogs");

            migrationBuilder.DropColumn(
                name: "SmallBlind",
                table: "DealersChoiceHandLogs");
        }
    }
}
