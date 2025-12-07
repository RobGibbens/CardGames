using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="GameCard"/> entity.
/// </summary>
public class GameCardConfiguration : IEntityTypeConfiguration<GameCard>
{
	public void Configure(EntityTypeBuilder<GameCard> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.Suit)
			.HasConversion<int>();

		builder.Property(t => t.Symbol)
			.HasConversion<int>();

		builder.Property(t => t.Location)
			.HasConversion<int>();

		builder.Property(t => t.DealtAtPhase)
			.HasMaxLength(GameCardFields.DealtAtPhase.MaxLength);

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.GameId);

		builder.HasIndex(t => t.GamePlayerId);

		builder.HasIndex(t => new { t.GameId, t.HandNumber });

		builder.HasIndex(t => new { t.GameId, t.HandNumber, t.Location });

		builder.HasIndex(t => new { t.GamePlayerId, t.Location, t.DealOrder });

		// Relationships
		builder.HasOne(t => t.Game)
			.WithMany(g => g.GameCards)
			.HasForeignKey(t => t.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(t => t.GamePlayer)
		.WithMany(gp => gp.Cards)
		.HasForeignKey(t => t.GamePlayerId)
		.OnDelete(DeleteBehavior.NoAction); // Avoid cascade conflict: Game -> GameCards also cascades
	}
}

/// <summary>
/// Field constraints for the <see cref="GameCard"/> entity.
/// </summary>
public static class GameCardFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class DealtAtPhase
	{
		public const int MinLength = 0;
		public const int MaxLength = 50;
	}
}
