using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.CreateGame;

[Mapper]
public static partial class CreateGameMapper
{
	//[MapperIgnoreTarget(nameof(Category.ETag))]
	//[MapValue(nameof(Category.CreatedAt), Use = nameof(GetDateTimeUtc))]
	//[MapValue(nameof(Category.UpdatedAt), Use = nameof(GetDateTimeUtc))]

	//[MapValue(nameof(Recipe.Id), Use = nameof(GenerateId))]
	//public static partial Category ToEntity(this CreateGameCommand command);

	private static DateTimeOffset GetDateTimeUtc() => DateTimeOffset.UtcNow;
	private static string GenerateId() => Guid.NewGuid().ToString();
}