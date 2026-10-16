namespace CardGames.Poker.Api.Contracts;

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
	public const string BuiltInName = "Default";
	public const string BuiltInBackUrl = "/images/cards/back-blue-white.png";

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

	public static string BuiltInFaceUrl(string suit, string rank) => $"/images/cards/{FaceKey(suit, rank)}.png";

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
