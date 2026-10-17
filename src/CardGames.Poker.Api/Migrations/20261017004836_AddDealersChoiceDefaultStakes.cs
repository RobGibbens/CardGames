using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations;

/// <inheritdoc />
public partial class _20261017004836_AddDealersChoiceDefaultStakes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DefaultAnte",
            table: "Games",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DefaultBigBlind",
            table: "Games",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DefaultSmallBlind",
            table: "Games",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultAnte",
            table: "Games");

        migrationBuilder.DropColumn(
            name: "DefaultBigBlind",
            table: "Games");

        migrationBuilder.DropColumn(
            name: "DefaultSmallBlind",
            table: "Games");
    }
}
