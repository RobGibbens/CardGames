namespace CardGames.Poker.Games;

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PokerGameMetadataAttribute(
	string name,
	string description,
	int minimumNumberOfPlayers,
	int maximumNumberOfPlayers,
	string? imageName = null) : Attribute
{
	public string Name { get; } = name;
	public string Description { get; } = description;
	public int MinimumNumberOfPlayers { get; } = minimumNumberOfPlayers;
	public int MaximumNumberOfPlayers { get; } = maximumNumberOfPlayers;
	public string? ImageName { get; } = imageName;
}
