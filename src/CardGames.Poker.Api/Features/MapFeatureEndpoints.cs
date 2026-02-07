using System.Reflection;

namespace CardGames.Poker.Api.Features;

public static class MapFeatureEndpoints
{
	private const string MapEndpointsMethodName = "MapEndpoints";

	/// <summary>
	/// Discovers and registers all endpoint map groups marked with <see cref="EndpointMapGroupAttribute"/>
	/// via assembly scanning.
	/// </summary>
	public static void AddFeatureEndpoints(this IEndpointRouteBuilder app)
	{
		var endpointMapGroupTypes = Assembly.GetExecutingAssembly()
			.GetTypes()
			.Where(t => t.GetCustomAttribute<EndpointMapGroupAttribute>() is not null);

		foreach (var mapGroupType in endpointMapGroupTypes)
		{
			var mapMethod = mapGroupType.GetMethod(
				MapEndpointsMethodName,
				BindingFlags.Public | BindingFlags.Static,
				[typeof(IEndpointRouteBuilder)]);

			mapMethod?.Invoke(null, [app]);
		}
	}
}
