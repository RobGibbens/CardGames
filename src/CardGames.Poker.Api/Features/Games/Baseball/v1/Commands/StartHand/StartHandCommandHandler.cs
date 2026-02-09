using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;
using Suit = CardGames.Core.French.Cards.Suit;
using Symbol = CardGames.Core.French.Cards.Symbol;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;

public class StartHandCommandHandler(
	CardsDbContext context,
	ILogger<StartHandCommandHandler> logger)
	: IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
	public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
		StartHandCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new StartHandError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = StartHandErrorCode.GameNotFound
			};
		}

		var validPhases = new[]
		{
			nameof(Phases.WaitingToStart),
			nameof(Phases.Complete)
		};

		if (!validPhases.Contains(game.CurrentPhase))
		{
			return new StartHandError
			{
				Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase. " +
						  $"A new hand can only be started when the game is in '{nameof(Phases.WaitingToStart)}' " +
						  $"or '{nameof(Phases.Complete)}' phase.",
				Code = StartHandErrorCode.InvalidGameState
			};
		}

		var playersWithPendingChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			player.ChipStack += player.PendingChipsToAdd;
			player.PendingChipsToAdd = 0;
		}

		var ante = game.Ante ?? 0;
		var playersWithInsufficientChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 (gp.ChipStack <= 0 || (ante > 0 && gp.ChipStack < ante)))
			.ToList();

		foreach (var player in playersWithInsufficientChips)
		{
			player.IsSittingOut = true;
		}

		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 (ante == 0 || gp.ChipStack >= ante))
			.ToList();

		if (eligiblePlayers.Count < 2)
		{
			return new StartHandError
			{
				Message = "Not enough eligible players to start a new hand. At least 2 players with sufficient chips are required.",
				Code = StartHandErrorCode.NotEnoughPlayers
			};
		}

		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.HasFolded = gamePlayer.IsSittingOut;
		}

		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			logger.LogInformation(
				"StartHand: Removing {Count} existing cards from previous hands for game {GameId}",
				existingCards.Count, game.Id);
			context.GameCards.RemoveRange(existingCards);
		}

		var dealer = FrenchDeckDealer.WithFullDeck();
		dealer.Shuffle();

		var newHandNumber = game.CurrentHandNumber + 1;
		var deckCards = new List<GameCard>();
		var dealOrder = 1;

		var shuffledCards = dealer.DealCards(52);
		foreach (var card in shuffledCards)
		{
			var gameCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = newHandNumber,
				Suit = MapSuit(card.Suit),
				Symbol = MapSymbol(card.Symbol),
				Location = CardLocation.Deck,
				DealOrder = dealOrder++,
				DealtAtPhase = null,
				IsVisible = false,
				IsWild = false,
				IsDiscarded = false,
				IsDrawnCard = false,
				IsBuyCard = false,
				DealtAt = now
			};
			deckCards.Add(gameCard);
		}

		context.GameCards.AddRange(deckCards);

		var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);
		BaseballGameSettings.SaveState(game, buyCardState with
		{
			PendingOffers = [],
			ReturnPhase = null,
			ReturnActorIndex = null
		});

		var mainPot = new Pot
		{
			GameId = game.Id,
			HandNumber = newHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			IsAwarded = false,
			CreatedAt = now
		};

		context.Pots.Add(mainPot);

		game.CurrentHandNumber = newHandNumber;
		game.CurrentPhase = nameof(Phases.CollectingAntes);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;
		game.StartedAt ??= now;

		await context.SaveChangesAsync(cancellationToken);

		return new StartHandSuccessful
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			CurrentPhase = game.CurrentPhase,
			ActivePlayerCount = eligiblePlayers.Count
		};
	}

	private static CardSuit MapSuit(Suit suit) => suit switch
	{
		Suit.Hearts => CardSuit.Hearts,
		Suit.Diamonds => CardSuit.Diamonds,
		Suit.Spades => CardSuit.Spades,
		Suit.Clubs => CardSuit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit))
	};

	private static CardSymbol MapSymbol(Symbol symbol) => symbol switch
	{
		Symbol.Deuce => CardSymbol.Deuce,
		Symbol.Three => CardSymbol.Three,
		Symbol.Four => CardSymbol.Four,
		Symbol.Five => CardSymbol.Five,
		Symbol.Six => CardSymbol.Six,
		Symbol.Seven => CardSymbol.Seven,
		Symbol.Eight => CardSymbol.Eight,
		Symbol.Nine => CardSymbol.Nine,
		Symbol.Ten => CardSymbol.Ten,
		Symbol.Jack => CardSymbol.Jack,
		Symbol.Queen => CardSymbol.Queen,
		Symbol.King => CardSymbol.King,
		Symbol.Ace => CardSymbol.Ace,
		_ => throw new ArgumentOutOfRangeException(nameof(symbol))
	};
}
