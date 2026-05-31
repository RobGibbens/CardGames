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
    // Enums and Models
    public enum GamePhase
    {
        [Description("Waiting for Players")] WaitingForPlayers,
        [Description("Betting Round")] Dealing,
        [Description("Pre-Draw")] PreDraw,
        [Description("Drawing")] Drawing,
        [Description("Post-Draw")] PostDraw,
        [Description("Showdown")] Showdown,
        [Description("Ended")] Ended,
        [Description("Waiting to Start")] WaitingToStart,
        [Description("Complete")] Complete,
        [Description("Second Betting Round")] SecondBettingRound,
        [Description("Draw Phase")] DrawPhase,
        [Description("First Betting Round")] FirstBettingRound,
        [Description("Collecting Antes")] CollectingAntes,
        [Description("Drop or Stay")] DropOrStay,
        [Description("Pot Matching")] PotMatching,
        [Description("Waiting for Dealer Choice")] WaitingForDealerChoice,
    }

    public record SeatInfo
    {
        public int SeatIndex { get; init; }
        public bool IsOccupied { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerFirstName { get; set; }
        public string? PlayerAvatarUrl { get; set; }
        public int Chips { get; set; }
        public bool IsReady { get; set; }
        public bool IsCurrentPlayer { get; set; }
        public bool IsFolded { get; set; }
        public bool IsAllIn { get; set; }
        public bool IsDisconnected { get; set; }
        public bool IsSittingOut { get; set; }
        public string? SittingOutReason { get; set; }
        public int CurrentBet { get; set; }
        public bool HasDecidedDropOrStay { get; set; }
        public List<CardInfo> Cards { get; set; } = [];
        public string? HandEvaluationDescription { get; set; }
        /// <summary>
        /// The last action performed by this player (e.g., "Checked", "Raised 50").
        /// Displayed temporarily in the seat pill.
        /// </summary>
        public string? LastActionDescription { get; set; }
        /// <summary>
        /// The UTC time when the last action was performed.
        /// Used to determine when to hide the action display.
        /// </summary>
        public DateTimeOffset? LastActionTime { get; set; }
    }

    public record CardInfo
    {
        public string? Rank { get; init; }
        public string? Suit { get; init; }
        public bool IsFaceUp { get; init; }
        public bool IsPubliclyVisible { get; init; }
        public bool IsSelected { get; init; }
        public int DealOrder { get; init; }
        public bool IsWild { get; init; }
        public bool IsShowcaseCard { get; init; }
    }

    public record PlayerActionRequest(BettingActionType Action, int? Amount);

    public record ToastMessage(string Message, string Type);
}
