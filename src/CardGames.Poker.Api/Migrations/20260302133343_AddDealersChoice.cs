using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDealersChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GameTypeId",
                table: "Games",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CurrentHandGameTypeCode",
                table: "Games",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealersChoiceDealerPosition",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDealersChoice",
                table: "Games",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DealersChoiceHandLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandNumber = table.Column<int>(type: "int", nullable: false),
                    GameTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GameTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DealerPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealerSeatPosition = table.Column<int>(type: "int", nullable: false),
                    Ante = table.Column<int>(type: "int", nullable: false),
                    MinBet = table.Column<int>(type: "int", nullable: false),
                    ChosenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealersChoiceHandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealersChoiceHandLogs_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealersChoiceHandLogs_GameId_HandNumber",
                table: "DealersChoiceHandLogs",
                columns: new[] { "GameId", "HandNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealersChoiceHandLogs");

            migrationBuilder.DropColumn(
                name: "CurrentHandGameTypeCode",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DealersChoiceDealerPosition",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsDealersChoice",
                table: "Games");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameTypeId",
                table: "Games",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
