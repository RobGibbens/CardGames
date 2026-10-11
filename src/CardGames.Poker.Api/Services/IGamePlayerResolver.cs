using System.Security.Claims;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Resolves the authenticated caller to a seated <see cref="GamePlayer" />.
/// </summary>
public interface IGamePlayerResolver
{
	Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, CancellationToken cancellationToken);

	Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, ClaimsPrincipal user, CancellationToken cancellationToken);

	Task<bool> IsTableHostAsync(Guid gameId, CancellationToken cancellationToken);
}
