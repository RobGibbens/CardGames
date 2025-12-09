using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="Player"/> entity.
/// </summary>
public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
	public void Configure(EntityTypeBuilder<Player> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Id)
			.ValueGeneratedNever();

		builder.Property(t => t.Name)
			.IsRequired()
			.HasMaxLength(PlayerFields.Name.MaxLength);

		builder.Property(t => t.Email)
			.HasMaxLength(PlayerFields.Email.MaxLength);

		builder.Property(t => t.ExternalId)
			.HasMaxLength(PlayerFields.ExternalId.MaxLength);

		builder.Property(t => t.AvatarUrl)
			.HasMaxLength(PlayerFields.AvatarUrl.MaxLength);

		builder.Property(t => t.Preferences)
			.HasMaxLength(PlayerFields.Preferences.MaxLength);

		builder.Property(t => t.RowVersion)
			.IsRowVersion();

		// Indexes
		builder.HasIndex(t => t.Name);

		builder.HasIndex(t => t.Email)
			.IsUnique();

		builder.HasIndex(t => t.ExternalId)
			.IsUnique();

		builder.HasIndex(t => t.IsActive);

		// Relationships
		builder.HasMany(t => t.GameParticipations)
			.WithOne(gp => gp.Player)
			.HasForeignKey(gp => gp.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

/// <summary>
/// Field constraints for the <see cref="Player"/> entity.
/// </summary>
public static class PlayerFields
{
	public static class Id
	{
		public const int MinLength = 36;
		public const int MaxLength = 38;
	}

	public static class Name
	{
		public const int MinLength = 1;
		public const int MaxLength = 100;
	}

	public static class Email
	{
		public const int MinLength = 5;
		public const int MaxLength = 256;
	}

	public static class ExternalId
	{
		public const int MinLength = 1;
		public const int MaxLength = 256;
	}

	public static class AvatarUrl
	{
		public const int MinLength = 0;
		public const int MaxLength = 2000;
	}

	public static class Preferences
	{
		public const int MinLength = 0;
		public const int MaxLength = 4000;
	}
}
