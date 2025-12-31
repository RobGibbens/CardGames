using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

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
			nameof(KingsAndLowsPhase.WaitingToStart),
			nameof(KingsAndLowsPhase.Complete)
		};

		if (!validPhases.Contains(game.CurrentPhase))
		{
			return new StartHandError
			{
				Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase. " +
						  $"A new hand can only be started when the game is in '{nameof(KingsAndLowsPhase.WaitingToStart)}' " +
						  $"or '{nameof(KingsAndLowsPhase.Complete)}' phase.",
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
		}

		// 6. Remove any existing cards from previous hand
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			context.GameCards.RemoveRange(existingCards);
		}

		// 7. Create a new main pot for this hand
		var mainPot = new Pot
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

		// 8. Update game state - Kings and Lows starts with CollectingAntes
		game.CurrentHandNumber++;
		game.CurrentPhase = nameof(KingsAndLowsPhase.CollectingAntes);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

		// Set StartedAt only on first hand
		game.StartedAt ??= now;

		// 9. Automatically collect antes if there's an ante
		if (ante > 0)
		{
			foreach (var player in eligiblePlayers)
			{
				var anteAmount = Math.Min(ante, player.ChipStack);
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

		// 10. Automatically deal hands - move to Dealing phase then DropOrStay
		game.CurrentPhase = nameof(KingsAndLowsPhase.Dealing);
		
		// Create a standard deck of 52 cards
		var deck = new List<GameCard>();
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
					DealOrder = 0,
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

		// Deal 5 cards to each eligible player
		int cardIndex = 0;
		int dealOrder = 1;
		for (int round = 0; round < 5; round++)
		{
			foreach (var player in eligiblePlayers.OrderBy(p => p.SeatPosition))
			{
				if (cardIndex < deck.Count)
				{
					var card = deck[cardIndex++];
					card.Location = CardLocation.Hand;
					card.GamePlayerId = player.Id;
					card.IsVisible = false;
					card.DealOrder = dealOrder++;
					card.DealtAtPhase = nameof(KingsAndLowsPhase.Dealing);
					context.GameCards.Add(card);
				}
			}
		}

		// 11. Move to DropOrStay phase - this is where players make their decision
		game.CurrentPhase = nameof(KingsAndLowsPhase.DropOrStay);

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
}
