using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public class PlayerChipAccountConfiguration : IEntityTypeConfiguration<PlayerChipAccount>
{
	public void Configure(EntityTypeBuilder<PlayerChipAccount> builder)
	{
		builder.HasKey(x => x.PlayerId);

		builder.Property(x => x.Balance)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasOne(x => x.Player)
			.WithMany()
			.HasForeignKey(x => x.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class PlayerChipLedgerEntryConfiguration : IEntityTypeConfiguration<PlayerChipLedgerEntry>
{
	public void Configure(EntityTypeBuilder<PlayerChipLedgerEntry> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Type)
			.HasConversion<int>();

		builder.Property(x => x.ReferenceType)
			.HasMaxLength(64);

		builder.Property(x => x.Reason)
			.HasMaxLength(512);

		builder.Property(x => x.ActorUserId)
			.HasMaxLength(256);

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.PlayerId, x.OccurredAtUtc });
		builder.HasIndex(x => x.Type);

		builder.HasOne(x => x.Player)
			.WithMany()
			.HasForeignKey(x => x.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
