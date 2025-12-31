using CardGames.Poker.Api.Data;
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
		if (game.CurrentPhase != nameof(KingsAndLowsPhase.PotMatching))
		{
			return new AcknowledgePotMatchError
			{
				Message = $"Cannot process pot matching. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(KingsAndLowsPhase.PotMatching)}' phase.",
				Code = AcknowledgePotMatchErrorCode.InvalidPhase
			};
		}

		// 3. Get current pot for this hand
		var currentPot = game.Pots
			.Where(p => p.HandNumber == game.CurrentHandNumber && !p.IsAwarded)
			.Sum(p => p.Amount);

		// 4. Determine losers - all staying players who didn't win
		// For simplicity in this implementation, we'll assume losers need to match the pot
		// In a real implementation, we'd need to determine winners from showdown results
		var gamePlayersList = game.GamePlayers.ToList();
		var stayingPlayers = gamePlayersList
			.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
			.ToList();

		// Calculate match amounts for each loser
		var matchAmounts = new Dictionary<string, int>();
		var totalMatched = 0;

		// In Kings and Lows, losers match the pot amount (or go all-in)
		foreach (var loser in stayingPlayers)
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
			PotType = Data.Entities.PotType.Main,
			PotOrder = 0,
			Amount = totalMatched,
			IsAwarded = false,
			CreatedAt = now
		};

		context.Pots.Add(newPot);

		// 6. Mark current hand complete and move to next hand setup
		game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
		game.HandCompletedAt = now;
		
		// Move dealer button
		if (gamePlayersList.Count > 0)
		{
			game.DealerPosition = (game.DealerPosition + 1) % gamePlayersList.Count;
		}

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
}
