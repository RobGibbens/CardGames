using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChipCheckPauseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChipCheckPauseEndsAt",
                table: "Games",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChipCheckPauseStartedAt",
                table: "Games",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPausedForChipCheck",
                table: "Games",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoDropOnDropOrStay",
                table: "GamePlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChipCheckPauseEndsAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ChipCheckPauseStartedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsPausedForChipCheck",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AutoDropOnDropOrStay",
                table: "GamePlayers");
        }
    }
}
