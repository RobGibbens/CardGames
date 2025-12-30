using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games;

public interface IPokerGame
{
	string Name { get; }
	string Description { get; }
	int MinimumNumberOfPlayers { get; }
	int MaximumNumberOfPlayers { get; }

	/// <summary>
	/// Gets the game rules metadata that describes this game's flow, phases, and mechanics.
	/// This allows the UI and API to adapt to different game types without hardcoded logic.
	/// </summary>
	GameRules GetGameRules();
}