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
			migrationBuilder.Sql(
				"""
				IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
				   AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'Theme') IS NULL
				BEGIN
				    ALTER TABLE [dbo].[AspNetUsers] ADD [Theme] nvarchar(100) NULL;
				END
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
				   AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'Theme') IS NOT NULL
				BEGIN
				    ALTER TABLE [dbo].[AspNetUsers] DROP COLUMN [Theme];
				END
				""");
		}
	}
}
