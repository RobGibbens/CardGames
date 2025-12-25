using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHandHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HandHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandNumber = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndReason = table.Column<int>(type: "int", nullable: false),
                    TotalPot = table.Column<int>(type: "int", nullable: false),
                    Rake = table.Column<int>(type: "int", nullable: false),
                    WinningHandDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandHistories_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandHistoryPlayerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SeatPosition = table.Column<int>(type: "int", nullable: false),
                    ResultType = table.Column<int>(type: "int", nullable: false),
                    ReachedShowdown = table.Column<bool>(type: "bit", nullable: false),
                    FoldStreet = table.Column<int>(type: "int", nullable: true),
                    NetChipDelta = table.Column<int>(type: "int", nullable: false),
                    WentAllIn = table.Column<bool>(type: "bit", nullable: false),
                    AllInStreet = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandHistoryPlayerResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandHistoryPlayerResults_HandHistories_HandHistoryId",
                        column: x => x.HandHistoryId,
                        principalTable: "HandHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HandHistoryPlayerResults_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandHistoryWinners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AmountWon = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandHistoryWinners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandHistoryWinners_HandHistories_HandHistoryId",
                        column: x => x.HandHistoryId,
                        principalTable: "HandHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HandHistoryWinners_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandHistories_GameId",
                table: "HandHistories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_HandHistories_GameId_CompletedAtUtc",
                table: "HandHistories",
                columns: new[] { "GameId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HandHistories_GameId_HandNumber",
                table: "HandHistories",
                columns: new[] { "GameId", "HandNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandHistoryPlayerResults_HandHistoryId",
                table: "HandHistoryPlayerResults",
                column: "HandHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HandHistoryPlayerResults_HandHistoryId_PlayerId",
                table: "HandHistoryPlayerResults",
                columns: new[] { "HandHistoryId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandHistoryPlayerResults_PlayerId",
                table: "HandHistoryPlayerResults",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_HandHistoryWinners_HandHistoryId",
                table: "HandHistoryWinners",
                column: "HandHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HandHistoryWinners_PlayerId",
                table: "HandHistoryWinners",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandHistoryPlayerResults");

            migrationBuilder.DropTable(
                name: "HandHistoryWinners");

            migrationBuilder.DropTable(
                name: "HandHistories");
        }
    }
}
