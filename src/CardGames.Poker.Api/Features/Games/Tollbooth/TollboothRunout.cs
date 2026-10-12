using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.Tollbooth;

/// <summary>
///     Fills a Tollbooth street that nobody chose, using the free furthest display card.
/// </summary>
internal static class TollboothRunout
{
	public static void DealStreet(
		Game game,
		IReadOnlyList<GamePlayer> playersStillInHand,
		IReadOnlyList<GameCard> handCards,
		string street,
		DateTimeOffset now,
		ILogger? logger)
	{
		var recipients = RecipientOrder(game, playersStillInHand);
		for (var index = 0; index < recipients.Count; index++)
		{
			var dealt = GiveFurthestOrDeck(
				game,
				handCards,
				recipients[index],
				street,
				now,
				replenishForNextPlayer: index + 1 < recipients.Count);
			if (dealt) continue;

			logger?.LogWarning(
				"Not enough cards in deck to deal remaining streets for game {GameId}",
				game.Id);
			break;
		}
	}

	private static List<GamePlayer> RecipientOrder(Game game, IReadOnlyList<GamePlayer> playersStillInHand)
	{
		var inHand = playersStillInHand.Where(player => !player.HasFolded).ToList();
		if (inHand.Count == 0) return [];

		var ordered = new List<GamePlayer>();
		var first = FirstInHandLeftOfDealer(game, inHand);
		if (first is not null) ordered.Add(first);

		ordered.AddRange(inHand.Where(player => !ordered.Contains(player)).OrderBy(player => player.SeatPosition));
		return ordered;
	}

	/// <summary>
	///     Seat immediately left of the dealer who is still in the hand. All-in seats are included.
	/// </summary>
	private static GamePlayer? FirstInHandLeftOfDealer(Game game, List<GamePlayer> inHand)
	{
		var totalPlayers = inHand.Max(player => player.SeatPosition) + 1;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = inHand.FirstOrDefault(candidate => candidate.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded) return player;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return null;
	}

	private static bool GiveFurthestOrDeck(
		Game game,
		IReadOnlyList<GameCard> handCards,
		GamePlayer player,
		string street,
		DateTimeOffset now,
		bool replenishForNextPlayer)
	{
		var display = DisplayCards(game, handCards);
		GameCard? card;
		var takenFromDisplay = false;
		if (display.Count > 0)
		{
			card = display[^1];
			takenFromDisplay = true;
		}
		else
		{
			card = DeckCards(game, handCards).FirstOrDefault();
		}

		if (card is null) return false;

		AssignToPlayer(game, handCards, card, player, street, now);

		if (takenFromDisplay && replenishForNextPlayer)
			ReplenishDisplay(game, handCards, now);

		return true;
	}

	private static void AssignToPlayer(
		Game game,
		IReadOnlyList<GameCard> handCards,
		GameCard card,
		GamePlayer player,
		string street,
		DateTimeOffset now)
	{
		var existingCardCount = handCards.Count(candidate =>
			candidate.GameId == game.Id &&
			candidate.HandNumber == game.CurrentHandNumber &&
			candidate.GamePlayerId == player.Id &&
			candidate.Location != CardLocation.Deck &&
			!candidate.IsDiscarded);
		var isSeventhStreet = street == nameof(Phases.SeventhStreet);

		card.GamePlayerId = player.Id;
		card.Location = isSeventhStreet ? CardLocation.Hole : CardLocation.Board;
		card.DealOrder = existingCardCount + 1;
		card.IsVisible = !isSeventhStreet;
		card.DealtAt = now;
		card.DealtAtPhase = street;
	}

	private static void ReplenishDisplay(Game game, IReadOnlyList<GameCard> handCards, DateTimeOffset now)
	{
		var replacement = DeckCards(game, handCards).FirstOrDefault();
		if (replacement is null) return;

		var maxDisplayDealOrder = DisplayCards(game, handCards)
			.Select(card => card.DealOrder)
			.DefaultIfEmpty(0)
			.Max();

		replacement.GamePlayerId = null;
		replacement.Location = CardLocation.Community;
		replacement.DealOrder = maxDisplayDealOrder + 1;
		replacement.IsVisible = true;
		replacement.DealtAt = now;
		replacement.DealtAtPhase = nameof(Phases.TollboothOffer);
	}

	private static List<GameCard> DisplayCards(Game game, IReadOnlyList<GameCard> handCards)
	{
		return handCards
			.Where(card => card.GameId == game.Id &&
			               card.HandNumber == game.CurrentHandNumber &&
			               card.Location == CardLocation.Community &&
			               card.GamePlayerId == null &&
			               !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.ToList();
	}

	private static IEnumerable<GameCard> DeckCards(Game game, IReadOnlyList<GameCard> handCards)
	{
		return handCards
			.Where(card => card.GameId == game.Id &&
			               card.HandNumber == game.CurrentHandNumber &&
			               card.Location == CardLocation.Deck &&
			               !card.IsDiscarded)
			.OrderBy(card => card.DealOrder);
	}
}
