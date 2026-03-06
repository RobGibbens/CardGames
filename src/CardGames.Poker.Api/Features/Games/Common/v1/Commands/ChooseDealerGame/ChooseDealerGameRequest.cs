namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public record ChooseDealerGameRequest(string GameTypeCode, int Ante, int MinBet, int? SmallBlind = null, int? BigBlind = null);
