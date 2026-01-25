using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;
using Suit = CardGames.Core.French.Cards.Suit;
using Symbol = CardGames.Core.French.Cards.Symbol;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.StartHand;

/// <summary>
/// Handles the <see cref="StartHandCommand"/> to start a new hand in a Seven Card Stud game.
/// </summary>
public class StartHandCommandHandler(CardsDbContext context)
	: IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
	public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
		StartHandCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
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

		// 2. Validate game state allows starting a new hand
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

		// 3. Apply pending chips to player stacks
		var playersWithPendingChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			player.ChipStack += player.PendingChipsToAdd;
			player.PendingChipsToAdd = 0;
		}

		// 4. Auto-sit-out players with insufficient chips for the ante
		var ante = game.Ante ?? 0;
		var playersWithInsufficientChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 gp.ChipStack > 0 &&
						 gp.ChipStack < ante)
			.ToList();

		foreach (var player in playersWithInsufficientChips)
		{
			player.IsSittingOut = true;
		}

		// 4. Get eligible players (active, not sitting out, chips >= ante or ante is 0)
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

		// 5. Reset player states for new hand (mirrors SevenCardStudGame.StartHand)
		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.HasFolded = gamePlayer.IsSittingOut;
		}

		// 6. Remove any existing cards from previous hand
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			context.GameCards.RemoveRange(existingCards);
		}

		// 7. Create and persist shuffled deck for this hand
		var dealer = FrenchDeckDealer.WithFullDeck();
		dealer.Shuffle();

		var newHandNumber = game.CurrentHandNumber + 1;
		var deckCards = new List<GameCard>();
		var dealOrder = 1;

		// Deal all 52 cards from the shuffled deck to persist their order
		var shuffledCards = dealer.DealCards(52);
		foreach (var card in shuffledCards)
		{
			var gameCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null, // Deck cards have no owner
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

		// 8. Create a new main pot for this hand
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

		// 9. Update game state
		game.CurrentHandNumber = newHandNumber;
		game.CurrentPhase = nameof(Phases.CollectingAntes);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

				// Set StartedAt only on first hand
				game.StartedAt ??= now;

				// 10. Persist changes
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
