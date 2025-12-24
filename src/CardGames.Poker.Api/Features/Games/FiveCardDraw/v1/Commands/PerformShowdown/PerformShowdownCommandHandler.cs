using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Hands.DrawHands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;

/// <summary>
/// Handles the <see cref="PerformShowdownCommand"/> to evaluate hands and award pots.
/// </summary>
public class PerformShowdownCommandHandler(CardsDbContext context)
	: IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
	/// <inheritdoc />
	public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
		PerformShowdownCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players, cards, and pots
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.Pots.Where(p => !p.IsAwarded))
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new PerformShowdownError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PerformShowdownErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != nameof(FiveCardDrawPhase.Showdown))
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
				          $"Showdown can only be performed when the game is in '{nameof(FiveCardDrawPhase.Showdown)}' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 3. Get players who have not folded
		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && gp.Status == GamePlayerStatus.Active)
			.ToList();

		// 4. Load cards for players in hand
		var playerCards = await context.GameCards
			.Where(c => c.GameId == command.GameId &&
			            c.HandNumber == game.CurrentHandNumber &&
			            !c.IsDiscarded &&
			            c.GamePlayerId != null &&
			            playersInHand.Select(p => p.Id).Contains(c.GamePlayerId.Value))
			.ToListAsync(cancellationToken);

		var playerCardGroups = playerCards
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.ToList());

		// 5. Calculate total pot
		var totalPot = game.Pots.Sum(p => p.Amount);

		// 6. Handle win by fold (only one player remaining)
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			winner.ChipStack += totalPot;

			// Mark pots as awarded
			foreach (var pot in game.Pots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = "All others folded";
			}

			game.CurrentPhase = nameof(FiveCardDrawPhase.Complete);
			game.UpdatedAt = now;
			MoveDealer(game);

			await context.SaveChangesAsync(cancellationToken);

			var winnerCards = playerCardGroups.GetValueOrDefault(winner.Id, []);

			return new PerformShowdownSuccessful
			{
				GameId = game.Id,
				WonByFold = true,
				CurrentPhase = game.CurrentPhase,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands =
				[
					new ShowdownPlayerHand
					{
						PlayerName = winner.Player.Name,
						Cards = winnerCards.Select(c => new ShowdownCard
						{
							Suit = c.Suit,
							Symbol = c.Symbol
						}).ToList(),
						HandType = null,
						HandStrength = null,
						IsWinner = true,
						AmountWon = totalPot
					}
				]
			};
		}

		// 7. Evaluate all hands
		var playerHandEvaluations = new Dictionary<string, (DrawHand hand, List<GameCard> cards, GamePlayer gamePlayer)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
			{
				continue; // Skip players without valid hands
			}

			var coreCards = cards.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol))).ToList();
			var drawHand = new DrawHand(coreCards);
			playerHandEvaluations[gamePlayer.Player.Name] = (drawHand, cards, gamePlayer);
		}

		// 8. Determine winners (highest strength)
		var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
		var winners = playerHandEvaluations
			.Where(kvp => kvp.Value.hand.Strength == maxStrength)
			.Select(kvp => kvp.Key)
			.ToList();

		// 9. Calculate payouts (split pot if multiple winners)
		var payoutPerWinner = totalPot / winners.Count;
		var remainder = totalPot % winners.Count;
		var payouts = new Dictionary<string, int>();

		foreach (var winner in winners)
		{
			payouts[winner] = payoutPerWinner;
		}

		// Add remainder to first winner (closest to dealer's left)
		if (remainder > 0 && winners.Count > 0)
		{
			payouts[winners[0]] += remainder;
		}

		// 10. Update player chip stacks
		foreach (var payout in payouts)
		{
			var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
			gamePlayer.ChipStack += payout.Value;
		}

		// 11. Mark pots as awarded
		var winReason = winners.Count > 1
			? $"Split pot - {playerHandEvaluations[winners[0]].hand.Type}"
			: playerHandEvaluations[winners[0]].hand.Type.ToString();

		foreach (var pot in game.Pots)
		{
			pot.IsAwarded = true;
			pot.AwardedAt = now;
			pot.WinReason = winReason;
		}

		// 12. Update game state
		game.CurrentPhase = nameof(FiveCardDrawPhase.Complete);
		game.UpdatedAt = now;
		MoveDealer(game);

		await context.SaveChangesAsync(cancellationToken);

		// 13. Build response
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isWinner = winners.Contains(kvp.Key);
			return new ShowdownPlayerHand
			{
				PlayerName = kvp.Key,
				Cards = kvp.Value.cards.Select(c => new ShowdownCard
				{
					Suit = c.Suit,
					Symbol = c.Symbol
				}).ToList(),
				HandType = kvp.Value.hand.Type.ToString(),
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0)
			};
		}).OrderByDescending(h => h.HandStrength ?? 0).ToList();

		return new PerformShowdownSuccessful
		{
			GameId = game.Id,
			WonByFold = false,
			CurrentPhase = game.CurrentPhase,
			Payouts = payouts,
			PlayerHands = playerHandsList
		};
	}

	/// <summary>
	/// Moves the dealer button to the next position.
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var totalPlayers = game.GamePlayers.Count;
		game.DealerPosition = (game.DealerPosition + 1) % totalPlayers;
	}

	/// <summary>
	/// Maps entity CardSuit to core library Suit.
	/// </summary>
	private static Suit MapSuit(CardSuit suit) => suit switch
	{
		CardSuit.Hearts => Suit.Hearts,
		CardSuit.Diamonds => Suit.Diamonds,
		CardSuit.Spades => Suit.Spades,
		CardSuit.Clubs => Suit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
	};

	/// <summary>
	/// Maps entity CardSymbol to core library Symbol.
	/// </summary>
	private static Symbol MapSymbol(CardSymbol symbol) => symbol switch
	{
		CardSymbol.Deuce => Symbol.Deuce,
		CardSymbol.Three => Symbol.Three,
		CardSymbol.Four => Symbol.Four,
		CardSymbol.Five => Symbol.Five,
		CardSymbol.Six => Symbol.Six,
		CardSymbol.Seven => Symbol.Seven,
		CardSymbol.Eight => Symbol.Eight,
		CardSymbol.Nine => Symbol.Nine,
		CardSymbol.Ten => Symbol.Ten,
		CardSymbol.Jack => Symbol.Jack,
		CardSymbol.Queen => Symbol.Queen,
		CardSymbol.King => Symbol.King,
		CardSymbol.Ace => Symbol.Ace,
		_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
	};
}

