using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CreateGame;

[Mapper]
public static partial class CreateGameMapper
{
	private static DateTimeOffset GetDateTimeUtc()
	{
		return DateTimeOffset.UtcNow;
	}

	private static string GenerateId()
	{
		return Guid.NewGuid().ToString();
	}
}