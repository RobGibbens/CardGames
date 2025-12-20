using System.Reflection;
using System;
using CardGames.Poker.Games;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;

/// <summary>
/// Handler that uses reflection to discover all IPokerGame implementations
/// in the CardGames.Poker assembly and returns their metadata.
/// </summary>
public class GetAvailablePokerGamesQueryHandler(HybridCache hybridCache)
	: IRequestHandler<GetAvailablePokerGamesQuery, List<GetAvailablePokerGamesResponse>>
{
	public async Task<List<GetAvailablePokerGamesResponse>> Handle(
		GetAvailablePokerGamesQuery request,
		CancellationToken cancellationToken)
	{
		var entryOptions = new HybridCacheEntryOptions
		{
			Expiration = TimeSpan.FromMinutes(10)
		};

		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			_ =>
			{
				var pokerGameType = typeof(IPokerGame);
				var assembly = pokerGameType.Assembly;

				var games = assembly.GetTypes()
					.Where(t => t is { IsClass: true, IsAbstract: false } && pokerGameType.IsAssignableFrom(t))
					.Select(CreateGameResponse)
					.Where(g => g is not null)
					.Cast<GetAvailablePokerGamesResponse>()
					.OrderBy(g => g.Name)
					.ToList();

				return ValueTask.FromResult(games);
			},
			cancellationToken: cancellationToken,
			options: entryOptions,
			tags: [Feature.Version, Feature.Name, nameof(GetAvailablePokerGamesQuery)]
		);
	}

	private static GetAvailablePokerGamesResponse? CreateGameResponse(Type gameType)
	{
		try
		{
			var metadata = gameType.GetCustomAttribute<PokerGameMetadataAttribute>();
			if (metadata is not null)
			{
				return new GetAvailablePokerGamesResponse(
					metadata.Name,
					metadata.Description,
					metadata.MinimumNumberOfPlayers,
					metadata.MaximumNumberOfPlayers
				);
			}

			// Back-compat fallback: Try to find a parameterless constructor or create with default values
			var instance = CreateGameInstance(gameType);
			return instance is null
				? null
				: new GetAvailablePokerGamesResponse(
					instance.Name,
					instance.Description,
					instance.MinimumNumberOfPlayers,
					instance.MaximumNumberOfPlayers
				);
		}
		catch (TargetInvocationException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch (MissingMethodException)
		{
			// If we can't instantiate the game, skip it
			return null;
		}
	}

	private static IPokerGame? CreateGameInstance(Type gameType)
	{
		// Try parameterless constructor first
		var parameterlessCtor = gameType.GetConstructor(Type.EmptyTypes);
		if (parameterlessCtor is not null)
		{
			return Activator.CreateInstance(gameType) as IPokerGame;
		}

		// Try to find a constructor and provide default values
		var constructors = gameType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
		if (constructors.Length == 0)
		{
			return null;
		}

		var ctor = constructors[0];
		var parameters = ctor.GetParameters();
		var args = new object?[parameters.Length];

		for (var i = 0; i < parameters.Length; i++)
		{
			var paramType = parameters[i].ParameterType;
			args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
		}

		return ctor.Invoke(args) as IPokerGame;
	}
}
