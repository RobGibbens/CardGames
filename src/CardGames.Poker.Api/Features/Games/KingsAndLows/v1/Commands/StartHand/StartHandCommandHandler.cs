using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

/// <summary>
/// Handles the <see cref="StartHandCommand"/> to start a new hand in a Kings and Lows game.
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

		// 3. Auto-sit-out players with insufficient chips for the ante
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

		// 5. Reset player states for new hand
		foreach (var gamePlayer in eligiblePlayers)
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.HasFolded = false;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.DropOrStayDecision = null;
		}

		// 6. Remove any existing cards from previous hand
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			context.GameCards.RemoveRange(existingCards);
		}

		// 7. Determine if this is the first hand (antes only collected on first hand in Kings and Lows)
		var isFirstHand = game.CurrentHandNumber == 0;

		// 8. Create a new main pot for this hand only if it's the first hand
		// For subsequent hands, the pot is created by AcknowledgePotMatchCommandHandler
		Pot? mainPot = null;
		if (isFirstHand)
		{
			mainPot = new Pot
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber + 1,
				PotType = PotType.Main,
				PotOrder = 0,
				Amount = 0,
				IsAwarded = false,
				CreatedAt = now
			};

			context.Pots.Add(mainPot);
		}

		// 9. Update game state - Kings and Lows starts with CollectingAntes (first hand) or Dealing (subsequent hands)
		game.CurrentHandNumber++;
		game.CurrentPhase = isFirstHand
			? nameof(Phases.CollectingAntes)
			: nameof(Phases.Dealing);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

		// Set StartedAt only on first hand
		game.StartedAt ??= now;

		// 10. Automatically collect antes only on first hand (Kings and Lows rule)
		if (isFirstHand && ante > 0 && mainPot is not null)
		{
			foreach (var player in eligiblePlayers)
			{
				var anteAmount = Math.Min(ante, player.ChipStack); //TODO:ROB - Don't let them play if they don't have enough
				player.ChipStack -= anteAmount;
				player.CurrentBet = anteAmount;
				player.TotalContributedThisHand = anteAmount;

				mainPot.Amount += anteAmount;

				var contribution = new PotContribution
				{
					PotId = mainPot.Id,
					GamePlayerId = player.Id,
					Amount = anteAmount,
					ContributedAt = now
				};
				context.Set<PotContribution>().Add(contribution);

				if (player.ChipStack == 0)
				{
					player.IsAllIn = true;
				}
			}
		}

		// 11. Automatically deal hands - move to Dealing phase then DropOrStay
		game.CurrentPhase = nameof(Phases.Dealing);
		
		// Create a standard deck of 52 cards with shuffled order
		var deck = new List<GameCard>();
		int deckOrder = 0;
		foreach (var suit in Enum.GetValues<CardSuit>())
		{
			foreach (var symbol in Enum.GetValues<CardSymbol>())
			{
				deck.Add(new GameCard
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					Suit = suit,
					Symbol = symbol,
					Location = CardLocation.Deck,
					DealOrder = deckOrder++,
					IsVisible = false,
					DealtAt = now
				});
			}
		}

		// Shuffle the deck using Fisher-Yates algorithm
		var random = new Random();
		for (int i = deck.Count - 1; i > 0; i--)
		{
			int j = random.Next(i + 1);
			(deck[i], deck[j]) = (deck[j], deck[i]);
		}

		// Update deck order after shuffle
		for (int i = 0; i < deck.Count; i++)
		{
			deck[i].DealOrder = i;
		}

		// Add all 52 cards to the context (including those that will remain in the deck)
		foreach (var card in deck)
		{
			context.GameCards.Add(card);
		}

				// Deal 5 cards to each eligible player from the shuffled deck
				int cardIndex = 0;
				for (int round = 0; round < 5; round++)
				{
					foreach (var player in eligiblePlayers.OrderBy(p => p.SeatPosition))
					{
						if (cardIndex < deck.Count)
						{
							var card = deck[cardIndex++];
							card.Location = CardLocation.Hand;
							card.GamePlayerId = player.Id;
							card.IsVisible = true; // Cards visible to the player in their hand
							card.DealtAtPhase = nameof(Phases.Dealing);
						}
					}
				}

				// Sort each player's cards by value (descending) and assign DealOrder for display
				foreach (var player in eligiblePlayers)
				{
					var playerCards = deck
						.Where(c => c.GamePlayerId == player.Id)
						.OrderByDescending(c => GetCardSortValue(c.Symbol))
						.ThenBy(c => GetSuitSortValue(c.Suit))
						.ToList();

					var dealOrder = 1;
					foreach (var card in playerCards)
					{
						card.DealOrder = dealOrder++;
					}
				}

				// 11. Move to DropOrStay phase - this is where players make their decision
				game.CurrentPhase = nameof(Phases.DropOrStay);

				// 12. Persist changes
				await context.SaveChangesAsync(cancellationToken);

				return new StartHandSuccessful
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					CurrentPhase = game.CurrentPhase,
					ActivePlayerCount = eligiblePlayers.Count
				};
			}

			/// <summary>
			/// Gets the numeric sort value for a card symbol (Ace high = 14).
			/// </summary>
			private static int GetCardSortValue(CardSymbol symbol) => symbol switch
			{
				CardSymbol.Deuce => 2,
				CardSymbol.Three => 3,
				CardSymbol.Four => 4,
				CardSymbol.Five => 5,
				CardSymbol.Six => 6,
				CardSymbol.Seven => 7,
				CardSymbol.Eight => 8,
				CardSymbol.Nine => 9,
				CardSymbol.Ten => 10,
				CardSymbol.Jack => 11,
				CardSymbol.Queen => 12,
				CardSymbol.King => 13,
				CardSymbol.Ace => 14,
				_ => 0
			};

			/// <summary>
			/// Gets the sort value for a suit (for consistent ordering: Clubs, Diamonds, Hearts, Spades).
			/// </summary>
			private static int GetSuitSortValue(CardSuit suit) => suit switch
			{
				CardSuit.Clubs => 0,
				CardSuit.Diamonds => 1,
				CardSuit.Hearts => 2,
				CardSuit.Spades => 3,
				_ => 0
			};
		}
