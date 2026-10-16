using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations;

/// <inheritdoc />
public partial class _20261016161821_AddUserCardDecks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SelectedCardDeckId",
            table: "AspNetUsers",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserCardDecks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserCardDecks", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserCardDecks_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserCardDeckImages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SlotKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Suit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                Rank = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                IsBack = table.Column<bool>(type: "bit", nullable: false),
                BlobName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Width = table.Column<int>(type: "int", nullable: false),
                Height = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserCardDeckImages", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserCardDeckImages_UserCardDecks_DeckId",
                    column: x => x.DeckId,
                    principalTable: "UserCardDecks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_SelectedCardDeckId",
            table: "AspNetUsers",
            column: "SelectedCardDeckId");

        migrationBuilder.CreateIndex(
            name: "IX_UserCardDeckImages_DeckId_SlotKey",
            table: "UserCardDeckImages",
            columns: new[] { "DeckId", "SlotKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserCardDecks_UserId_Name",
            table: "UserCardDecks",
            columns: new[] { "UserId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserCardDeckImages");

        migrationBuilder.DropTable(
            name: "UserCardDecks");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_SelectedCardDeckId",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "SelectedCardDeckId",
            table: "AspNetUsers");
    }
}
