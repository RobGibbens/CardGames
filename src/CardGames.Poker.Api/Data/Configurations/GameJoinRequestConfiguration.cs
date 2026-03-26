using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public sealed class GameJoinRequestConfiguration : IEntityTypeConfiguration<GameJoinRequest>
{
	public void Configure(EntityTypeBuilder<GameJoinRequest> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Status)
			.HasConversion<int>();

		builder.Property(x => x.ResolvedByUserId)
			.HasMaxLength(GameJoinRequestFields.ResolvedByUserId.MaxLength);

		builder.Property(x => x.ResolvedByName)
			.HasMaxLength(GameJoinRequestFields.ResolvedByName.MaxLength);

		builder.Property(x => x.ResolutionReason)
			.HasMaxLength(GameJoinRequestFields.ResolutionReason.MaxLength);

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.GameId, x.Status });
		builder.HasIndex(x => new { x.PlayerId, x.GameId });
		builder.HasIndex(x => x.ExpiresAt);

		builder.HasOne(x => x.Game)
			.WithMany(g => g.JoinRequests)
			.HasForeignKey(x => x.GameId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Player)
			.WithMany()
			.HasForeignKey(x => x.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public static class GameJoinRequestFields
{
	public static class ResolvedByUserId
	{
		public const int MaxLength = 450;
	}

	public static class ResolvedByName
	{
		public const int MaxLength = 256;
	}

	public static class ResolutionReason
	{
		public const int MaxLength = 1024;
	}
}