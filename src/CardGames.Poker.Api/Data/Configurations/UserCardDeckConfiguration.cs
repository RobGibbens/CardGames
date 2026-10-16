using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public sealed class UserCardDeckConfiguration : IEntityTypeConfiguration<UserCardDeck>
{
	public void Configure(EntityTypeBuilder<UserCardDeck> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.UserId)
			.IsRequired();

		builder.Property(x => x.Name)
			.HasMaxLength(60)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.UserId, x.Name })
			.IsUnique();

		builder.HasOne(x => x.User)
			.WithMany()
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.Images)
			.WithOne(x => x.Deck)
			.HasForeignKey(x => x.DeckId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
