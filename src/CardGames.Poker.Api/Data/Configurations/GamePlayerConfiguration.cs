using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="GamePlayer"/> entity.
/// </summary>
public class GamePlayerConfiguration : IEntityTypeConfiguration<GamePlayer>
{
	public void Configure(EntityTypeBuilder<GamePlayer> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.VariantState)
			.HasMaxLength(GamePlayerFields.VariantState.MaxLength);

		builder.Property(t => t.Status)
			.HasConversion<int>();

		builder.Property(t => t.DropOrStayDecision)
			.HasConversion<int?>();

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.GameId);

		builder.HasIndex(t => t.PlayerId);

		builder.HasIndex(t => new { t.GameId, t.PlayerId })
			.IsUnique();

		builder.HasIndex(t => new { t.GameId, t.SeatPosition })
			.IsUnique();

		builder.HasIndex(t => new { t.GameId, t.Status });

		// Relationships
		builder.HasOne(t => t.Game)
			.WithMany(g => g.GamePlayers)
			.HasForeignKey(t => t.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(t => t.Player)
			.WithMany(p => p.GameParticipations)
			.HasForeignKey(t => t.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(t => t.Cards)
			.WithOne(c => c.GamePlayer)
			.HasForeignKey(c => c.GamePlayerId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasMany(t => t.PotContributions)
			.WithOne(pc => pc.GamePlayer)
			.HasForeignKey(pc => pc.GamePlayerId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.BettingActions)
			.WithOne(ba => ba.GamePlayer)
			.HasForeignKey(ba => ba.GamePlayerId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>
/// Field constraints for the <see cref="GamePlayer"/> entity.
/// </summary>
public static class GamePlayerFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class VariantState
	{
		public const int MinLength = 0;
		public const int MaxLength = 4000;
	}
}
