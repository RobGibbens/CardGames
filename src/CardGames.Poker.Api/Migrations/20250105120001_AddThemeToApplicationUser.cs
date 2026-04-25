using CardGames.Poker.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Api.Migrations
{
	[DbContext(typeof(CardsDbContext))]
	[Migration("20250105120001_AddThemeToApplicationUser")]
	/// <inheritdoc />
	public partial class AddThemeToApplicationUser : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "Theme",
				table: "AspNetUsers",
				type: "nvarchar(100)",
				maxLength: 100,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "Theme",
				table: "AspNetUsers");
		}
	}
}
