using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

/// <summary>
/// Handles the <see cref="KeepOrTradeCommand"/> to process a player's keep or trade decision.
/// </summary>
public class KeepOrTradeCommandHandler(
	CardsDbContext context,
	IGameFlowHandlerFactory flowHandlerFactory,
	IHandHistoryRecorder handHistoryRecorder,
	IHandSettlementService handSettlementService)
	: IRequestHandler<KeepOrTradeCommand, OneOf<KeepOrTradeSuccessful, KeepOrTradeError>>
{
	public async Task<OneOf<KeepOrTradeSuccessful, KeepOrTradeError>> Handle(
		KeepOrTradeCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with players, cards
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new KeepOrTradeError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = KeepOrTradeErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in KeepOrTrade phase
		if (!string.Equals(game.CurrentPhase, nameof(Phases.KeepOrTrade), StringComparison.OrdinalIgnoreCase))
		{
			return new KeepOrTradeError
			{
				Message = $"Cannot make keep/trade decision. Game is in '{game.CurrentPhase}' phase.",
				Code = KeepOrTradeErrorCode.InvalidPhase
			};
		}

		// 3. Find the player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new KeepOrTradeError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = KeepOrTradeErrorCode.PlayerNotFound
			};
		}

		// 4. Validate it's this player's turn
		if (gamePlayer.SeatPosition != game.CurrentPlayerIndex)
		{
			return new KeepOrTradeError
			{
				Message = "It's not this player's turn.",
				Code = KeepOrTradeErrorCode.NotPlayersTurn
			};
		}

		// 5. Parse decision
		if (!string.Equals(command.Decision, "Keep", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(command.Decision, "Trade", StringComparison.OrdinalIgnoreCase))
		{
			return new KeepOrTradeError
			{
				Message = $"Invalid decision '{command.Decision}'. Must be 'Keep' or 'Trade'.",
				Code = KeepOrTradeErrorCode.InvalidDecision
			};
		}

		var isKeep = string.Equals(command.Decision, "Keep", StringComparison.OrdinalIgnoreCase);
		var didTrade = false;
		var wasBlocked = false;

		// Persist player's choice for public seat display during this hand.
		gamePlayer.VariantState = isKeep ? "SYN_KEPT" : "SYN_TRADED";

		if (!isKeep)
		{
			// Attempt to trade
			var eligiblePlayers = GetEligiblePlayersInOrder(game);
			var isDealer = gamePlayer.SeatPosition == game.DealerPosition;

			if (isDealer)
			{
				// Dealer trades with the deck
				didTrade = await TradeWithDeckAsync(game, gamePlayer, now, cancellationToken);
			}
			else
			{
				// Trade with the next player to the left
				var tradeTarget = FindTradeTarget(game, gamePlayer, eligiblePlayers);
				if (tradeTarget is null || ScrewYourNeighborFlowHandler.IsKing(tradeTarget.Value.Card.Symbol))
				{
					// Blocked by King — can't trade
					wasBlocked = true;
				}
				else
				{
					await TradeCardsAsync(game, gamePlayer, tradeTarget.Value.Player, now, cancellationToken);
					didTrade = true;
				}
			}
		}

		// 6. Advance to next player or next phase
		var (nextPhase, nextPlayerSeat) = AdvanceToNextPlayerOrPhase(game, gamePlayer);

		if (string.Equals(nextPhase, nameof(Phases.Reveal), StringComparison.OrdinalIgnoreCase))
		{
			// The final SYN decision owns showdown resolution server-side.
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			game.UpdatedAt = now;

			var flowHandler = flowHandlerFactory.GetHandler(PokerGameMetadataRegistry.ScrewYourNeighborCode);
			var showdownResult = await flowHandler.PerformShowdownAsync(
				context,
				game,
				handHistoryRecorder,
				now,
				cancellationToken);

			if (!showdownResult.IsSuccess)
			{
				return new KeepOrTradeError
				{
					Message = showdownResult.ErrorMessage ?? "Failed to resolve Screw Your Neighbor showdown.",
					Code = KeepOrTradeErrorCode.InvalidPhase
				};
			}

			var postShowdownPhase = await flowHandler.ProcessPostShowdownAsync(
				context,
				game,
				showdownResult,
				now,
				cancellationToken);

			game.CurrentPhase = postShowdownPhase;
			game.CurrentPlayerIndex = -1;
			game.UpdatedAt = now;

			await handSettlementService.SettleHandAsync(
				game,
				BuildSettlementPayouts(game, showdownResult),
				cancellationToken);
		}
		else
		{
			game.CurrentPhase = nextPhase;
			game.CurrentPlayerIndex = nextPlayerSeat;
			game.UpdatedAt = now;
		}

		await context.SaveChangesAsync(cancellationToken);

		return new KeepOrTradeSuccessful
		{
			GameId = game.Id,
			PlayerId = command.PlayerId,
			Decision = command.Decision,
			DidTrade = didTrade,
			WasBlocked = wasBlocked,
			NextPhase = game.CurrentPhase,
			NextPlayerSeatIndex = game.CurrentPlayerIndex >= 0 ? game.CurrentPlayerIndex : null
		};
	}

	private static Dictionary<string, int> BuildSettlementPayouts(Game game, ShowdownResult showdownResult)
	{
		var settlementPayouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		if (game.Status == GameStatus.Completed &&
		    showdownResult.WinnerPlayerIds.Count == 1 &&
		    showdownResult.TotalPotAwarded > 0)
		{
			var winner = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == showdownResult.WinnerPlayerIds[0]);
			if (winner is not null)
			{
				settlementPayouts[winner.Player.Name] = showdownResult.TotalPotAwarded + winner.TotalContributedThisHand;
			}
		}

		return settlementPayouts;
	}

	/// <summary>
	/// Gets eligible (active, not sitting out) players ordered by seat position.
	/// </summary>
	private static List<GamePlayer> GetEligiblePlayersInOrder(Game game)
	{
		return game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();
	}

	/// <summary>
	/// Finds the trade target (next eligible player to the left of the current player).
	/// Returns null if no valid target exists.
	/// </summary>
	private static (GamePlayer Player, GameCard Card)? FindTradeTarget(
		Game game, GamePlayer currentPlayer, List<GamePlayer> eligiblePlayers)
	{
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;
		var searchSeat = (currentPlayer.SeatPosition + 1) % totalSeats;

		for (var i = 0; i < totalSeats; i++)
		{
			var target = eligiblePlayers.FirstOrDefault(p => p.SeatPosition == searchSeat);
			if (target is not null && target.Id != currentPlayer.Id)
			{
				var targetCard = game.GameCards
					.FirstOrDefault(gc => gc.GamePlayerId == target.Id &&
					                      gc.HandNumber == game.CurrentHandNumber &&
					                      gc.Location == CardLocation.Hand &&
					                      !gc.IsDiscarded);
				if (targetCard is not null)
				{
					return (target, targetCard);
				}
			}

			searchSeat = (searchSeat + 1) % totalSeats;
		}

		return null;
	}

	/// <summary>
	/// Swaps cards between two players.
	/// </summary>
	private static Task TradeCardsAsync(
		Game game, GamePlayer playerA, GamePlayer playerB,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		var cardA = game.GameCards
			.First(gc => gc.GamePlayerId == playerA.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Hand &&
			             !gc.IsDiscarded);

		var cardB = game.GameCards
			.First(gc => gc.GamePlayerId == playerB.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Hand &&
			             !gc.IsDiscarded);

		// Swap ownership
		cardA.GamePlayerId = playerB.Id;
		cardB.GamePlayerId = playerA.Id;

		return Task.CompletedTask;
	}

	/// <summary>
	/// Dealer trades their card with the top card of the deck.
	/// </summary>
	private async Task<bool> TradeWithDeckAsync(
		Game game, GamePlayer dealer,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		var dealerCard = game.GameCards
			.FirstOrDefault(gc => gc.GamePlayerId == dealer.Id &&
			                      gc.HandNumber == game.CurrentHandNumber &&
			                      gc.Location == CardLocation.Hand &&
			                      !gc.IsDiscarded);

		if (dealerCard is null) return false;

		// Find the next undealt card in the deck
		var topDeckCard = game.GameCards
			.Where(gc => gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Deck &&
			             gc.GamePlayerId == null)
			.OrderBy(gc => gc.DealOrder)
			.FirstOrDefault();

		if (topDeckCard is null) return false;

		// Discard dealer's card
		dealerCard.Location = CardLocation.Discarded;
		dealerCard.IsDiscarded = true;
		dealerCard.GamePlayerId = null;

		// Give deck card to dealer
		topDeckCard.GamePlayerId = dealer.Id;
		topDeckCard.Location = CardLocation.Hand;
		topDeckCard.DealtAt = now;
		topDeckCard.IsVisible = ScrewYourNeighborFlowHandler.IsKing(topDeckCard.Symbol);

		return true;
	}

	/// <summary>
	/// Advances to the next player in the KeepOrTrade phase, or transitions to Reveal
	/// if all players have acted. Auto-skips players who have Kings or whose trade target
	/// has a King (except the dealer, who can always choose to keep).
	/// </summary>
	private static (string NextPhase, int NextPlayerSeat) AdvanceToNextPlayerOrPhase(
		Game game, GamePlayer currentPlayer)
	{
		var eligiblePlayers = GetEligiblePlayersInOrderStatic(game);
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		// Find the next player after the current one
		var currentSeat = currentPlayer.SeatPosition;
		var searchSeat = (currentSeat + 1) % totalSeats;

		for (var i = 0; i < totalSeats; i++)
		{
			var nextPlayer = eligiblePlayers.FirstOrDefault(p => p.SeatPosition == searchSeat);
			if (nextPlayer is not null && nextPlayer.Id != currentPlayer.Id)
			{
				// Check if this player has already acted (everyone before current player in rotation has acted)
				// In SYN, each player acts exactly once, moving from left of dealer clockwise to dealer
				// We need to check if we've gone all the way around back to the first player
				if (HasPlayerAlreadyActed(game, nextPlayer, currentPlayer))
				{
					// All players have acted — transition to Reveal
					return (nameof(Phases.Reveal), -1);
				}

				// Check if this player can be auto-skipped
				var nextPlayerCard = game.GameCards
					.FirstOrDefault(gc => gc.GamePlayerId == nextPlayer.Id &&
					                      gc.HandNumber == game.CurrentHandNumber &&
					                      gc.Location == CardLocation.Hand &&
					                      !gc.IsDiscarded);

				if (nextPlayerCard is not null && ScrewYourNeighborFlowHandler.IsKing(nextPlayerCard.Symbol))
				{
					// Player has a King — auto-skip
					searchSeat = (searchSeat + 1) % totalSeats;
					continue;
				}

				var isDealer = nextPlayer.SeatPosition == game.DealerPosition;
				if (!isDealer)
				{
					// Check if trade target has King (would block trade)
					var tradeTarget = FindTradeTargetStatic(game, nextPlayer, eligiblePlayers);
					if (tradeTarget is not null && ScrewYourNeighborFlowHandler.IsKing(tradeTarget.Value.Card.Symbol))
					{
						// Trade target has King — auto-skip (player is stuck with their card)
						searchSeat = (searchSeat + 1) % totalSeats;
						continue;
					}
				}

				// This player needs to make a decision
				return (nameof(Phases.KeepOrTrade), nextPlayer.SeatPosition);
			}

			searchSeat = (searchSeat + 1) % totalSeats;
		}

		// If we got here, no more players to act — transition to Reveal
		return (nameof(Phases.Reveal), -1);
	}

	/// <summary>
	/// Determines if a player has already acted in this round.
	/// Players act in order from left of dealer, ending with the dealer.
	/// Once we wrap around to someone who should have already acted, all have acted.
	/// </summary>
	private static bool HasPlayerAlreadyActed(Game game, GamePlayer candidatePlayer, GamePlayer justActedPlayer)
	{
		var eligiblePlayers = GetEligiblePlayersInOrderStatic(game);
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		// The first player to act is left of dealer
		var dealerPosition = game.DealerPosition;
		var firstActorSeat = -1;

		// Find first eligible player after dealer
		var search = (dealerPosition + 1) % totalSeats;
		for (var i = 0; i < totalSeats; i++)
		{
			if (eligiblePlayers.Any(p => p.SeatPosition == search))
			{
				firstActorSeat = search;
				break;
			}

			search = (search + 1) % totalSeats;
		}

		if (firstActorSeat == -1) return true;

		// If the candidate is the first actor, and someone else just acted, then we've gone around
		if (candidatePlayer.SeatPosition == firstActorSeat && justActedPlayer.SeatPosition != firstActorSeat)
		{
			return true;
		}

		// Check if candidate comes before justActed in the rotation order (meaning they already acted)
		var firstActorRelative = 0;
		var candidateRelative = (candidatePlayer.SeatPosition - firstActorSeat + totalSeats) % totalSeats;
		var justActedRelative = (justActedPlayer.SeatPosition - firstActorSeat + totalSeats) % totalSeats;

		// candidatePlayer already acted if their relative position is <= justActedPlayer's relative position
		return candidateRelative <= justActedRelative;
	}

	private static List<GamePlayer> GetEligiblePlayersInOrderStatic(Game game)
	{
		return game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();
	}

	private static (GamePlayer Player, GameCard Card)? FindTradeTargetStatic(
		Game game, GamePlayer currentPlayer, List<GamePlayer> eligiblePlayers)
	{
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;
		var searchSeat = (currentPlayer.SeatPosition + 1) % totalSeats;

		for (var i = 0; i < totalSeats; i++)
		{
			var target = eligiblePlayers.FirstOrDefault(p => p.SeatPosition == searchSeat);
			if (target is not null && target.Id != currentPlayer.Id)
			{
				var targetCard = game.GameCards
					.FirstOrDefault(gc => gc.GamePlayerId == target.Id &&
					                      gc.HandNumber == game.CurrentHandNumber &&
					                      gc.Location == CardLocation.Hand &&
					                      !gc.IsDiscarded);
				if (targetCard is not null)
				{
					return (target, targetCard);
				}
			}

			searchSeat = (searchSeat + 1) % totalSeats;
		}

		return null;
	}
}
