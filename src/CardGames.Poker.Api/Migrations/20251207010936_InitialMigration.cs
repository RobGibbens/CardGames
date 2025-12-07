using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BettingStructure = table.Column<int>(type: "int", nullable: false),
                    MinPlayers = table.Column<int>(type: "int", nullable: false),
                    MaxPlayers = table.Column<int>(type: "int", nullable: false),
                    InitialHoleCards = table.Column<int>(type: "int", nullable: false),
                    InitialBoardCards = table.Column<int>(type: "int", nullable: false),
                    MaxCommunityCards = table.Column<int>(type: "int", nullable: false),
                    MaxPlayerCards = table.Column<int>(type: "int", nullable: false),
                    HasDrawPhase = table.Column<bool>(type: "bit", nullable: false),
                    MaxDiscards = table.Column<int>(type: "int", nullable: false),
                    WildCardRule = table.Column<int>(type: "int", nullable: false),
                    VariantSettings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Preferences = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TotalGamesPlayed = table.Column<int>(type: "int", nullable: false),
                    TotalHandsPlayed = table.Column<int>(type: "int", nullable: false),
                    TotalHandsWon = table.Column<int>(type: "int", nullable: false),
                    TotalChipsWon = table.Column<long>(type: "bigint", nullable: false),
                    TotalChipsLost = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CurrentPhase = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentHandNumber = table.Column<int>(type: "int", nullable: false),
                    DealerPosition = table.Column<int>(type: "int", nullable: false),
                    Ante = table.Column<int>(type: "int", nullable: true),
                    SmallBlind = table.Column<int>(type: "int", nullable: true),
                    BigBlind = table.Column<int>(type: "int", nullable: true),
                    BringIn = table.Column<int>(type: "int", nullable: true),
                    SmallBet = table.Column<int>(type: "int", nullable: true),
                    BigBet = table.Column<int>(type: "int", nullable: true),
                    MinBet = table.Column<int>(type: "int", nullable: true),
                    GameSettings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentPlayerIndex = table.Column<int>(type: "int", nullable: false),
                    BringInPlayerIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentDrawPlayerIndex = table.Column<int>(type: "int", nullable: false),
                    RandomSeed = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BettingRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandNumber = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentBet = table.Column<int>(type: "int", nullable: false),
                    MinBet = table.Column<int>(type: "int", nullable: false),
                    RaiseCount = table.Column<int>(type: "int", nullable: false),
                    MaxRaises = table.Column<int>(type: "int", nullable: false),
                    LastRaiseAmount = table.Column<int>(type: "int", nullable: false),
                    PlayersInHand = table.Column<int>(type: "int", nullable: false),
                    PlayersActed = table.Column<int>(type: "int", nullable: false),
                    CurrentActorIndex = table.Column<int>(type: "int", nullable: false),
                    LastAggressorIndex = table.Column<int>(type: "int", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingRounds_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GamePlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatPosition = table.Column<int>(type: "int", nullable: false),
                    ChipStack = table.Column<int>(type: "int", nullable: false),
                    StartingChips = table.Column<int>(type: "int", nullable: false),
                    CurrentBet = table.Column<int>(type: "int", nullable: false),
                    TotalContributedThisHand = table.Column<int>(type: "int", nullable: false),
                    HasFolded = table.Column<bool>(type: "bit", nullable: false),
                    IsAllIn = table.Column<bool>(type: "bit", nullable: false),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false),
                    IsSittingOut = table.Column<bool>(type: "bit", nullable: false),
                    DropOrStayDecision = table.Column<int>(type: "int", nullable: true),
                    HasDrawnThisRound = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAtHandNumber = table.Column<int>(type: "int", nullable: false),
                    LeftAtHandNumber = table.Column<int>(type: "int", nullable: false),
                    FinalChipCount = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VariantState = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamePlayers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GamePlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandNumber = table.Column<int>(type: "int", nullable: false),
                    PotType = table.Column<int>(type: "int", nullable: false),
                    PotOrder = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    MaxContributionPerPlayer = table.Column<int>(type: "int", nullable: true),
                    IsAwarded = table.Column<bool>(type: "bit", nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WinnerPayouts = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    WinReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pots_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BettingActionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BettingRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GamePlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionOrder = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    ChipsMoved = table.Column<int>(type: "int", nullable: false),
                    ChipStackBefore = table.Column<int>(type: "int", nullable: false),
                    ChipStackAfter = table.Column<int>(type: "int", nullable: false),
                    PotBefore = table.Column<int>(type: "int", nullable: false),
                    PotAfter = table.Column<int>(type: "int", nullable: false),
                    DecisionTimeSeconds = table.Column<double>(type: "float", nullable: true),
                    IsForced = table.Column<bool>(type: "bit", nullable: false),
                    IsTimeout = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingActionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingActionRecords_BettingRounds_BettingRoundId",
                        column: x => x.BettingRoundId,
                        principalTable: "BettingRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BettingActionRecords_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GamePlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HandNumber = table.Column<int>(type: "int", nullable: false),
                    Suit = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    DealOrder = table.Column<int>(type: "int", nullable: false),
                    DealtAtPhase = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsWild = table.Column<bool>(type: "bit", nullable: false),
                    IsDiscarded = table.Column<bool>(type: "bit", nullable: false),
                    DiscardedAtDrawRound = table.Column<int>(type: "int", nullable: true),
                    IsDrawnCard = table.Column<bool>(type: "bit", nullable: false),
                    DrawnAtRound = table.Column<int>(type: "int", nullable: true),
                    IsBuyCard = table.Column<bool>(type: "bit", nullable: false),
                    DealtAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameCards_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameCards_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PotContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GamePlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    IsEligibleToWin = table.Column<bool>(type: "bit", nullable: false),
                    IsPotMatch = table.Column<bool>(type: "bit", nullable: false),
                    ContributedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PotContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PotContributions_GamePlayers_GamePlayerId",
                        column: x => x.GamePlayerId,
                        principalTable: "GamePlayers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PotContributions_Pots_PotId",
                        column: x => x.PotId,
                        principalTable: "Pots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingActionRecords_ActionAt",
                table: "BettingActionRecords",
                column: "ActionAt");

            migrationBuilder.CreateIndex(
                name: "IX_BettingActionRecords_BettingRoundId",
                table: "BettingActionRecords",
                column: "BettingRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingActionRecords_BettingRoundId_ActionOrder",
                table: "BettingActionRecords",
                columns: new[] { "BettingRoundId", "ActionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BettingActionRecords_GamePlayerId",
                table: "BettingActionRecords",
                column: "GamePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingRounds_GameId",
                table: "BettingRounds",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingRounds_GameId_HandNumber",
                table: "BettingRounds",
                columns: new[] { "GameId", "HandNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BettingRounds_GameId_HandNumber_RoundNumber",
                table: "BettingRounds",
                columns: new[] { "GameId", "HandNumber", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameCards_GameId",
                table: "GameCards",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameCards_GameId_HandNumber",
                table: "GameCards",
                columns: new[] { "GameId", "HandNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_GameCards_GameId_HandNumber_Location",
                table: "GameCards",
                columns: new[] { "GameId", "HandNumber", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_GameCards_GamePlayerId",
                table: "GameCards",
                column: "GamePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameCards_GamePlayerId_Location_DealOrder",
                table: "GameCards",
                columns: new[] { "GamePlayerId", "Location", "DealOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId",
                table: "GamePlayers",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId_PlayerId",
                table: "GamePlayers",
                columns: new[] { "GameId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId_SeatPosition",
                table: "GamePlayers",
                columns: new[] { "GameId", "SeatPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId_Status",
                table: "GamePlayers",
                columns: new[] { "GameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_PlayerId",
                table: "GamePlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_CreatedAt",
                table: "Games",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameTypeId",
                table: "Games",
                column: "GameTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status",
                table: "Games",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_CreatedAt",
                table: "Games",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameTypes_Code",
                table: "GameTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTypes_IsActive",
                table: "GameTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ExternalId",
                table: "Players",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_IsActive",
                table: "Players",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PotContributions_GamePlayerId",
                table: "PotContributions",
                column: "GamePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PotContributions_PotId",
                table: "PotContributions",
                column: "PotId");

            migrationBuilder.CreateIndex(
                name: "IX_PotContributions_PotId_GamePlayerId",
                table: "PotContributions",
                columns: new[] { "PotId", "GamePlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pots_GameId",
                table: "Pots",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Pots_GameId_HandNumber",
                table: "Pots",
                columns: new[] { "GameId", "HandNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Pots_GameId_HandNumber_PotOrder",
                table: "Pots",
                columns: new[] { "GameId", "HandNumber", "PotOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BettingActionRecords");

            migrationBuilder.DropTable(
                name: "GameCards");

            migrationBuilder.DropTable(
                name: "PotContributions");

            migrationBuilder.DropTable(
                name: "BettingRounds");

            migrationBuilder.DropTable(
                name: "GamePlayers");

            migrationBuilder.DropTable(
                name: "Pots");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "GameTypes");
        }
    }
}
