using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;

/// <summary>
/// Handles the <see cref="AcknowledgePotMatchCommand"/> to process pot matching in Kings and Lows.
/// </summary>
public class AcknowledgePotMatchCommandHandler(CardsDbContext context)
	: IRequestHandler<AcknowledgePotMatchCommand, OneOf<AcknowledgePotMatchSuccessful, AcknowledgePotMatchError>>
{
	public async Task<OneOf<AcknowledgePotMatchSuccessful, AcknowledgePotMatchError>> Handle(
		AcknowledgePotMatchCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and pots
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new AcknowledgePotMatchError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = AcknowledgePotMatchErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in PotMatching phase
		if (game.CurrentPhase != nameof(Phases.PotMatching))
		{
			return new AcknowledgePotMatchError
			{
				Message = $"Cannot process pot matching. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(Phases.PotMatching)}' phase.",
				Code = AcknowledgePotMatchErrorCode.InvalidPhase
			};
		}

		// 3. Get the awarded pot for this hand to determine the pot amount and winners
		var awardedPot = game.Pots
			.FirstOrDefault(p => p.HandNumber == game.CurrentHandNumber && p.IsAwarded);

		if (awardedPot is null)
		{
			return new AcknowledgePotMatchError
			{
				Message = "No awarded pot found for the current hand. Showdown must complete before pot matching.",
				Code = AcknowledgePotMatchErrorCode.InvalidPhase
			};
		}

		var currentPot = awardedPot.Amount;

		// 4. Determine winners from the awarded pot's WinnerPayouts
		var winnerPlayerIds = new HashSet<Guid>();
		if (!string.IsNullOrEmpty(awardedPot.WinnerPayouts))
		{
			try
			{
				using var doc = JsonDocument.Parse(awardedPot.WinnerPayouts);
				foreach (var element in doc.RootElement.EnumerateArray())
				{
					if (element.TryGetProperty("playerId", out var playerIdProp) &&
						Guid.TryParse(playerIdProp.GetString(), out var playerId))
					{
						winnerPlayerIds.Add(playerId);
					}
				}
			}
			catch (JsonException)
			{
				// If parsing fails, proceed with empty winners (all staying players match)
			}
		}

		// 5. Determine losers - staying players who are NOT winners
		var gamePlayersList = game.GamePlayers.ToList();
		var losers = gamePlayersList
			.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay &&
						 !winnerPlayerIds.Contains(gp.PlayerId))
			.ToList();

		// Calculate match amounts for each loser
		var matchAmounts = new Dictionary<string, int>();
		var totalMatched = 0;

		// In Kings and Lows, only losers match the pot amount (or go all-in)
		foreach (var loser in losers)
		{
			var matchAmount = Math.Min(currentPot, loser.ChipStack);
			if (matchAmount > 0)
			{
				matchAmounts[loser.Player?.Name ?? loser.PlayerId.ToString()] = matchAmount;
				loser.ChipStack -= matchAmount;
				totalMatched += matchAmount;
			}
		}

		// 5. Add matched amounts to new pot for next hand
		var newPot = new Data.Entities.Pot
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber + 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = totalMatched,
			IsAwarded = false,
			CreatedAt = now
		};

		context.Pots.Add(newPot);

		// 6. Mark current hand complete and move to next hand setup
		game.CurrentPhase = nameof(Phases.Complete);
		game.HandCompletedAt = now;
		game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);

		// Move dealer button to next occupied seat position (clockwise)
		MoveDealer(game);

		game.UpdatedAt = now;

		// 7. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		return new AcknowledgePotMatchSuccessful
		{
			GameId = game.Id,
			TotalMatched = totalMatched,
			NewPotAmount = totalMatched,
			NextPhase = game.CurrentPhase,
			MatchAmounts = matchAmounts
		};
	}

	/// <summary>
	/// Moves the dealer button to the next occupied seat position (clockwise).
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == Data.Entities.GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0)
		{
			return;
		}

		var currentPosition = game.DealerPosition;

		// Find next occupied seat clockwise from current position
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		if (seatsAfterCurrent.Count > 0)
		{
			game.DealerPosition = seatsAfterCurrent.First();
		}
		else
		{
			game.DealerPosition = occupiedSeats.First();
		}
	}
}
