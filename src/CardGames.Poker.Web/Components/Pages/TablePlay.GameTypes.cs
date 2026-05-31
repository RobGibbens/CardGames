using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CardGames.Contracts.SignalR;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using CardGames.Poker.Web.Components.Shared;
using CardGames.Poker.Web.Extensions;
using CardGames.Poker.Web.Infrastructure.Validation;
using CardGames.Poker.Web.Services;
using CardGames.Poker.Web.Services.TableActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using IApiResponse = Refit.IApiResponse;

namespace CardGames.Poker.Web.Components.Pages;

public partial class TablePlay
{
    // Game type code for routing to correct API client
    private string? _gameTypeCode;
    private bool IsTwosJacksManWithTheAxe => string.Equals(_gameTypeCode, "TWOSJACKSMANWITHTHEAXE",
    StringComparison.OrdinalIgnoreCase);
    private bool IsFiveCardDraw => string.Equals(_gameTypeCode, "FIVECARDDRAW", StringComparison.OrdinalIgnoreCase);
    private bool IsKingsAndLows => string.Equals(_gameTypeCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
    private bool IsSevenCardStud => string.Equals(_gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);
    private bool IsPairPressure => string.Equals(_gameTypeCode, "PAIRPRESSURE", StringComparison.OrdinalIgnoreCase);
    private bool IsRazz => string.Equals(_gameTypeCode, "RAZZ", StringComparison.OrdinalIgnoreCase);
    private bool IsGoodBadUgly => string.Equals(_gameTypeCode, "GOODBADUGLY", StringComparison.OrdinalIgnoreCase);
    private bool IsFollowTheQueen => string.Equals(_gameTypeCode, "FOLLOWTHEQUEEN", StringComparison.OrdinalIgnoreCase);
    private bool IsBaseball => string.Equals(_gameTypeCode, "BASEBALL", StringComparison.OrdinalIgnoreCase);
    private bool IsHoldEm => string.Equals(_gameTypeCode, "HOLDEM", StringComparison.OrdinalIgnoreCase);
    private bool IsRedRiver => string.Equals(_gameTypeCode, "REDRIVER", StringComparison.OrdinalIgnoreCase);
    private bool IsHoldTheBaseball => string.Equals(_gameTypeCode, "HOLDTHEBASEBALL", StringComparison.OrdinalIgnoreCase);
    private bool IsOmaha => string.Equals(_gameTypeCode, "OMAHA", StringComparison.OrdinalIgnoreCase);
    private bool IsNebraska => string.Equals(_gameTypeCode, "NEBRASKA", StringComparison.OrdinalIgnoreCase);
    private bool IsSouthDakota => string.Equals(_gameTypeCode, "SOUTHDAKOTA", StringComparison.OrdinalIgnoreCase);
    private bool IsBobBarker => string.Equals(_gameTypeCode, "BOBBARKER", StringComparison.OrdinalIgnoreCase);
    private bool IsIrishHoldEm => string.Equals(_gameTypeCode, "IRISHHOLDEM", StringComparison.OrdinalIgnoreCase);
    private bool IsPhilsMom => string.Equals(_gameTypeCode, "PHILSMOM", StringComparison.OrdinalIgnoreCase);
    private bool IsCrazyPineapple => string.Equals(_gameTypeCode, "CRAZYPINEAPPLE", StringComparison.OrdinalIgnoreCase);
    private bool IsScrewYourNeighbor => string.Equals(_gameTypeCode, "SCREWYOURNEIGHBOR", StringComparison.OrdinalIgnoreCase);
    private bool IsTollbooth => string.Equals(_gameTypeCode, "TOLLBOOTH", StringComparison.OrdinalIgnoreCase);
    private bool IsKlondike => string.Equals(_gameTypeCode, "KLONDIKE", StringComparison.OrdinalIgnoreCase);
    private bool IsInBetween => string.Equals(_gameTypeCode, "INBETWEEN", StringComparison.OrdinalIgnoreCase);
    private bool IsStartedScrewYourNeighborTable => IsScrewYourNeighbor && _gameResponse?.StartedAt is not null;
    private bool SupportsRabbitHunt => IsHoldEm || IsRedRiver || IsKlondike || IsOmaha || IsNebraska || IsSouthDakota || IsBobBarker || IsIrishHoldEm || IsPhilsMom || IsCrazyPineapple || IsHoldTheBaseball;
    private bool SupportsOdds => !IsInBetween && !IsScrewYourNeighbor;
    private bool IsIrishStyleHoldEm => IsIrishHoldEm || IsPhilsMom || IsCrazyPineapple;
    private bool IsSevenCardStudStyle => IsSevenCardStud || IsPairPressure;
    private bool UsesCardDealAnimation => IsSevenCardStudStyle || IsRazz || IsGoodBadUgly || IsBaseball || IsFollowTheQueen || IsFiveCardDraw || IsTwosJacksManWithTheAxe || IsKingsAndLows || IsHoldEm || IsRedRiver || IsKlondike || IsHoldTheBaseball || IsOmaha || IsNebraska || IsSouthDakota || IsBobBarker || IsIrishHoldEm || IsPhilsMom || IsCrazyPineapple || IsScrewYourNeighbor || IsTollbooth || IsInBetween;
    
    private int DealAnimationDelayMs => 750;
}
