using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extension methods for registering the IGamesApi client.
/// </summary>
public static class GamesApiServiceCollectionExtensions
{
	/// <summary>
	/// Registers the IGamesApi Refit client with the service collection.
	/// </summary>
	public static IServiceCollection AddGamesApiClient(
		this IServiceCollection services,
		Uri baseUrl,
		Action<IHttpClientBuilder>? builder = default,
		RefitSettings? settings = default)
	{
		var clientBuilder = services
			.AddRefitClient<IGamesApi>(settings)
			.ConfigureHttpClient(c => c.BaseAddress = baseUrl);

		clientBuilder.AddStandardResilienceHandler(config =>
		{
			config.Retry = new HttpRetryStrategyOptions
			{
				UseJitter = true,
				MaxRetryAttempts = 3,
				Delay = TimeSpan.FromSeconds(0.5)
			};
		});

		builder?.Invoke(clientBuilder);

		return services;
	}
}
