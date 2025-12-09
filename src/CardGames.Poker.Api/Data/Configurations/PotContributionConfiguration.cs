using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="PotContribution"/> entity.
/// </summary>
public class PotContributionConfiguration : IEntityTypeConfiguration<PotContribution>
{
	public void Configure(EntityTypeBuilder<PotContribution> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.PotId);

		builder.HasIndex(t => t.GamePlayerId);

		builder.HasIndex(t => new { t.PotId, t.GamePlayerId })
			.IsUnique();

		// Relationships
		builder.HasOne(t => t.Pot)
			.WithMany(p => p.Contributions)
			.HasForeignKey(t => t.PotId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(t => t.GamePlayer)
			.WithMany(gp => gp.PotContributions)
			.HasForeignKey(t => t.GamePlayerId)
			.OnDelete(DeleteBehavior.NoAction); // Avoid cascade conflict
	}
}

/// <summary>
/// Field constraints for the <see cref="PotContribution"/> entity.
/// </summary>
public static class PotContributionFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}
}
