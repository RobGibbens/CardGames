using CardGames.Poker.Api.Clients;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CardGames.Contracts;

/// <summary>
/// Extension methods for configuring Seven Card Stud API clients.
/// </summary>
public static class ISevenCardStudApiExtensions
{
	/// <summary>
	/// Adds the Seven Card Stud API Refit client to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="baseUrl">The base URL for the API.</param>
	/// <param name="configureClient">Optional action to configure the HTTP client.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSevenCardStudApi(
		this IServiceCollection services,
		Uri baseUrl,
		Action<IHttpClientBuilder>? configureClient = null)
	{
		var clientBuilder = services
			.AddRefitClient<ISevenCardStudApi>(new RefitSettings())
			.ConfigureHttpClient(c => c.BaseAddress = baseUrl);

		configureClient?.Invoke(clientBuilder);

		return services;
	}
}
