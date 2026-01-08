namespace CardGames.Poker.Web.Services;

/// <summary>
/// Factory implementation for resolving game-specific gameplay services.
/// </summary>
public class GamePlayServiceFactory : IGamePlayServiceFactory
{
	private readonly Dictionary<string, IGamePlayService> _services;

	public GamePlayServiceFactory(IEnumerable<IGamePlayService> services)
	{
		if (services == null)
			throw new ArgumentNullException(nameof(services));

		_services = services.ToDictionary(
			s => s.GameTypeCode,
			s => s,
			StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public IGamePlayService Resolve(string gameTypeCode)
	{
		if (string.IsNullOrWhiteSpace(gameTypeCode))
			throw new ArgumentException("Game type code cannot be null or empty", nameof(gameTypeCode));

		if (_services.TryGetValue(gameTypeCode, out var service))
		{
			return service;
		}

		throw new NotSupportedException($"Game type '{gameTypeCode}' is not supported");
	}
}
