using CardGames.Poker.Api.Features.Games.AvailablePokerGames;
using CardGames.Poker.Api.Features.Games.FiveCardDraw;

namespace CardGames.Poker.Api.Features;

public static class MapFeatureEndpoints
{
	public static void AddFeatureEndpoints(this IEndpointRouteBuilder app)
	{
		app.AddAvailablePokerGamesEndpoints();
		app.AddFiveCardDrawEndpoints();
		//app.AddCategoriesEndpoints();
		//app.AddFavoritesEndpoints();
		//app.AddIngredientsEndpoints();
		//app.AddMealPlansEndpoints();
		//app.AddMealsEndpoints();
		//app.AddRecipesEndpoints();
		//app.AddTagsEndpoints();
		//app.AddUsersEndpoints();
	}
}