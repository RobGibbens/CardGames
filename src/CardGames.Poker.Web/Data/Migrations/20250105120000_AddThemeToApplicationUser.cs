using CardGames.Poker.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Web.Data.Migrations
{
	[DbContext(typeof(ApplicationDbContext))]
	[Migration("20250105120000_AddThemeToApplicationUser")]
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
