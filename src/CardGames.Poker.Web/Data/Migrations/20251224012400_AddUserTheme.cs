using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGames.Poker.Web.Data.Migrations
{
	[DbContext(typeof(ApplicationDbContext))]
	[Migration("20251224012400_AddUserTheme")]
	public partial class AddUserTheme : Migration
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
