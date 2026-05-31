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
    // Phase category helper properties - driven by SignalR state
    private bool IsBettingPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Betting", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsDrawingPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Drawing", StringComparison.OrdinalIgnoreCase) == true
    || _isDrawPhase; // Fallback to existing logic

    private bool IsDecisionPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Decision", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsSetupPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Setup", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsResolutionPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Resolution", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsSpecialPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Special", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsBuyCardOfferPhase =>
    string.Equals(_tableState?.CurrentPhase, "BuyCardOffer", StringComparison.OrdinalIgnoreCase);

    private bool IsCurrentPlayerBuyCardDecisionMaker =>
    _currentPlayerSeatIndex >= 0 &&
    _currentPlayerSeatIndex == (_tableState?.CurrentActorSeatIndex ?? -1);

    private bool ShouldShowBuyCardOverlay =>
    IsBuyCardOfferPhase &&
    IsCurrentPlayerBuyCardDecisionMaker &&
    _buyCardOffer is not null &&
    IsParticipatingInHand &&
    (IsBuyCardTriggerCardVisible() || _dealAnimationPausedForBuyCardOffer) &&
    (!IsCardDealingVisualInProgress || _dealAnimationPausedForBuyCardOffer);

    private bool IsTollboothOfferPhase =>
    string.Equals(_tableState?.CurrentPhase, "TollboothOffer", StringComparison.OrdinalIgnoreCase);

    private bool ShouldShowTollboothOfferOverlay =>
    IsTollboothOfferPhase &&
    _tollboothOffer is not null &&
    _tollboothOffer.IsMyTurnToChoose &&
    IsParticipatingInHand;

    private bool IsCardDealingVisualInProgress =>
    _dealAnimationInProgress || _flyingCard is not null;

    // Drop or Stay phase detection (for Kings and Lows)
    private bool IsDropOrStayPhase =>
    string.Equals(_gameResponse?.CurrentPhase, "DropOrStay", StringComparison.OrdinalIgnoreCase);

    // Keep or Trade phase detection (for Screw Your Neighbor)
    private bool IsKeepOrTradePhase =>
    string.Equals(_gameResponse?.CurrentPhase, "KeepOrTrade", StringComparison.OrdinalIgnoreCase);

    // In-Between turn phase detection
    private bool IsInBetweenTurnPhase =>
    string.Equals(_gameResponse?.CurrentPhase, "InBetweenTurn", StringComparison.OrdinalIgnoreCase);

    private bool ShouldShowInBetweenOverlay =>
    IsInBetween && IsInBetweenTurnPhase && _isPlayerTurn && IsParticipatingInHand &&
    !IsCardDealingVisualInProgress;

    // Reveal phase detection (for Screw Your Neighbor)
    private bool IsRevealPhase =>
    string.Equals(_gameResponse?.CurrentPhase, "Reveal", StringComparison.OrdinalIgnoreCase);

    // Draw Complete phase detection (for Kings and Lows - delay before showdown)
    private bool IsDrawCompletePhase =>
    string.Equals(_gameResponse?.CurrentPhase, "DrawComplete", StringComparison.OrdinalIgnoreCase);

    // Player vs Deck phase detection (for Kings and Lows)
    private bool IsPlayerVsDeckPhase =>
    string.Equals(_gameResponse?.CurrentPhase, "PlayerVsDeck", StringComparison.OrdinalIgnoreCase);

    // Pot matching phase detection (for Kings and Lows)
    private bool IsPotMatchingPhase =>
    string.Equals(_gameResponse?.CurrentPhase, "PotMatching", StringComparison.OrdinalIgnoreCase);

    // Dealer's Choice phase detection
    private bool IsDealersChoicePhase =>
    string.Equals(_gameResponse?.CurrentPhase, "WaitingForDealerChoice", StringComparison.OrdinalIgnoreCase);

    private bool IsDealersChoiceWaitingForSelectedGameStart =>
    _isDealersChoice &&
    string.Equals(_gameResponse?.CurrentPhase, GamePhase.WaitingToStart.ToString(), StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(_gameTypeCode) &&
    !string.Equals(_gameTypeCode, "DEALERSCHOICE", StringComparison.OrdinalIgnoreCase);

    private bool IsCurrentPlayerDealersChoiceDealer =>
    _isDealersChoice && _dealersChoiceDealerPosition.HasValue && _currentPlayerSeatIndex == _dealersChoiceDealerPosition.Value;

    // Whether the current player is a loser in pot matching.
    private bool IsCurrentPlayerLoser => _showdownResult?.PlayerHands?.Any(h =>
    (string.Equals(h.PlayerName, _loggedInUserEmail, StringComparison.OrdinalIgnoreCase) ||
    string.Equals(h.PlayerName, _currentPlayerName, StringComparison.OrdinalIgnoreCase)) &&
    h.IsWinner == false) == true;

    // Helper property to check if current player is participating in the current hand
    private bool IsParticipatingInHand =>
    _isSeated &&
    _currentPlayerSeatIndex >= 0 &&
    _currentPlayerSeatIndex < _seats.Count &&
    !_seats[_currentPlayerSeatIndex].IsFolded &&
    (!_seats[_currentPlayerSeatIndex].IsSittingOut || _seats[_currentPlayerSeatIndex].Cards.Count > 0);

    private bool IsSittingOut =>
    _currentPlayerSeatIndex >= 0 &&
    _currentPlayerSeatIndex < _seats.Count &&
    _seats[_currentPlayerSeatIndex].IsSittingOut;

    private bool IsInWaitingState =>
    _gameResponse?.CurrentPhase == GamePhase.WaitingForPlayers.ToString() ||
    _gameResponse?.CurrentPhase == GamePhase.WaitingToStart.ToString();

    private bool IsGameActive =>
    _gameResponse is not null && _gameResponse.CurrentPhase != GamePhase.Ended.ToString();

    private bool ShowGameVariantBadges =>
    !_isLoading && !string.IsNullOrWhiteSpace(_gameTypeCode);

    private bool ShouldShowHostControls =>
    !_isLoading && _isHost;

    private bool ShouldShowHostStartControls =>
    ShouldShowHostControls && IsInWaitingState;

    private bool ShouldShowHostInGameControls =>
    ShouldShowHostControls && IsGameActive && !IsInWaitingState;

    private bool ShouldShowActionPanel =>
    _isPlayerTurn &&
    IsBettingPhase &&
    CurrentPlayerHandCards.Count > 0 &&
    !IsCardDealingVisualInProgress;

    private bool ShouldShowDecisionPanel =>
    _isPlayerTurn &&
    IsDecisionPhase &&
    !(HasDropOrStay && IsDropOrStayPhase) &&
    !(HasKeepOrTrade && IsKeepOrTradePhase) &&
    !IsInBetween &&
    !IsCardDealingVisualInProgress;

    private bool ShouldShowDrawPanel =>
    !IsCardDealingVisualInProgress &&
    !IsTollboothOfferPhase &&
    ((IsDrawingPhase && (_isPlayerDrawTurn || _isPlayerTurn)) || _forceShowDrawPanel || IsDrawCompletePhase) &&
    IsParticipatingInHand;

    private bool ShouldShowDrawWaitingIndicator =>
    IsDrawingPhase &&
    !IsTollboothOfferPhase &&
    !(_isPlayerDrawTurn || _isPlayerTurn) &&
    _currentDrawPlayer is not null;

    private bool ShouldShowWaitingForPlayersOverlay =>
    !_isPausedForChipCheck &&
    _gameResponse?.CurrentPhase == GamePhase.WaitingForPlayers.ToString() &&
    (!_isSeated || IsSittingOut || !_isReady);

    private bool ShouldShowPausedOverlay =>
    _isPaused &&
    _gameResponse is not null &&
    _gameResponse.CurrentPhase != GamePhase.WaitingForPlayers.ToString() &&
    _gameResponse.CurrentPhase != GamePhase.Ended.ToString() &&
    _gameResponse.CurrentPhase != GamePhase.WaitingToStart.ToString() &&
    _gameResponse.CurrentPhase != GamePhase.WaitingForDealerChoice.ToString();
}
