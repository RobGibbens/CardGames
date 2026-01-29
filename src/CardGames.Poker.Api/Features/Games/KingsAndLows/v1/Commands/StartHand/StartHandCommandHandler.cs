using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

/// <summary>
/// Handles the <see cref="StartHandCommand"/> to start a new hand in a Kings and Lows game.
/// </summary>
public class StartHandCommandHandler(CardsDbContext context, ILogger<StartHandCommandHandler> logger)
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
					.ThenInclude(gp => gp.Player)
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

		// 3. Finalize leave requests for players who were waiting for the hand to finish
		var playersLeaving = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
			.ToList();

		foreach (var player in playersLeaving)
		{
			player.Status = GamePlayerStatus.Left;
			player.LeftAt = now;
			player.FinalChipCount = player.ChipStack;
			player.IsSittingOut = true;
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

		// 4. Apply pending chips to player stacks
		var playersWithPendingChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			player.ChipStack += player.PendingChipsToAdd;
			player.PendingChipsToAdd = 0;
		}

		// 5. Get eligible players (active, not sitting out, chips >= ante or ante is 0)
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

		// 5a. Calculate current pot to check chip coverage
		// For Kings and Lows, players must be able to cover the pot in case they lose
		// Query for unawarded pots - this includes pots created by AcknowledgePotMatch for the next hand
		var unawardedPots = await context.Pots
			.Where(p => p.GameId == game.Id && !p.IsAwarded)
			.ToListAsync(cancellationToken);

		var currentPotAmount = unawardedPots.Sum(p => p.Amount);

		// Also check specifically for the pot for the upcoming hand (CurrentHandNumber + 1)
		var nextHandPot = unawardedPots.FirstOrDefault(p => p.HandNumber == game.CurrentHandNumber + 1);

		logger.LogInformation(
			"[CHIP-CHECK] Game {GameId}, CurrentHandNumber={HandNumber}: Found {PotCount} unawarded pots with total amount {PotAmount}. NextHandPot exists={NextHandPotExists} with amount={NextHandPotAmount}. Pot details: {PotDetails}",
			game.Id, game.CurrentHandNumber, unawardedPots.Count, currentPotAmount,
			nextHandPot != null, nextHandPot?.Amount ?? 0,
			string.Join("; ", unawardedPots.Select(p => $"HandNumber={p.HandNumber}, Amount={p.Amount}")));

		// Log each eligible player's chip stack for debugging
		foreach (var player in eligiblePlayers)
		{
			logger.LogInformation(
				"[CHIP-CHECK] Game {GameId}: Player {PlayerName} (Seat {Seat}) has {ChipStack} chips, pot is {PotAmount}, CanCover={CanCover}",
				game.Id, player.Player?.Name ?? "Unknown", player.SeatPosition, player.ChipStack, currentPotAmount, player.ChipStack >= currentPotAmount);
		}

		// Check if any eligible player cannot cover the pot
		var playersNeedingChips = eligiblePlayers
			.Where(p => p.ChipStack < currentPotAmount && !p.AutoDropOnDropOrStay)
			.ToList();

		logger.LogInformation(
			"[CHIP-CHECK] Game {GameId}: {ShortPlayerCount} player(s) cannot cover the pot. ShortPlayers: {ShortPlayers}",
			game.Id, playersNeedingChips.Count,
			string.Join(", ", playersNeedingChips.Select(p => $"{p.Player?.Name ?? "Unknown"}({p.ChipStack})")));

		// If the game is currently paused for chip check, check if we should resume or expire
		if (game.IsPausedForChipCheck)
		{
			// Check if pause timer has expired
			if (game.ChipCheckPauseEndsAt.HasValue && now >= game.ChipCheckPauseEndsAt.Value)
			{
				// Timer expired - mark short players for auto-drop
				foreach (var shortPlayer in playersNeedingChips)
				{
					shortPlayer.AutoDropOnDropOrStay = true;
					logger.LogInformation(
						"Chip check pause expired: Player {PlayerId} at seat {SeatPosition} will auto-drop (chips: {ChipStack}, pot: {PotAmount})",
						shortPlayer.PlayerId, shortPlayer.SeatPosition, shortPlayer.ChipStack, currentPotAmount);
				}

				// Clear pause state
				game.IsPausedForChipCheck = false;
				game.ChipCheckPauseStartedAt = null;
				game.ChipCheckPauseEndsAt = null;
				game.UpdatedAt = now;

				// Re-evaluate eligible players after marking auto-drops
				// Note: Auto-drop players can still play, they just auto-drop in DropOrStay phase
			}
			else
			{
				// Still within pause period - check if all players now have enough chips
				var stillShort = eligiblePlayers.Any(p => p.ChipStack < currentPotAmount && !p.AutoDropOnDropOrStay);
				if (!stillShort)
				{
					// All players now have enough chips - clear pause and continue
					game.IsPausedForChipCheck = false;
					game.ChipCheckPauseStartedAt = null;
					game.ChipCheckPauseEndsAt = null;
					game.UpdatedAt = now;
					logger.LogInformation("All players now have sufficient chips, resuming game {GameId}", game.Id);
				}
				else
				{
					// Still paused - return early
					await context.SaveChangesAsync(cancellationToken);
					return new StartHandError
					{
						Message = "Game is paused waiting for players to add chips. Resume will occur automatically when all players have sufficient chips or after 2 minutes.",
						Code = StartHandErrorCode.PausedForChipCheck
					};
				}
			}
		}
		else if (currentPotAmount > 0 && playersNeedingChips.Count > 0)
		{
			// New chip shortage detected - initiate pause
			game.IsPausedForChipCheck = true;
			game.ChipCheckPauseStartedAt = now;
			game.ChipCheckPauseEndsAt = now.AddMinutes(2);
			game.UpdatedAt = now;

			var shortPlayerNames = string.Join(", ", playersNeedingChips.Select(p => p.Player?.Name ?? $"Seat {p.SeatPosition}"));
			logger.LogInformation(
				"Chip check pause initiated for game {GameId}: {PlayerCount} player(s) cannot cover pot of {PotAmount}. Players: {PlayerNames}",
				game.Id, playersNeedingChips.Count, currentPotAmount, shortPlayerNames);

			await context.SaveChangesAsync(cancellationToken);

			return new StartHandError
			{
				Message = $"Cannot start hand: {playersNeedingChips.Count} player(s) cannot cover the pot of {currentPotAmount} chips. Game is paused for 2 minutes to allow adding chips.",
				Code = StartHandErrorCode.PausedForChipCheck
			};
		}

		// 5b. Reset player states for new hand
		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.DropOrStayDecision = null;
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

		// 7. Determine if this is the first hand (antes only collected on first hand in Kings and Lows)
		var isFirstHand = game.CurrentHandNumber == 0;

		// 8. Find unawarded pots from the current hand (e.g., if all players dropped) and carry them over
		var previousHandPots = await context.Pots
			.Where(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber && !p.IsAwarded)
			.ToListAsync(cancellationToken);

		var carriedOverAmount = 0;
		if (previousHandPots.Count > 0)
		{
			carriedOverAmount = previousHandPots.Sum(p => p.Amount);
			foreach (var pot in previousHandPots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = "Carried over - all players dropped";
			}
		}

		// 9. Find or create the main pot for this hand
		// For subsequent hands, the pot is typically created by AcknowledgePotMatchCommandHandler (or PerformShowdown),
		// but if a hand ends in a fold-win or all dropped, we need to create it here.
		Pot? mainPot = await context.Pots.FirstOrDefaultAsync(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber + 1 && !p.IsAwarded, cancellationToken);

		if (mainPot is null)
		{
			mainPot = new Pot
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber + 1,
				PotType = PotType.Main,
				PotOrder = 0,
				Amount = carriedOverAmount,
				IsAwarded = false,
				CreatedAt = now
			};

			context.Pots.Add(mainPot);
		}
		else if (carriedOverAmount > 0)
		{
			mainPot.Amount += carriedOverAmount;
		}

		// 10. Update game state
		game.CurrentHandNumber++;
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

		// Set StartedAt only on first hand
		game.StartedAt ??= now;

		// 11. Automatically collect antes if it's the first hand OR if the pot is empty (Kings and Lows rule)
		// This ensures the pot never stays at 0 indefinitely.
		bool collectAntes = isFirstHand || mainPot.Amount == 0;
		if (collectAntes && ante > 0)
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

				logger.LogInformation(
					"Kings and Lows pot created for game {GameId}, hand {HandNumber}: Amount={PotAmount}, Ante={Ante}, CollectAntes={CollectAntes}, IsFirstHand={IsFirstHand}",
					game.Id, game.CurrentHandNumber, mainPot.Amount, ante, collectAntes, isFirstHand);

				// 12. Automatically deal hands - move to Dealing phase then DropOrStay
				game.CurrentPhase = collectAntes ? nameof(Phases.CollectingAntes) : nameof(Phases.Dealing);
		
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

				// 13. Move to DropOrStay phase - this is where players make their decision
				game.CurrentPhase = nameof(Phases.DropOrStay);

				// 14. Persist changes
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
