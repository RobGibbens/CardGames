using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardGames.Poker.Api.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for the <see cref="HandHistory"/> entity.
/// </summary>
public class HandHistoryConfiguration : IEntityTypeConfiguration<HandHistory>
{
    public void Configure(EntityTypeBuilder<HandHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedNever();

        builder.Property(h => h.EndReason)
            .HasConversion<int>();

        builder.Property(h => h.WinningHandDescription)
            .HasMaxLength(HandHistoryFields.WinningHandDescription.MaxLength);

        builder.Property(h => h.RowVersion)
            .IsRowVersion();

        // Indexes for efficient queries
        builder.HasIndex(h => h.GameId);

        // Unique constraint: one history record per game/hand combination
        builder.HasIndex(h => new { h.GameId, h.HandNumber })
            .IsUnique();

        builder.HasIndex(h => new { h.GameId, h.CompletedAtUtc });

        // Relationships
        builder.HasOne(h => h.Game)
            .WithMany()
            .HasForeignKey(h => h.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.Winners)
            .WithOne(w => w.HandHistory)
            .HasForeignKey(w => w.HandHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.PlayerResults)
            .WithOne(pr => pr.HandHistory)
            .HasForeignKey(pr => pr.HandHistoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Entity Framework Core configuration for the <see cref="HandHistoryWinner"/> entity.
/// </summary>
public class HandHistoryWinnerConfiguration : IEntityTypeConfiguration<HandHistoryWinner>
{
    public void Configure(EntityTypeBuilder<HandHistoryWinner> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .ValueGeneratedNever();

        builder.Property(w => w.PlayerName)
            .IsRequired()
            .HasMaxLength(HandHistoryFields.PlayerName.MaxLength);

        // Indexes
        builder.HasIndex(w => w.HandHistoryId);

        builder.HasIndex(w => w.PlayerId);

        // Relationships
        builder.HasOne(w => w.Player)
            .WithMany()
            .HasForeignKey(w => w.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity Framework Core configuration for the <see cref="HandHistoryPlayerResult"/> entity.
/// </summary>
public class HandHistoryPlayerResultConfiguration : IEntityTypeConfiguration<HandHistoryPlayerResult>
{
    public void Configure(EntityTypeBuilder<HandHistoryPlayerResult> builder)
    {
        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.Id)
            .ValueGeneratedNever();

        builder.Property(pr => pr.PlayerName)
            .IsRequired()
            .HasMaxLength(HandHistoryFields.PlayerName.MaxLength);

        builder.Property(pr => pr.ResultType)
            .HasConversion<int>();

        builder.Property(pr => pr.FoldStreet)
            .HasConversion<int?>();

        builder.Property(pr => pr.AllInStreet)
            .HasConversion<int?>();

        // Indexes
        builder.HasIndex(pr => pr.HandHistoryId);

        builder.HasIndex(pr => pr.PlayerId);

        builder.HasIndex(pr => new { pr.HandHistoryId, pr.PlayerId })
            .IsUnique();

        // Relationships
        builder.HasOne(pr => pr.Player)
            .WithMany()
            .HasForeignKey(pr => pr.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Field constraints for HandHistory entities.
/// </summary>
public static class HandHistoryFields
{
    public static class WinningHandDescription
    {
        public const int MaxLength = 200;
    }

    public static class PlayerName
    {
        public const int MaxLength = 100;
    }
}
