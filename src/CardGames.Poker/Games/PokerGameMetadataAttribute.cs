using CardGames.Poker.Betting;

namespace CardGames.Poker.Games;

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PokerGameMetadataAttribute(
	string code,
	string name,
	string description,
	int minimumNumberOfPlayers,
	int maximumNumberOfPlayers,
	int initialHoleCards,
	int initialBoardCards,
	int maxCommunityCards,
	int maxPlayerCards,
	bool hasDrawPhase,
	int maxDiscards,
	WildCardRule wildCardRule,
	BettingStructure bettingStructure,
	string? imageName = null) : Attribute
{
	public string Code { get; } = code;
	public string Name { get; } = name;
	public string Description { get; } = description;
	public int MinimumNumberOfPlayers { get; } = minimumNumberOfPlayers;
	public int MaximumNumberOfPlayers { get; } = maximumNumberOfPlayers;
	public BettingStructure BettingStructure { get; } = bettingStructure;
	public string? ImageName { get; } = imageName;
	public int InitialHoleCards { get; } = initialHoleCards;
	public int InitialBoardCards { get; } = initialBoardCards;
	public int MaxCommunityCards { get; } = maxCommunityCards;
	public int MaxPlayerCards { get; } = maxPlayerCards;
	public bool HasDrawPhase { get; } = hasDrawPhase;
	public int MaxDiscards { get; } = maxDiscards;
	public WildCardRule WildCardRule { get; } = wildCardRule;
}
