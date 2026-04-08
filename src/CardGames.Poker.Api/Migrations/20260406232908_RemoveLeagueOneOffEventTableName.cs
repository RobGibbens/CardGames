using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLeagueOneOffEventTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TableName",
                table: "LeagueOneOffEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TableName",
                table: "LeagueOneOffEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }
    }
}
