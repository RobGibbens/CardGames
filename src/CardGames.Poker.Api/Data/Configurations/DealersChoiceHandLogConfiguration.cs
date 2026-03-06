using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public class DealersChoiceHandLogConfiguration : IEntityTypeConfiguration<DealersChoiceHandLog>
{
	public void Configure(EntityTypeBuilder<DealersChoiceHandLog> builder)
	{
		builder.HasKey(d => d.Id);

		builder.Property(d => d.Id)
			.ValueGeneratedNever();

		builder.Property(d => d.GameTypeCode)
			.IsRequired()
			.HasMaxLength(50);

		builder.Property(d => d.GameTypeName)
			.IsRequired()
			.HasMaxLength(200);

		builder.HasIndex(d => new { d.GameId, d.HandNumber })
			.IsUnique();
	}
}
