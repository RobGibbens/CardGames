// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Refit;
// using CardGames.Poker.Api.Contracts;
//
// namespace CardGames.Poker.Api.Clients;
//
// public partial interface IGamesApi
// {
//     [Post("/api/games/{gameId}/sit-out")]
//     Task<IApiResponse> SetSittingOutAsync(Guid gameId, [Body] SetSittingOutRequest request, CancellationToken cancellationToken = default);
// }
//
