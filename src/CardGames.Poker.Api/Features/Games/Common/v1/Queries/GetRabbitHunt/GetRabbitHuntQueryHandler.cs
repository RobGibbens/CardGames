using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;
using ContractCardSuit = CardGames.Poker.Api.Contracts.CardSuit;
using ContractCardSymbol = CardGames.Poker.Api.Contracts.CardSymbol;
using GamePlayerStatus = CardGames.Poker.Api.Data.Entities.GamePlayerStatus;
using GameStatus = CardGames.Poker.Api.Data.Entities.GameStatus;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

public sealed class GetRabbitHuntQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetRabbitHuntQuery, OneOf<GetRabbitHuntSuccessful, GetRabbitHuntError>>
{
	public async Task<OneOf<GetRabbitHuntSuccessful, GetRabbitHuntError>> Handle(GetRabbitHuntQuery request,
		CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated)
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.NotAuthenticated, "User not authenticated.");

		var game = await context.Games
			.Include(g => g.GameType)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

		if (game is null)
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.GameNotFound,
				$"Game with ID '{request.GameId}' was not found.");

		var gameTypeCode = game.GameType?.Code;
		if (!RabbitHuntBoardProjector.SupportsRabbitHunt(gameTypeCode))
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.UnsupportedGameType,
				"Rabbit Hunt is only available for supported community-card variants.");

		var handIsOver = string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
		                 || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase)
		                 || game.Status == GameStatus.Completed;

		if (!handIsOver)
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.RabbitHuntNotAvailable,
				"Rabbit Hunt is only available after the hand is over.");

		var userEmail = currentUserService.UserEmail;
		var userName = currentUserService.UserName;
		var userId = currentUserService.UserId;

		var requestingGamePlayer = await context.GamePlayers
			.AsNoTracking()
			.Where(gp => gp.GameId == request.GameId && gp.Status != GamePlayerStatus.Left)
			.Include(gp => gp.Player)
			.FirstOrDefaultAsync(gp =>
					(!string.IsNullOrWhiteSpace(userEmail) && gp.Player.Email == userEmail) ||
					(!string.IsNullOrWhiteSpace(userName) && gp.Player.Name == userName) ||
					(!string.IsNullOrWhiteSpace(userId) && gp.Player.ExternalId == userId),
				cancellationToken);

		if (requestingGamePlayer is null)
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.NotSeated,
				"Only seated players can request Rabbit Hunt for this hand.");

		var currentCommunityCards = await context.GameCards
			.Where(card => card.GameId == request.GameId
			               && card.HandNumber == game.CurrentHandNumber
			               && !card.IsDiscarded
			               && card.Location == CardLocation.Community
			               && card.GamePlayerId == null)
			.OrderBy(card => card.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		var remainingDeckCards = await context.GameCards
			.Where(card => card.GameId == request.GameId
			               && card.HandNumber == game.CurrentHandNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		var projectedBoard =
			RabbitHuntBoardProjector.ProjectBoard(gameTypeCode!, currentCommunityCards, remainingDeckCards);
		var newlyRevealedCards = projectedBoard.Where(card => !card.WasAlreadyVisible).ToList();

		if (newlyRevealedCards.Count == 0)
			return new GetRabbitHuntError(GetRabbitHuntErrorCode.RabbitHuntNotAvailable,
				"All community cards were already dealt and shown for this hand.");

		var selectedShowcaseDealOrder = string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode,
			StringComparison.OrdinalIgnoreCase)
			? BobBarkerVariantState.GetSelectedShowcaseDealOrder(requestingGamePlayer)
			: null;

		var playerCards = await context.GameCards
			.Where(card => card.GameId == request.GameId
			               && card.HandNumber == game.CurrentHandNumber
			               && card.GamePlayerId == requestingGamePlayer.Id
			               && !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		var projectedPlayerCards = playerCards
			.Where(card => selectedShowcaseDealOrder is null || card.DealOrder != selectedShowcaseDealOrder.Value)
			.Select(card => new Card((Suit)card.Suit, (Symbol)card.Symbol))
			.ToList();

		var projectedPlayerCardDtos = playerCards
			.Where(card => selectedShowcaseDealOrder is null || card.DealOrder != selectedShowcaseDealOrder.Value)
			.Select(ToShowdownCard)
			.ToList();

		var projectedBoardCards = projectedBoard
			.Select(card => ToCoreCard(card.Card))
			.ToList();

		var projectedKlondikeCard = projectedBoard
			.Where(card => card.IsKlondikeCard)
			.Select(card => ToCoreCard(card.Card))
			.FirstOrDefault();

		var projectedHandEvaluationDescription = CommunityHandDescriptionEvaluator.Evaluate(
			gameTypeCode,
			projectedPlayerCards,
			projectedBoardCards,
			projectedKlondikeCard);

		return new GetRabbitHuntSuccessful
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			GameTypeCode = gameTypeCode!,
			PlayerCards = projectedPlayerCardDtos,
			CommunityCards = projectedBoard,
			PreviouslyVisibleCards = projectedBoard.Where(card => card.WasAlreadyVisible).ToList(),
			NewlyRevealedCards = newlyRevealedCards,
			ProjectedHandEvaluationDescription = projectedHandEvaluationDescription
		};
	}

	private static Card ToCoreCard(ShowdownCard card)
	{
		return new Card(MapSuit(card.Suit), MapSymbol(card.Symbol));
	}

	private static ShowdownCard ToShowdownCard(GameCard card)
	{
		return new ShowdownCard(MapSuit(card.Suit), MapSymbol(card.Symbol));
	}

	private static ContractCardSuit MapSuit(CardSuit suit)
	{
		return suit switch
		{
			CardSuit.Hearts => ContractCardSuit.Hearts,
			CardSuit.Diamonds => ContractCardSuit.Diamonds,
			CardSuit.Spades => ContractCardSuit.Spades,
			CardSuit.Clubs => ContractCardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown card suit")
		};
	}

	private static ContractCardSymbol MapSymbol(CardSymbol symbol)
	{
		return symbol switch
		{
			CardSymbol.Deuce => ContractCardSymbol.Deuce,
			CardSymbol.Three => ContractCardSymbol.Three,
			CardSymbol.Four => ContractCardSymbol.Four,
			CardSymbol.Five => ContractCardSymbol.Five,
			CardSymbol.Six => ContractCardSymbol.Six,
			CardSymbol.Seven => ContractCardSymbol.Seven,
			CardSymbol.Eight => ContractCardSymbol.Eight,
			CardSymbol.Nine => ContractCardSymbol.Nine,
			CardSymbol.Ten => ContractCardSymbol.Ten,
			CardSymbol.Jack => ContractCardSymbol.Jack,
			CardSymbol.Queen => ContractCardSymbol.Queen,
			CardSymbol.King => ContractCardSymbol.King,
			CardSymbol.Ace => ContractCardSymbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown card symbol")
		};
	}

	private static Suit MapSuit(ContractCardSuit? suit)
	{
		return suit switch
		{
			ContractCardSuit.Hearts => Suit.Hearts,
			ContractCardSuit.Diamonds => Suit.Diamonds,
			ContractCardSuit.Spades => Suit.Spades,
			ContractCardSuit.Clubs => Suit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown card suit")
		};
	}

	private static Symbol MapSymbol(ContractCardSymbol? symbol)
	{
		return symbol switch
		{
			ContractCardSymbol.Deuce => Symbol.Deuce,
			ContractCardSymbol.Three => Symbol.Three,
			ContractCardSymbol.Four => Symbol.Four,
			ContractCardSymbol.Five => Symbol.Five,
			ContractCardSymbol.Six => Symbol.Six,
			ContractCardSymbol.Seven => Symbol.Seven,
			ContractCardSymbol.Eight => Symbol.Eight,
			ContractCardSymbol.Nine => Symbol.Nine,
			ContractCardSymbol.Ten => Symbol.Ten,
			ContractCardSymbol.Jack => Symbol.Jack,
			ContractCardSymbol.Queen => Symbol.Queen,
			ContractCardSymbol.King => Symbol.King,
			ContractCardSymbol.Ace => Symbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown card symbol")
		};
	}
}