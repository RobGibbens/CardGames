using System.Collections.Frozen;
using System.Reflection;
using CardGames.Poker.Games;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Registry for poker game metadata. Uses assembly scanning to discover all
/// <see cref="IPokerGame"/> implementations decorated with <see cref="PokerGameMetadataAttribute"/>.
/// </summary>
/// <remarks>
/// New game types are automatically discovered when they implement <see cref="IPokerGame"/>
/// and are decorated with <see cref="PokerGameMetadataAttribute"/>. No manual registration required.
/// </remarks>
public static class PokerGameMetadataRegistry
{
	// Constants kept for backward compatibility with existing code references
	public const string HoldEmCode = "HOLDEM";
	public const string FiveCardDrawCode = "FIVECARDDRAW";
	public const string TwosJacksManWithTheAxeCode = "TWOSJACKSMANWITHTHEAXE";
	public const string OmahaCode = "OMAHA";
	public const string SevenCardStudCode = "SEVENCARDSTUD";
	public const string KingsAndLowsCode = "KINGSANDLOWS";
	public const string FollowTheQueenCode = "FOLLOWTHEQUEEN";

	private static readonly FrozenDictionary<string, PokerGameMetadataAttribute> MetadataByGameTypeCode;
	private static readonly FrozenDictionary<string, Type> GameTypeByCode;

	static PokerGameMetadataRegistry()
	{
		var metadataDict = new Dictionary<string, PokerGameMetadataAttribute>(StringComparer.OrdinalIgnoreCase);
		var gameTypeDict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

		var pokerGameInterface = typeof(IPokerGame);
		var assembly = pokerGameInterface.Assembly;

		var gameTypes = assembly.GetTypes()
			.Where(t => t is { IsClass: true, IsAbstract: false } && pokerGameInterface.IsAssignableFrom(t));

		foreach (var gameType in gameTypes)
		{
			var attribute = gameType.GetCustomAttribute<PokerGameMetadataAttribute>(inherit: false);
			if (attribute is not null)
			{
				metadataDict[attribute.Code] = attribute;
				gameTypeDict[attribute.Code] = gameType;
			}
		}

		MetadataByGameTypeCode = metadataDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		GameTypeByCode = gameTypeDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Attempts to get metadata for the specified game type code.
	/// </summary>
	/// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
	/// <param name="metadata">The metadata if found.</param>
	/// <returns>True if metadata was found; otherwise, false.</returns>
	public static bool TryGet(string? gameTypeCode, out PokerGameMetadataAttribute? metadata)
	{
		if (string.IsNullOrWhiteSpace(gameTypeCode))
		{
			metadata = null;
			return false;
		}

		return MetadataByGameTypeCode.TryGetValue(gameTypeCode, out metadata);
	}

	/// <summary>
	/// Gets the metadata for the specified game type code, or null if not found.
	/// </summary>
	public static PokerGameMetadataAttribute? Get(string? gameTypeCode)
	{
		return TryGet(gameTypeCode, out var metadata) ? metadata : null;
	}

	/// <summary>
	/// Attempts to get the game type class for the specified game type code.
	/// </summary>
	/// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
	/// <param name="gameType">The game type class if found.</param>
	/// <returns>True if the game type was found; otherwise, false.</returns>
	public static bool TryGetGameType(string? gameTypeCode, out Type? gameType)
	{
		if (string.IsNullOrWhiteSpace(gameTypeCode))
		{
			gameType = null;
			return false;
		}

		return GameTypeByCode.TryGetValue(gameTypeCode, out gameType);
	}

	/// <summary>
	/// Gets all available game type codes.
	/// </summary>
	public static IEnumerable<string> GetAllGameTypeCodes()
	{
		return MetadataByGameTypeCode.Keys;
	}

	/// <summary>
	/// Gets all registered game metadata.
	/// </summary>
	public static IEnumerable<PokerGameMetadataAttribute> GetAllMetadata()
	{
		return MetadataByGameTypeCode.Values;
	}

	/// <summary>
	/// Checks if a game type code is registered.
	/// </summary>
	public static bool IsRegistered(string? gameTypeCode)
	{
		return !string.IsNullOrWhiteSpace(gameTypeCode) && MetadataByGameTypeCode.ContainsKey(gameTypeCode);
	}
}
