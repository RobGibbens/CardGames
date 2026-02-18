using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

public sealed class LeagueConfiguration : IEntityTypeConfiguration<League>
{
	public void Configure(EntityTypeBuilder<League> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Name)
			.HasMaxLength(120)
			.IsRequired();

		builder.Property(x => x.Description)
			.HasMaxLength(512);

		builder.Property(x => x.CreatedByUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => x.CreatedByUserId);
		builder.HasIndex(x => x.CreatedAtUtc);
	}
}

public sealed class LeagueMemberCurrentConfiguration : IEntityTypeConfiguration<LeagueMemberCurrent>
{
	public void Configure(EntityTypeBuilder<LeagueMemberCurrent> builder)
	{
		builder.HasKey(x => new { x.LeagueId, x.UserId });

		builder.Property(x => x.UserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.Role)
			.HasConversion<int>();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.UserId, x.IsActive });
		builder.HasIndex(x => new { x.LeagueId, x.Role, x.IsActive });

		builder.HasOne(x => x.League)
			.WithMany(x => x.Members)
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

public sealed class LeagueMembershipEventConfiguration : IEntityTypeConfiguration<LeagueMembershipEvent>
{
	public void Configure(EntityTypeBuilder<LeagueMembershipEvent> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.UserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.ActorUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.EventType)
			.HasConversion<int>();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.LeagueId, x.OccurredAtUtc });

		builder.HasOne(x => x.League)
			.WithMany()
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

public sealed class LeagueInviteConfiguration : IEntityTypeConfiguration<LeagueInvite>
{
	public void Configure(EntityTypeBuilder<LeagueInvite> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.TokenHash)
			.HasMaxLength(128)
			.IsRequired();

		builder.Property(x => x.Status)
			.HasConversion<int>();

		builder.Property(x => x.CreatedByUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.RevokedByUserId)
			.HasMaxLength(256);

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => x.TokenHash)
			.IsUnique();

		builder.HasIndex(x => new { x.LeagueId, x.Status, x.ExpiresAtUtc });

		builder.HasOne(x => x.League)
			.WithMany(x => x.Invites)
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

public sealed class LeagueSeasonConfiguration : IEntityTypeConfiguration<LeagueSeason>
{
	public void Configure(EntityTypeBuilder<LeagueSeason> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Name)
			.HasMaxLength(120)
			.IsRequired();

		builder.Property(x => x.Status)
			.HasConversion<int>();

		builder.Property(x => x.CreatedByUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.LeagueId, x.Status, x.CreatedAtUtc });

		builder.HasOne(x => x.League)
			.WithMany(x => x.Seasons)
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

public sealed class LeagueSeasonEventConfiguration : IEntityTypeConfiguration<LeagueSeasonEvent>
{
	public void Configure(EntityTypeBuilder<LeagueSeasonEvent> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Name)
			.HasMaxLength(120)
			.IsRequired();

		builder.Property(x => x.Status)
			.HasConversion<int>();

		builder.Property(x => x.Notes)
			.HasMaxLength(512);

		builder.Property(x => x.CreatedByUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.LeagueId, x.LeagueSeasonId, x.ScheduledAtUtc });
		builder.HasIndex(x => new { x.LeagueSeasonId, x.SequenceNumber })
			.IsUnique()
			.HasFilter("[SequenceNumber] IS NOT NULL");

		builder.HasOne(x => x.League)
			.WithMany(x => x.SeasonEvents)
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.NoAction);

		builder.HasOne(x => x.Season)
			.WithMany(x => x.Events)
			.HasForeignKey(x => x.LeagueSeasonId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}

public sealed class LeagueOneOffEventConfiguration : IEntityTypeConfiguration<LeagueOneOffEvent>
{
	public void Configure(EntityTypeBuilder<LeagueOneOffEvent> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Id)
			.ValueGeneratedNever();

		builder.Property(x => x.Name)
			.HasMaxLength(120)
			.IsRequired();

		builder.Property(x => x.EventType)
			.HasConversion<int>();

		builder.Property(x => x.Status)
			.HasConversion<int>();

		builder.Property(x => x.Notes)
			.HasMaxLength(512);

		builder.Property(x => x.CreatedByUserId)
			.HasMaxLength(256)
			.IsRequired();

		builder.Property(x => x.RowVersion)
			.IsRowVersion();

		builder.HasIndex(x => new { x.LeagueId, x.ScheduledAtUtc });

		builder.HasOne(x => x.League)
			.WithMany(x => x.OneOffEvents)
			.HasForeignKey(x => x.LeagueId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}