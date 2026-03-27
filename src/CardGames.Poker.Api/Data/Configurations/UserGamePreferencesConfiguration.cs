using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public sealed class UserGamePreferencesConfiguration : IEntityTypeConfiguration<UserGamePreferences>
{
	public void Configure(EntityTypeBuilder<UserGamePreferences> builder)
	{
		builder.HasKey(x => x.UserId);

		builder.Property(x => x.DefaultSmallBlind)
			.IsRequired();

		builder.Property(x => x.DefaultBigBlind)
			.IsRequired();

		builder.Property(x => x.DefaultAnte)
			.IsRequired();

		builder.Property(x => x.DefaultMinimumBet)
			.IsRequired();

		builder.Property(x => x.FavoriteVariantCodesJson);

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasOne(x => x.User)
			.WithMany()
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
