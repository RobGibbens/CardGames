namespace CardGames.Poker.Api.Contracts;

public sealed record BuiltInCardDeck(string Key, Guid? Id, string Name, string ImageBase, string BackFileName)
{
	public string BackUrl => ImageBase + BackFileName;

	public string FaceUrl(string suit, string rank) => $"{ImageBase}{suit}-{rank}.png";
}

public static class CardDeckSlots
{
	public const int RequiredFaceCount = 52;
	public const int RequiredHeight = 1259;
	public const int MinimumWidth = 750;
	public const int MaximumWidth = 920;
	public const int MaxNameLength = 60;
	public const int MaxDecksPerUser = 10;
	public const long MaxUploadBytes = 5 * 1024 * 1024;
	public const string BackSlotKey = "back";
	public const string DefaultBuiltInKey = "fridaynightpoker";

	public static readonly BuiltInCardDeck FridayNightPoker = new(
		DefaultBuiltInKey,
		null,
		"Friday Night Poker",
		"/images/cards/fridaynightpoker/",
		"card-back.png");

	public static readonly BuiltInCardDeck Basic = new(
		"basic",
		Guid.Parse("b451c000-0000-4000-8000-000000000001"),
		"Basic",
		"/images/cards/basic/",
		"card-back.png");

	public static readonly IReadOnlyList<BuiltInCardDeck> BuiltIns = [FridayNightPoker, Basic];

	public static BuiltInCardDeck DefaultBuiltIn => FridayNightPoker;

	public const string BuiltInName = "Friday Night Poker";
	public const string BuiltInImageBase = "/images/cards/fridaynightpoker/";
	public const string BuiltInBackUrl = BuiltInImageBase + "card-back.png";

	public static readonly IReadOnlyList<string> Suits = ["spades", "hearts", "diamonds", "clubs"];

	public static readonly IReadOnlyList<string> Ranks =
	[
		"ace", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "jack", "queen", "king"
	];

	public static IEnumerable<(string Suit, string Rank)> Faces()
	{
		foreach (var suit in Suits)
		{
			foreach (var rank in Ranks)
			{
				yield return (suit, rank);
			}
		}
	}

	public static string FaceKey(string suit, string rank) => $"{suit}-{rank}";

	public static string BuiltInFaceUrl(string suit, string rank) => DefaultBuiltIn.FaceUrl(suit, rank);

	public static bool TryGetBuiltIn(Guid id, out BuiltInCardDeck deck)
	{
		foreach (var candidate in BuiltIns)
		{
			if (candidate.Id == id)
			{
				deck = candidate;
				return true;
			}
		}

		deck = DefaultBuiltIn;
		return false;
	}

	public static BuiltInCardDeck ResolveBuiltIn(Guid? selectedId)
	{
		if (selectedId is Guid id && TryGetBuiltIn(id, out var deck))
		{
			return deck;
		}

		return DefaultBuiltIn;
	}

	public static bool IsReservedName(string name) =>
		BuiltIns.Any(deck => string.Equals(deck.Name, name, StringComparison.OrdinalIgnoreCase) ||
		                     string.Equals(deck.Key, name, StringComparison.OrdinalIgnoreCase));

	public static bool IsKnownSlot(string? slotKey)
	{
		if (string.IsNullOrWhiteSpace(slotKey))
		{
			return false;
		}

		if (string.Equals(slotKey, BackSlotKey, StringComparison.Ordinal))
		{
			return true;
		}

		var separator = slotKey.IndexOf('-');
		if (separator <= 0 || separator >= slotKey.Length - 1)
		{
			return false;
		}

		return TryGetFace(slotKey[..separator], slotKey[(separator + 1)..], out _);
	}

	public static bool TryGetFace(string? suit, string? rank, out string slotKey)
	{
		slotKey = string.Empty;
		if (string.IsNullOrWhiteSpace(suit) || string.IsNullOrWhiteSpace(rank))
		{
			return false;
		}

		var normalizedSuit = suit.Trim().ToLowerInvariant();
		var normalizedRank = rank.Trim().ToLowerInvariant();
		if (!Suits.Contains(normalizedSuit, StringComparer.Ordinal) ||
		    !Ranks.Contains(normalizedRank, StringComparer.Ordinal))
		{
			return false;
		}

		slotKey = FaceKey(normalizedSuit, normalizedRank);
		return true;
	}
}
