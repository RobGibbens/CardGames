using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Handler for retrieving the current player's turn state in a specific Follow the Queen game.
/// </summary>
public class GetCurrentPlayerTurnQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetCurrentPlayerTurnQuery, GetCurrentPlayerTurnResponse?>
{
	public async Task<GetCurrentPlayerTurnResponse?> Handle(GetCurrentPlayerTurnQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
			{
				var game = await context.Games
					.AsNoTracking()
					.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

				if (game is null || game.CurrentPlayerIndex < 0)
				{
					return null;
				}

				var gamePlayer = await context.GamePlayers
					.Where(gp => gp.GameId == request.GameId && gp.SeatPosition == game.CurrentPlayerIndex)
					.Include(gp => gp.Player)
					.Include(gp => gp.Cards)
					.AsNoTracking()
					.FirstOrDefaultAsync(cancellationToken);

				if (gamePlayer is null)
				{
					return null;
				}

				var playerResponse = gamePlayer.ToCurrentPlayerResponse();

					// Get the current betting round to calculate available actions
					BettingRound? bettingRound = null;
					try
					{
						bettingRound = await context.BettingRounds
							.Where(br => br.GameId == request.GameId && br.HandNumber == game.CurrentHandNumber && !br.IsComplete)
							.AsNoTracking()
							.FirstOrDefaultAsync(cancellationToken);
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
						throw;
					}

					var availableActions = CalculateAvailableActions(playerResponse, bettingRound);

					// Calculate hand odds
					// Get dead cards from folded players
					var deadCards = await context.GameCards
						.Where(gc => gc.GamePlayer.GameId == request.GameId 
							&& gc.GamePlayer.HasFolded 
							&& !gc.IsDiscarded)
						.AsNoTracking()
						.ToListAsync(cancellationToken);

					var handOdds = HandOddsCalculationService.CalculateDrawOdds(
						gamePlayer.Cards,
						deadCards);

					return new GetCurrentPlayerTurnResponse(playerResponse, availableActions, handOdds);
			},
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetCurrentPlayerTurnQuery)]
		);
	}

	private static AvailableActionsResponse? CalculateAvailableActions(
		CurrentPlayerResponse player,
		BettingRound? bettingRound)
	{
		// No available actions if not in a betting phase
		if (bettingRound is null || bettingRound.IsComplete)
		{
			return null;
		}

		// No actions for folded or all-in players
		if (player.HasFolded || player.IsAllIn)
		{
			return null;
		}

		var currentBet = bettingRound.CurrentBet;
		var minBet = bettingRound.MinBet;
		var lastRaiseAmount = bettingRound.LastRaiseAmount > 0 ? bettingRound.LastRaiseAmount : minBet;
		var amountToCall = currentBet - player.CurrentBet;
		var canAffordCall = player.ChipStack >= amountToCall;

		return new AvailableActionsResponse(
			CanCheck: currentBet == player.CurrentBet,
			CanBet: currentBet == 0 && player.ChipStack >= minBet,
			CanCall: currentBet > player.CurrentBet && canAffordCall && amountToCall < player.ChipStack,
			CanRaise: currentBet > 0 && player.ChipStack > amountToCall,
			CanFold: currentBet > player.CurrentBet,
			CanAllIn: player.ChipStack > 0,
			MinBet: minBet,
			MaxBet: player.ChipStack,
			CallAmount: Math.Min(amountToCall, player.ChipStack),
			MinRaise: currentBet + lastRaiseAmount
		);
	}
}
