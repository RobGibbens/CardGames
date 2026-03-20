using CardGames.Poker.Api.Features.Testing.v1;

namespace CardGames.Poker.Api.Features.Testing;

[EndpointMapGroup]
public static class TestingApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var environment = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
		if (!environment.IsDevelopment())
		{
			return;
		}

		var testing = app.NewVersionedApi("Testing");
		testing.MapV1();
	}
}