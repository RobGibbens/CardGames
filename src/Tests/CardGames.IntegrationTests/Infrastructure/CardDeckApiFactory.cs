using CardGames.Poker.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CardGames.IntegrationTests.Infrastructure;

public sealed class CardDeckApiFactory : ApiWebApplicationFactory
{
	public MemoryCardDeckStorage Storage { get; } = new();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<ICardDeckStorageService>();
			services.AddSingleton<ICardDeckStorageService>(Storage);
		});
	}
}
