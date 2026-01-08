namespace CardGames.Poker.Web.Services;

/// <summary>
/// Factory for resolving game-specific gameplay services based on game type code.
/// </summary>
public interface IGamePlayServiceFactory
{
	/// <summary>
	/// Resolves the appropriate gameplay service for the specified game type.
	/// </summary>
	/// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW", "SEVENCARDSTUD").</param>
	/// <returns>The gameplay service for the specified game type.</returns>
	/// <exception cref="NotSupportedException">Thrown if the game type is not supported.</exception>
	IGamePlayService Resolve(string gameTypeCode);
}
