namespace CardGames.Poker.Games;

public interface IPokerGame
{
	string Name { get; }
	string Description { get; }
	int MinimumNumberOfPlayers { get; }
	int MaximumNumberOfPlayers { get; }
}