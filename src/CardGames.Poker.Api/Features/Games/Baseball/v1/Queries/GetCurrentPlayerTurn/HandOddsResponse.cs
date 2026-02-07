namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public record HandOddsResponse(IReadOnlyList<HandTypeOdds> HandTypeProbabilities);

public record HandTypeOdds(string HandType, string DisplayName, decimal Probability);
