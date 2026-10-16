using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public sealed class UserCardDeckImageConfiguration : IEntityTypeConfiguration<UserCardDeckImage>
{
	public void Configure(EntityTypeBuilder<UserCardDeckImage> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.SlotKey)
			.HasMaxLength(32)
			.IsRequired();

		builder.Property(x => x.Suit)
			.HasMaxLength(16);

		builder.Property(x => x.Rank)
			.HasMaxLength(16);

		builder.Property(x => x.BlobName)
			.HasMaxLength(512)
			.IsRequired();

		builder.Property(x => x.ContentType)
			.HasMaxLength(100)
			.IsRequired();

		builder.HasIndex(x => new { x.DeckId, x.SlotKey })
			.IsUnique();
	}
}
