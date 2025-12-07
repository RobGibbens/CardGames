using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="GameType"/> entity.
/// </summary>
public class GameTypeConfiguration : IEntityTypeConfiguration<GameType>
{
	public void Configure(EntityTypeBuilder<GameType> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.Name)
			.IsRequired()
			.HasMaxLength(GameTypeFields.Name.MaxLength);

		builder.Property(t => t.Code)
			.IsRequired()
			.HasMaxLength(GameTypeFields.Code.MaxLength);

		builder.Property(t => t.Description)
			.HasMaxLength(GameTypeFields.Description.MaxLength);

		builder.Property(t => t.VariantSettings)
			.HasMaxLength(GameTypeFields.VariantSettings.MaxLength);

		builder.Property(t => t.BettingStructure)
			.HasConversion<int>();

		builder.Property(t => t.WildCardRule)
			.HasConversion<int>();

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.Code)
			.IsUnique();

		builder.HasIndex(t => t.IsActive);

		// Relationships
		builder.HasMany(t => t.Games)
			.WithOne(g => g.GameType)
			.HasForeignKey(g => g.GameTypeId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

/// <summary>
/// Field constraints for the <see cref="GameType"/> entity.
/// </summary>
public static class GameTypeFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class Name
	{
		public const int MinLength = 2;
		public const int MaxLength = 100;
	}

	public static class Code
	{
		public const int MinLength = 2;
		public const int MaxLength = 50;
	}

	public static class Description
	{
		public const int MinLength = 0;
		public const int MaxLength = 2000;
	}

	public static class VariantSettings
	{
		public const int MinLength = 0;
		public const int MaxLength = 4000;
	}
}
