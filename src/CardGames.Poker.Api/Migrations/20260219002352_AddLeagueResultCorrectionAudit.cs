using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueResultCorrectionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LaunchedAtUtc",
                table: "LeagueSeasonEvents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaunchedByUserId",
                table: "LeagueSeasonEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LaunchedGameId",
                table: "LeagueSeasonEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LaunchedAtUtc",
                table: "LeagueOneOffEvents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaunchedByUserId",
                table: "LeagueOneOffEvents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LaunchedGameId",
                table: "LeagueOneOffEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeagueJoinRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueJoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueJoinRequests_LeagueInvites_InviteId",
                        column: x => x.InviteId,
                        principalTable: "LeagueInvites",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueJoinRequests_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueSeasonEventResultCorrectionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PreviousResultsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewResultsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSeasonEventResultCorrectionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResultCorrectionAudits_LeagueSeasonEvents_LeagueSeasonEventId",
                        column: x => x.LeagueSeasonEventId,
                        principalTable: "LeagueSeasonEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResultCorrectionAudits_LeagueSeasons_LeagueSeasonId",
                        column: x => x.LeagueSeasonId,
                        principalTable: "LeagueSeasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResultCorrectionAudits_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueSeasonEventResults",
                columns: table => new
                {
                    LeagueSeasonEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeagueSeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    ChipsDelta = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSeasonEventResults", x => new { x.LeagueSeasonEventId, x.UserId });
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResults_LeagueSeasonEvents_LeagueSeasonEventId",
                        column: x => x.LeagueSeasonEventId,
                        principalTable: "LeagueSeasonEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResults_LeagueSeasons_LeagueSeasonId",
                        column: x => x.LeagueSeasonId,
                        principalTable: "LeagueSeasons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeagueSeasonEventResults_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueStandingsCurrent",
                columns: table => new
                {
                    LeagueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TotalEvents = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<int>(type: "int", nullable: false),
                    TotalChipsDelta = table.Column<int>(type: "int", nullable: false),
                    LastPlacement = table.Column<int>(type: "int", nullable: true),
                    LastEventRecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStandingsCurrent", x => new { x.LeagueId, x.UserId });
                    table.ForeignKey(
                        name: "FK_LeagueStandingsCurrent_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEvents_LaunchedGameId",
                table: "LeagueSeasonEvents",
                column: "LaunchedGameId",
                unique: true,
                filter: "[LaunchedGameId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueOneOffEvents_LaunchedGameId",
                table: "LeagueOneOffEvents",
                column: "LaunchedGameId",
                unique: true,
                filter: "[LaunchedGameId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJoinRequests_InviteId",
                table: "LeagueJoinRequests",
                column: "InviteId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJoinRequests_LeagueId_RequesterUserId",
                table: "LeagueJoinRequests",
                columns: new[] { "LeagueId", "RequesterUserId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJoinRequests_LeagueId_RequesterUserId_Status_ExpiresAtUtc",
                table: "LeagueJoinRequests",
                columns: new[] { "LeagueId", "RequesterUserId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJoinRequests_LeagueId_Status_CreatedAtUtc",
                table: "LeagueJoinRequests",
                columns: new[] { "LeagueId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResultCorrectionAudits_LeagueId_LeagueSeasonId_LeagueSeasonEventId_CorrectedAtUtc",
                table: "LeagueSeasonEventResultCorrectionAudits",
                columns: new[] { "LeagueId", "LeagueSeasonId", "LeagueSeasonEventId", "CorrectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResultCorrectionAudits_LeagueSeasonEventId_CorrectedAtUtc",
                table: "LeagueSeasonEventResultCorrectionAudits",
                columns: new[] { "LeagueSeasonEventId", "CorrectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResultCorrectionAudits_LeagueSeasonId",
                table: "LeagueSeasonEventResultCorrectionAudits",
                column: "LeagueSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResults_LeagueId_LeagueSeasonId_LeagueSeasonEventId",
                table: "LeagueSeasonEventResults",
                columns: new[] { "LeagueId", "LeagueSeasonId", "LeagueSeasonEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResults_LeagueId_UserId",
                table: "LeagueSeasonEventResults",
                columns: new[] { "LeagueId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasonEventResults_LeagueSeasonId",
                table: "LeagueSeasonEventResults",
                column: "LeagueSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStandingsCurrent_LeagueId_TotalPoints_TotalChipsDelta",
                table: "LeagueStandingsCurrent",
                columns: new[] { "LeagueId", "TotalPoints", "TotalChipsDelta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueJoinRequests");

            migrationBuilder.DropTable(
                name: "LeagueSeasonEventResultCorrectionAudits");

            migrationBuilder.DropTable(
                name: "LeagueSeasonEventResults");

            migrationBuilder.DropTable(
                name: "LeagueStandingsCurrent");

            migrationBuilder.DropIndex(
                name: "IX_LeagueSeasonEvents_LaunchedGameId",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropIndex(
                name: "IX_LeagueOneOffEvents_LaunchedGameId",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedAtUtc",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedByUserId",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedGameId",
                table: "LeagueSeasonEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedAtUtc",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedByUserId",
                table: "LeagueOneOffEvents");

            migrationBuilder.DropColumn(
                name: "LaunchedGameId",
                table: "LeagueOneOffEvents");
        }
    }
}
