using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalDcDealerPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalDealersChoiceDealerPosition",
                table: "Games",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalDealersChoiceDealerPosition",
                table: "Games");
        }
    }
}
