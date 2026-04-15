using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRebuyGracePause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPausedForRebuyGrace",
                table: "Games",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RebuyGraceEndsAt",
                table: "Games",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RebuyGraceStartedAt",
                table: "Games",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPausedForRebuyGrace",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RebuyGraceEndsAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RebuyGraceStartedAt",
                table: "Games");
        }
    }
}
