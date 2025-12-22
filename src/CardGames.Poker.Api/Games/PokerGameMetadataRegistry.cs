using System.Collections.Frozen;
using System.Reflection;
using CardGames.Poker.Games;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.HoldEm;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Games.Omaha;
using CardGames.Poker.Games.SevenCardStud;

namespace CardGames.Poker.Api.Games;

public static class PokerGameMetadataRegistry
{
	public const string HoldEmCode = "HOLDEM";
	public const string FiveCardDrawCode = "FIVECARDDRAW";
	public const string OmahaCode = "OMAHA";
	public const string SevenCardStudCode = "SEVENCARDSTUD";
	public const string KingsAndLowsCode = "KINGSANDLOWS";
	public const string FollowTheQueenCode = "FOLLOWTHEQUEEN";

	private static readonly FrozenDictionary<string, PokerGameMetadataAttribute> MetadataByGameTypeCode =
		new Dictionary<string, PokerGameMetadataAttribute>(StringComparer.OrdinalIgnoreCase)
		{
			[HoldEmCode] = GetMetadataOrThrow(typeof(HoldEmGame)),
			[FiveCardDrawCode] = GetMetadataOrThrow(typeof(FiveCardDrawGame)),
			[OmahaCode] = GetMetadataOrThrow(typeof(OmahaGame)),
			[SevenCardStudCode] = GetMetadataOrThrow(typeof(SevenCardStudGame)),
			[KingsAndLowsCode] = GetMetadataOrThrow(typeof(KingsAndLowsGame)),
			[FollowTheQueenCode] = GetMetadataOrThrow(typeof(FollowTheQueenGame))
		}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	public static bool TryGet(string? gameTypeCode, out PokerGameMetadataAttribute? metadata)
	{
		if (string.IsNullOrWhiteSpace(gameTypeCode))
		{
			metadata = null;
			return false;
		}

		return MetadataByGameTypeCode.TryGetValue(gameTypeCode, out metadata);
	}

	private static PokerGameMetadataAttribute GetMetadataOrThrow(MemberInfo gameType)
	{
		var attribute = gameType.GetCustomAttribute<PokerGameMetadataAttribute>(inherit: false);
		return attribute ?? throw new InvalidOperationException($"Poker game type '{gameType.Name}' is missing '{nameof(PokerGameMetadataAttribute)}'.");
	}
}
