using CardGames.Poker.Api.Betting;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
///     Handler for retrieving the current player's turn state in a specific Follow the Queen game.
/// </summary>
public class GetCurrentPlayerTurnQueryHandler(CardsDbContext context, IGamePlayerResolver gamePlayerResolver)
	: IRequestHandler<GetCurrentPlayerTurnQuery, GetCurrentPlayerTurnResponse?>
{
	public async Task<GetCurrentPlayerTurnResponse?> Handle(GetCurrentPlayerTurnQuery request,
		CancellationToken cancellationToken)
	{
		var game = await context.Games
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

		if (game is null || game.CurrentPlayerIndex < 0) return null;

		var gamePlayer = await context.GamePlayers
			.Where(gp => gp.GameId == request.GameId && gp.SeatPosition == game.CurrentPlayerIndex)
			.Include(gp => gp.Player)
			.Include(gp => gp.Cards)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (gamePlayer is null) return null;

		var caller = await gamePlayerResolver.ResolveSeatedPlayerAsync(request.GameId, cancellationToken);
		var callerIsActor = caller is not null && caller.Id == gamePlayer.Id;

		var playerResponse = gamePlayer.ToCurrentPlayerResponse();
		playerResponse = playerResponse with
		{
			Hand = MapHand(gamePlayer.Cards, game.CurrentHandNumber, callerIsActor)
		};

		// Get the current betting round to calculate available actions
		BettingRound? bettingRound;
		try
		{
			bettingRound = await context.BettingRounds
				.Where(br =>
					br.GameId == request.GameId && br.HandNumber == game.CurrentHandNumber && !br.IsComplete)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}

		var availableActions = CalculateAvailableActions(playerResponse, bettingRound);

		HandOddsResponse? handOdds = null;
		if (callerIsActor)
		{
			var deadCards = await KnownDeadCards.LoadAsync(
				context,
				request.GameId,
				game.CurrentHandNumber,
				cancellationToken);

			handOdds = HandOddsCalculationService.CalculateDrawOdds(
				gamePlayer.Cards,
				deadCards);
		}

		return new GetCurrentPlayerTurnResponse(playerResponse, availableActions, handOdds);
	}

	private static IReadOnlyList<DealtCard> MapHand(
		ICollection<GameCard> cards,
		int currentHandNumber,
		bool callerIsActor)
	{
		return cards
			.Where(c => !c.IsDiscarded
			            && c.HandNumber == currentHandNumber
			            && (callerIsActor || c.IsVisible))
			.OrderBy(c => c.DealOrder)
			.Select(c => new DealtCard
			{
				Suit = c.Suit,
				Symbol = c.Symbol,
				DealOrder = c.DealOrder
			})
			.ToList();
	}

	private static AvailableActionsResponse? CalculateAvailableActions(
		CurrentPlayerResponse player,
		BettingRound? bettingRound)
	{
		// No available actions if not in a betting phase
		if (bettingRound is null || bettingRound.IsComplete) return null;

		// No actions for folded or all-in players
		if (player.HasFolded || player.IsAllIn) return null;

		var currentBet = bettingRound.CurrentBet;
		var minBet = bettingRound.MinBet;
		var lastRaiseAmount = bettingRound.LastRaiseAmount > 0 ? bettingRound.LastRaiseAmount : minBet;
		var amountToCall = currentBet - player.CurrentBet;
		var canAffordCall = player.ChipStack >= amountToCall;

		var actions = new AvailableActionsResponse(
			currentBet == player.CurrentBet,
			currentBet == 0 && player.ChipStack >= minBet,
			currentBet > player.CurrentBet && canAffordCall && amountToCall < player.ChipStack,
			currentBet > 0 && player.ChipStack > amountToCall,
			currentBet > player.CurrentBet,
			player.ChipStack > 0,
			minBet,
			player.ChipStack,
			Math.Min(amountToCall, player.ChipStack),
			currentBet + lastRaiseAmount
		);

		if (FixedLimitStudBetting.Offer(currentBet, player.CurrentBet, player.ChipStack, minBet) is not { } offer)
			return actions;

		return actions with
		{
			CanBet = offer.CanBet,
			CanRaise = offer.CanRaise,
			CanAllIn = offer.CanAllIn,
			MinBet = offer.MinBet,
			MaxBet = offer.MaxBet,
			MinRaise = offer.MinRaise
		};
	}
}
