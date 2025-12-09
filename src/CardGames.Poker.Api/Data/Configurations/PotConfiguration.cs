using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="Pot"/> entity.
/// </summary>
public class PotConfiguration : IEntityTypeConfiguration<Pot>
{
	public void Configure(EntityTypeBuilder<Pot> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.PotType)
			.HasConversion<int>();

		builder.Property(t => t.WinnerPayouts)
			.HasMaxLength(PotFields.WinnerPayouts.MaxLength);

		builder.Property(t => t.WinReason)
			.HasMaxLength(PotFields.WinReason.MaxLength);

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.GameId);

		builder.HasIndex(t => new { t.GameId, t.HandNumber });

		builder.HasIndex(t => new { t.GameId, t.HandNumber, t.PotOrder });

		// Relationships
		builder.HasOne(t => t.Game)
			.WithMany(g => g.Pots)
			.HasForeignKey(t => t.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(t => t.Contributions)
			.WithOne(pc => pc.Pot)
			.HasForeignKey(pc => pc.PotId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

/// <summary>
/// Field constraints for the <see cref="Pot"/> entity.
/// </summary>
public static class PotFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class WinnerPayouts
	{
		public const int MinLength = 0;
		public const int MaxLength = 4000;
	}

	public static class WinReason
	{
		public const int MinLength = 0;
		public const int MaxLength = 500;
	}
}
