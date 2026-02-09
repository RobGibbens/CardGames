using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

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

				BettingRound? bettingRound = null;
				bettingRound = await context.BettingRounds
					.Where(br => br.GameId == request.GameId && br.HandNumber == game.CurrentHandNumber && !br.IsComplete)
					.AsNoTracking()
					.FirstOrDefaultAsync(cancellationToken);

				var availableActions = CalculateAvailableActions(playerResponse, bettingRound);

				var deadCards = await context.GameCards
					.Where(gc => gc.GamePlayer.GameId == request.GameId
						&& gc.GamePlayer.HasFolded
						&& !gc.IsDiscarded)
					.AsNoTracking()
					.ToListAsync(cancellationToken);

				var handOdds = HandOddsCalculationService.CalculateBaseballOdds(
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
		if (bettingRound is null || bettingRound.IsComplete)
		{
			return null;
		}

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
