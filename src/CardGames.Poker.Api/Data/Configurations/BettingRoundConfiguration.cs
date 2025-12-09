using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="BettingRound"/> entity.
/// </summary>
public class BettingRoundConfiguration : IEntityTypeConfiguration<BettingRound>
{
	public void Configure(EntityTypeBuilder<BettingRound> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.Street)
			.IsRequired()
			.HasMaxLength(BettingRoundFields.Street.MaxLength);

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.GameId);

		builder.HasIndex(t => new { t.GameId, t.HandNumber });

		builder.HasIndex(t => new { t.GameId, t.HandNumber, t.RoundNumber })
			.IsUnique();

		// Relationships
		builder.HasOne(t => t.Game)
			.WithMany(g => g.BettingRounds)
			.HasForeignKey(t => t.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.Actions)
			.WithOne(ba => ba.BettingRound)
			.HasForeignKey(ba => ba.BettingRoundId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>
/// Field constraints for the <see cref="BettingRound"/> entity.
/// </summary>
public static class BettingRoundFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class Street
	{
		public const int MinLength = 1;
		public const int MaxLength = 50;
	}
}
