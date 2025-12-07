using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="BettingActionRecord"/> entity.
/// </summary>
public class BettingActionRecordConfiguration : IEntityTypeConfiguration<BettingActionRecord>
{
	public void Configure(EntityTypeBuilder<BettingActionRecord> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.ActionType)
			.HasConversion<int>();

		builder.Property(t => t.Note)
			.HasMaxLength(BettingActionRecordFields.Note.MaxLength);

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.BettingRoundId);

		builder.HasIndex(t => t.GamePlayerId);

		builder.HasIndex(t => new { t.BettingRoundId, t.ActionOrder })
			.IsUnique();

		builder.HasIndex(t => t.ActionAt);

		// Relationships
		builder.HasOne(t => t.BettingRound)
			.WithMany(br => br.Actions)
			.HasForeignKey(t => t.BettingRoundId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(t => t.GamePlayer)
			.WithMany(gp => gp.BettingActions)
			.HasForeignKey(t => t.GamePlayerId)
			.OnDelete(DeleteBehavior.NoAction); // Avoid cascade conflict
	}
}

/// <summary>
/// Field constraints for the <see cref="BettingActionRecord"/> entity.
/// </summary>
public static class BettingActionRecordFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class Note
	{
		public const int MinLength = 0;
		public const int MaxLength = 500;
	}
}
