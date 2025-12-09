using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="Game"/> entity.
/// </summary>
public class GameConfiguration : IEntityTypeConfiguration<Game>
{
	public void Configure(EntityTypeBuilder<Game> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.Name)
			.HasMaxLength(GameFields.Name.MaxLength);

		builder.Property(t => t.CurrentPhase)
			.IsRequired()
			.HasMaxLength(GameFields.CurrentPhase.MaxLength);

		builder.Property(t => t.GameSettings)
			.HasMaxLength(GameFields.GameSettings.MaxLength);

		builder.Property(t => t.Status)
			.HasConversion<int>();

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.GameTypeId);

		builder.HasIndex(t => t.Status);

		builder.HasIndex(t => t.CreatedAt);

		builder.HasIndex(t => new { t.Status, t.CreatedAt });

		// Relationships
		builder.HasOne(t => t.GameType)
			.WithMany(gt => gt.Games)
			.HasForeignKey(t => t.GameTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(t => t.GamePlayers)
			.WithOne(gp => gp.Game)
			.HasForeignKey(gp => gp.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.GameCards)
			.WithOne(gc => gc.Game)
			.HasForeignKey(gc => gc.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.Pots)
			.WithOne(p => p.Game)
			.HasForeignKey(p => p.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.BettingRounds)
			.WithOne(br => br.Game)
			.HasForeignKey(br => br.GameId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>
/// Field constraints for the <see cref="Game"/> entity.
/// </summary>
public static class GameFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class Name
	{
		public const int MinLength = 0;
		public const int MaxLength = 200;
	}

	public static class CurrentPhase
	{
		public const int MinLength = 1;
		public const int MaxLength = 50;
	}

	public static class GameSettings
	{
		public const int MinLength = 0;
		public const int MaxLength = 4000;
	}
}
