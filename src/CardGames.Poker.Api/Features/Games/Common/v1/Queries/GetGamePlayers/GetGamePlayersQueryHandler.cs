using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Profile;
using CardGames.Poker.Api.Infrastructure.Caching;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGamePlayers;

/// <summary>
/// Handler for retrieving all players in a specific game.
/// </summary>
public class GetGamePlayersQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetGamePlayersQuery, List<GetGamePlayersResponse>>
{
       public async Task<List<GetGamePlayersResponse>> Handle(GetGamePlayersQuery request, CancellationToken cancellationToken)
       {
               // Load the game to get the current hand number
               var game = await context.Games
                       .AsNoTracking()
                       .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

               if (game is null)
               {
                       return [];
               }

               var currentHandNumber = game.CurrentHandNumber;

			var gamePlayers = await hybridCache.GetOrCreateAsync(
                       $"{Feature.Version}-{request.CacheKey}",
                       async _ =>
                               await context.GamePlayers
                                       .Where(gp => gp.GameId == request.GameId)
                                       .Include(gp => gp.Player)
                                       .Include(gp => gp.Cards)
                                       .OrderBy(gp => gp.SeatPosition)
                                       .AsNoTracking()
								.ToListAsync(cancellationToken),
                       cancellationToken: cancellationToken,
				   options: new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(3) },
                       tags: [GameCacheKeys.GamePlayersTag(request.GameId)]
               );

		var usersByEmail = await context.Users
			.AsNoTracking()
			.Where(u => u.Email != null)
                        .Select(u => new { u.Id, Email = u.Email!, u.FirstName, u.AvatarUrl })
			.ToDictionaryAsync(u => u.Email, StringComparer.OrdinalIgnoreCase, cancellationToken);

		return gamePlayers
			.Select(gp =>
			{
				usersByEmail.TryGetValue(gp.Player.Email ?? string.Empty, out var user);

                                var avatarUrl = BuildAvatarUrl(user?.Id, user?.AvatarUrl ?? gp.Player.AvatarUrl);
					
				return GetGamePlayersMapper.ToResponse(gp, currentHandNumber, user?.FirstName, avatarUrl);
			})
			.ToList();
       }

        private static string? BuildAvatarUrl(string? userId, string? avatarUrl)
        {
                if (string.IsNullOrWhiteSpace(avatarUrl))
                {
                        return null;
                }

                avatarUrl = avatarUrl.Trim();
                if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out var absoluteUri))
                {
                        var isLocalHost = absoluteUri.IsLoopback || string.Equals(absoluteUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                        if (!isLocalHost && string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                        {
                                return absoluteUri.ToString();
                        }
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                        return null;
                }

                return ProfileAvatarRoutes.BuildAvatarPath(userId);
        }
}
