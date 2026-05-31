using System.Diagnostics;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Implementation of <see cref="IHandHistoryRecorder"/> that persists hand history to the database.
/// </summary>
public sealed class HandHistoryRecorder : IHandHistoryRecorder
{
    private readonly CardsDbContext _context;
    private readonly ILogger<HandHistoryRecorder> _logger;
    private readonly HandHistoryTelemetry _telemetry;

    public HandHistoryRecorder(CardsDbContext context, ILogger<HandHistoryRecorder> logger, HandHistoryTelemetry telemetry)
    {
        _context = context;
        _logger = logger;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public async Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        using var activity = PokerActivitySource.Source.StartActivity("hand_history.record");
        activity?.SetTag("game.id", parameters.GameId);
        activity?.SetTag("hand.number", parameters.HandNumber);

        // Resolve the low-cardinality variant code for the metric tag (best-effort).
        var gameType = await ResolveGameTypeCodeAsync(parameters.GameId, cancellationToken);

        // Check for existing record (idempotency)
        var existingRecord = await _context.HandHistories
            .AsNoTracking()
            .AnyAsync(h => h.GameId == parameters.GameId && h.HandNumber == parameters.HandNumber, cancellationToken);

        if (existingRecord)
        {
            _logger.LogDebug(
                "Hand history already exists for GameId={GameId}, HandNumber={HandNumber}. Skipping.",
                parameters.GameId,
                parameters.HandNumber);
            return false;
        }

        try
        {
            var handHistory = CreateHandHistoryEntity(parameters);

            _context.HandHistories.Add(handHistory);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recorded hand history for GameId={GameId}, HandNumber={HandNumber}, EndReason={EndReason}, TotalPot={TotalPot}",
                parameters.GameId,
                parameters.HandNumber,
                handHistory.EndReason,
                parameters.TotalPot);

            _telemetry.RecordOutcome(gameType, "recorded");

            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent insert - treat as success (idempotency)
            _logger.LogDebug(
                "Concurrent insert detected for GameId={GameId}, HandNumber={HandNumber}. Treating as success.",
                parameters.GameId,
                parameters.HandNumber);
            return false;
        }
        catch (Exception ex)
        {
            _telemetry.RecordOutcome(gameType, "failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Failed to record hand history for GameId={GameId}, HandNumber={HandNumber}",
                parameters.GameId,
                parameters.HandNumber);

            throw;
        }
    }

    private async Task<string> ResolveGameTypeCodeAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var code = await _context.Games
            .AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => g.CurrentHandGameTypeCode ?? (g.GameType != null ? g.GameType.Code : null))
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(code) ? "unknown" : code;
    }

    private static HandHistory CreateHandHistoryEntity(RecordHandHistoryParameters parameters)
    {
        var handHistory = new HandHistory
        {
            Id = Guid.CreateVersion7(),
            GameId = parameters.GameId,
            HandNumber = parameters.HandNumber,
            CompletedAtUtc = parameters.CompletedAtUtc,
            EndReason = parameters.WonByFold ? HandEndReason.FoldedToWinner : HandEndReason.Showdown,
            TotalPot = parameters.TotalPot,
            Rake = 0, // Rake not currently implemented
            WinningHandDescription = parameters.WinningHandDescription
        };

        // Add winners
        foreach (var winner in parameters.Winners)
        {
            handHistory.Winners.Add(new HandHistoryWinner
            {
                Id = Guid.CreateVersion7(),
                HandHistoryId = handHistory.Id,
                PlayerId = winner.PlayerId,
                PlayerName = winner.PlayerName,
                AmountWon = winner.AmountWon
            });
        }

        // Add player results
        foreach (var playerResult in parameters.PlayerResults)
        {
            var resultType = DetermineResultType(playerResult);
            var foldStreet = ParseFoldStreet(playerResult.FoldStreet);

            handHistory.PlayerResults.Add(new HandHistoryPlayerResult
            {
                Id = Guid.CreateVersion7(),
                HandHistoryId = handHistory.Id,
                PlayerId = playerResult.PlayerId,
                PlayerName = playerResult.PlayerName,
                SeatPosition = playerResult.SeatPosition,
                ResultType = resultType,
                ReachedShowdown = playerResult.ReachedShowdown,
                FoldStreet = foldStreet,
                NetChipDelta = playerResult.NetChipDelta,
                WentAllIn = playerResult.WentAllIn,
                AllInStreet = null, // Not currently tracked
                ShowdownCards = playerResult.ShowdownCards != null && playerResult.ShowdownCards.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(playerResult.ShowdownCards)
                    : null
            });
        }

        return handHistory;
    }

    private static PlayerResultType DetermineResultType(PlayerResultInfo playerResult)
    {
        if (playerResult.HasFolded)
        {
            return PlayerResultType.Folded;
        }

        if (playerResult.IsWinner)
        {
            return playerResult.IsSplitPot ? PlayerResultType.SplitPotWon : PlayerResultType.Won;
        }

        return PlayerResultType.Lost;
    }

    private static FoldStreet? ParseFoldStreet(string? foldStreetName)
    {
        if (string.IsNullOrEmpty(foldStreetName))
        {
            return null;
        }

        // Map phase names to FoldStreet enum values
        return foldStreetName.ToUpperInvariant() switch
        {
            "PREFLOP" => FoldStreet.Preflop,
            "FLOP" => FoldStreet.Flop,
            "TURN" => FoldStreet.Turn,
            "RIVER" => FoldStreet.River,
            "FIRSTBETTINGROUND" or "FIRSTROUND" or "1STROUND" => FoldStreet.FirstRound,
            "DRAWPHASE" or "DRAW" => FoldStreet.DrawPhase,
            "SECONDBETTINGROUND" or "SECONDROUND" or "2NDROUND" => FoldStreet.SecondRound,
            "THIRDSTREET" or "3RDSTREET" => FoldStreet.ThirdStreet,
            "FOURTHSTREET" or "4THSTREET" => FoldStreet.FourthStreet,
            "FIFTHSTREET" or "5THSTREET" => FoldStreet.FifthStreet,
            "SIXTHSTREET" or "6THSTREET" => FoldStreet.SixthStreet,
            "SEVENTHSTREET" or "7THSTREET" => FoldStreet.SeventhStreet,
            _ => null
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check for unique constraint violation in different database providers
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_", StringComparison.OrdinalIgnoreCase);
    }
}
