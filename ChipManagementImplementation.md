# Chip Management Dashboard - Detailed Implementation Plan

## Overview
This document provides a detailed, step-by-step implementation plan for adding a chip management dashboard section to the CardGames.Poker application. The feature allows players to view their current chip stack, add chips (with game-state restrictions), and visualize chip history through an interactive chart.

## Architecture Summary

### Components Structure
```
CardGames.Poker.Api/
├── Features/Games/Common/v1/Commands/AddChips/
│   ├── AddChipsCommand.cs
│   ├── AddChipsCommandHandler.cs
│   ├── AddChipsEndpoint.cs
│   ├── AddChipsRequest.cs
│   ├── AddChipsResponse.cs
│   └── AddChipsError.cs
├── Services/
│   └── GameStateBroadcaster.cs (modified)
└── Data/Entities/
    └── GamePlayer.cs (modified)

CardGames.Contracts/
└── SignalR/
    ├── ChipHistoryDto.cs (new)
    ├── PrivateStateDto.cs (modified)
    └── ChipsAddedDto.cs (new)

CardGames.Poker.Web/
├── Components/Shared/
│   └── ChipManagementSection.razor (new)
├── Components/Pages/
│   └── TablePlay.razor (modified)
└── Services/
    └── DashboardState.cs (modified)
```

## Implementation Steps

### Phase 1: Backend - Database and Entity Changes

#### 1.1 Extend GamePlayer Entity
**File**: `src/CardGames.Poker.Api/Data/Entities/GamePlayer.cs`

**Changes**:
- Add `PendingChipsToAdd` property (int, default: 0)
- This property stores chips that are queued to be added when the game reaches the appropriate state

**Code Addition**:
```csharp
/// <summary>
/// Chips pending to be added to the player's stack.
/// Applied automatically when the game status reaches BetweenHands for certain game types.
/// </summary>
public int PendingChipsToAdd { get; set; }
```

**Location**: After line 118 (after `FinalChipCount` property)

**Migration**: A new database migration will be required
```bash
dotnet ef migrations add AddPendingChipsToGamePlayer -p src/CardGames.Poker.Api
```

---

### Phase 2: Backend - DTOs and Contracts

#### 2.1 Create ChipHistoryDto
**File**: `src/CardGames.Contracts/SignalR/ChipHistoryDto.cs` (NEW)

**Purpose**: Encapsulate chip history data for transmission to the client

**Structure**:
```csharp
namespace CardGames.Contracts.SignalR;

/// <summary>
/// Represents a single entry in the chip history tracking.
/// </summary>
public sealed record ChipHistoryEntryDto
{
    /// <summary>
    /// The 1-based hand number within the game.
    /// </summary>
    public required int HandNumber { get; init; }

    /// <summary>
    /// The player's chip stack at the end of this hand.
    /// </summary>
    public required int ChipStackAfterHand { get; init; }

    /// <summary>
    /// The net chip change for this hand (positive = won, negative = lost).
    /// </summary>
    public required int ChipsDelta { get; init; }

    /// <summary>
    /// The UTC timestamp when the hand completed.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Contains chip history and current chip state for a player.
/// </summary>
public sealed record ChipHistoryDto
{
    /// <summary>
    /// The player's current chip stack.
    /// </summary>
    public required int CurrentChips { get; init; }

    /// <summary>
    /// Chips pending to be added (queued until BetweenHands state for applicable game types).
    /// </summary>
    public int PendingChipsToAdd { get; init; }

    /// <summary>
    /// The player's starting chip stack for this game session.
    /// </summary>
    public required int StartingChips { get; init; }

    /// <summary>
    /// History of chip changes for the last 30 hands.
    /// Sorted chronologically (oldest to newest).
    /// </summary>
    public required IReadOnlyList<ChipHistoryEntryDto> History { get; init; }
}
```

#### 2.2 Create ChipsAddedDto
**File**: `src/CardGames.Contracts/SignalR/ChipsAddedDto.cs` (NEW)

**Purpose**: Event notification when chips are added or queued

**Structure**:
```csharp
namespace CardGames.Contracts.SignalR;

/// <summary>
/// Notification sent when chips are added or queued for a player.
/// </summary>
public sealed record ChipsAddedDto
{
    /// <summary>
    /// The game ID where chips were added.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The player name who received chips.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The amount of chips added.
    /// </summary>
    public required int Amount { get; init; }

    /// <summary>
    /// Whether chips were immediately added (true) or queued (false).
    /// </summary>
    public bool AppliedImmediately { get; init; }

    /// <summary>
    /// User-friendly message explaining when chips will be applied.
    /// </summary>
    public string? Message { get; init; }
}
```

#### 2.3 Extend PrivateStateDto
**File**: `src/CardGames.Contracts/SignalR/PrivateStateDto.cs`

**Changes**:
- Add `ChipHistory` property (ChipHistoryDto?)
- This provides the player with their personalized chip tracking data

**Code Addition**:
```csharp
/// <summary>
/// Chip history and current chip state for this player.
/// </summary>
public ChipHistoryDto? ChipHistory { get; init; }
```

**Location**: After line 62 (after `HandHistory` property)

---

### Phase 3: Backend - Command Implementation

#### 3.1 Create AddChipsCommand
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsCommand.cs` (NEW)

**Structure**:
```csharp
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Command to add chips to a player's stack in a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The player's unique identifier.</param>
/// <param name="Amount">The amount of chips to add (must be positive).</param>
public record AddChipsCommand(Guid GameId, Guid PlayerId, int Amount)
    : IRequest<OneOf<AddChipsResponse, AddChipsError>>, IGameStateChangingCommand;
```

#### 3.2 Create AddChipsRequest
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsRequest.cs` (NEW)

**Structure**:
```csharp
using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Request to add chips to a player's stack.
/// </summary>
public sealed record AddChipsRequest
{
    /// <summary>
    /// The amount of chips to add (must be positive).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public required int Amount { get; init; }
}
```

#### 3.3 Create AddChipsResponse
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsResponse.cs` (NEW)

**Structure**:
```csharp
namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Successful response when chips are added.
/// </summary>
public sealed record AddChipsResponse
{
    /// <summary>
    /// The updated chip stack (may not include the added amount if queued).
    /// </summary>
    public required int NewChipStack { get; init; }

    /// <summary>
    /// The amount of pending chips waiting to be applied.
    /// </summary>
    public int PendingChipsToAdd { get; init; }

    /// <summary>
    /// Whether the chips were applied immediately.
    /// </summary>
    public bool AppliedImmediately { get; init; }

    /// <summary>
    /// Message explaining the result.
    /// </summary>
    public required string Message { get; init; }
}
```

#### 3.4 Create AddChipsError
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsError.cs` (NEW)

**Structure**:
```csharp
namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Error codes for AddChips command failures.
/// </summary>
public enum AddChipsErrorCode
{
    /// <summary>
    /// The game was not found.
    /// </summary>
    GameNotFound,

    /// <summary>
    /// The player is not part of this game.
    /// </summary>
    PlayerNotInGame,

    /// <summary>
    /// The amount provided is invalid (e.g., zero or negative).
    /// </summary>
    InvalidAmount,

    /// <summary>
    /// Cannot add chips because the game has ended.
    /// </summary>
    GameEnded
}

/// <summary>
/// Error response for AddChips command.
/// </summary>
public sealed record AddChipsError(AddChipsErrorCode Code, string Message);
```

#### 3.5 Create AddChipsCommandHandler
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsCommandHandler.cs` (NEW)

**Responsibilities**:
1. Validate the game exists and player is part of the game
2. Validate amount is positive
3. Check game type to determine immediate vs. queued chip addition:
   - **Kings and Lows** (`GameTypeCode == "KINGSANDLOWS"`): Add chips immediately
   - **Other games** (Five Card Draw, Seven Card Stud, Twos Jacks Man with the Axe): 
     - If `GameStatus == BetweenHands`: Add immediately
     - Otherwise: Queue chips in `PendingChipsToAdd`
4. Update database
5. Broadcast state via `IGameStateBroadcaster`

**Key Logic**:
```csharp
// Determine if chips should be applied immediately
bool applyImmediately = string.Equals(game.GameType.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase)
    || string.Equals(game.CurrentPhase, "BetweenHands", StringComparison.OrdinalIgnoreCase);

if (applyImmediately)
{
    gamePlayer.ChipStack += command.Amount;
    message = $"{command.Amount} chips added to your stack.";
}
else
{
    gamePlayer.PendingChipsToAdd += command.Amount;
    message = $"{command.Amount} chips will be added at the start of the next hand.";
}
```

**Dependencies**:
- `CardsDbContext`
- `ICurrentUserService`
- `IGameStateBroadcaster`
- `ILogger<AddChipsCommandHandler>`

#### 3.6 Create AddChipsEndpoint
**File**: `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsEndpoint.cs` (NEW)

**Route**: `POST /api/v1/games/{gameId}/players/{playerId}/add-chips`

**Structure**:
```csharp
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Endpoint for adding chips to a player's stack.
/// </summary>
public sealed class AddChipsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/games/{gameId:guid}/players/{playerId:guid}/add-chips",
            async (
                [FromRoute] Guid gameId,
                [FromRoute] Guid playerId,
                [FromBody] AddChipsRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new AddChipsCommand(gameId, playerId, request.Amount);
                var result = await mediator.Send(command, cancellationToken);

                return result.Match<IResult>(
                    success => Results.Ok(success),
                    error => error.Code switch
                    {
                        AddChipsErrorCode.GameNotFound => Results.NotFound(error.Message),
                        AddChipsErrorCode.PlayerNotInGame => Results.BadRequest(error.Message),
                        AddChipsErrorCode.InvalidAmount => Results.BadRequest(error.Message),
                        AddChipsErrorCode.GameEnded => Results.BadRequest(error.Message),
                        _ => Results.Problem(error.Message)
                    });
            })
            .RequireAuthorization()
            .WithTags("Games")
            .WithName("AddChips")
            .WithOpenApi();
    }
}
```

---

### Phase 4: Backend - Broadcasting and State Building

#### 4.1 Update GameStateBroadcaster
**File**: `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`

**Changes**:
1. Add `ChipsAdded` event method to broadcast chip additions
2. Ensure `PrivateStateUpdated` includes chip history data

**New Method**:
```csharp
/// <summary>
/// Broadcasts a chips added notification to the specific player.
/// </summary>
public async Task BroadcastChipsAddedAsync(ChipsAddedDto notification)
{
    var playerGroup = $"player:{notification.GameId}:{notification.PlayerName}";
    
    await _hubContext.Clients
        .Group(playerGroup)
        .SendAsync("ChipsAdded", notification);
    
    _logger.LogInformation(
        "Broadcasted chips added: {Amount} chips for player {PlayerName} in game {GameId}",
        notification.Amount,
        notification.PlayerName,
        notification.GameId);
}
```

#### 4.2 Update TableStateBuilder
**File**: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

**Changes**:
1. Add logic to build `ChipHistoryDto` from `HandHistory` data
2. Include `ChipHistory` in `PrivateStateDto` response

**Helper Method**:
```csharp
private ChipHistoryDto BuildChipHistory(GamePlayer gamePlayer, IReadOnlyList<HandHistoryEntryDto> handHistory)
{
    var entries = new List<ChipHistoryEntryDto>();
    var currentStack = gamePlayer.StartingChips;

    // Take last 30 hands
    foreach (var hand in handHistory.TakeLast(30))
    {
        var playerResult = hand.PlayerResults.FirstOrDefault(pr => pr.PlayerId == gamePlayer.PlayerId);
        if (playerResult != null)
        {
            currentStack += playerResult.NetAmount;
            entries.Add(new ChipHistoryEntryDto
            {
                HandNumber = hand.HandNumber,
                ChipStackAfterHand = currentStack,
                ChipsDelta = playerResult.NetAmount,
                Timestamp = hand.CompletedAtUtc
            });
        }
    }

    return new ChipHistoryDto
    {
        CurrentChips = gamePlayer.ChipStack,
        PendingChipsToAdd = gamePlayer.PendingChipsToAdd,
        StartingChips = gamePlayer.StartingChips,
        History = entries
    };
}
```

#### 4.3 Apply Pending Chips Logic
**File**: Update hand start logic in game engines

**Location**: In each game's hand start method (e.g., `FiveCardDrawEngine`, `SevenCardStudEngine`, etc.)

**Logic**: When starting a new hand and game is in `BetweenHands` state:
```csharp
// Apply pending chips
foreach (var player in activePlayers)
{
    if (player.PendingChipsToAdd > 0)
    {
        player.ChipStack += player.PendingChipsToAdd;
        player.PendingChipsToAdd = 0;
    }
}
```

---

### Phase 5: Frontend - NuGet Package Installation

#### 5.1 Add Blazor-ApexCharts Package
**File**: `src/CardGames.Poker.Web/CardGames.Poker.Web.csproj`

**Command**:
```bash
cd src/CardGames.Poker.Web
dotnet add package Blazor-ApexCharts
```

**Expected Package Version**: Latest stable (e.g., 3.x)

#### 5.2 Register ApexCharts Services
**File**: `src/CardGames.Poker.Web/Program.cs`

**Addition**:
```csharp
// Add ApexCharts services
builder.Services.AddApexCharts();
```

**Location**: After other service registrations, before `var app = builder.Build();`

#### 5.3 Add ApexCharts Scripts and Styles
**File**: `src/CardGames.Poker.Web/Components/App.razor` or `_Host.cshtml`

**Addition**:
```html
<!-- ApexCharts -->
<link rel="stylesheet" href="_content/Blazor-ApexCharts/css/apexcharts.css">
<script src="_content/Blazor-ApexCharts/js/apex-charts.js"></script>
<script src="_content/Blazor-ApexCharts/js/blazor-apexcharts.js"></script>
```

**Location**: In the `<head>` section

---

### Phase 6: Frontend - Blazor Component

#### 6.1 Create ChipManagementSection.razor
**File**: `src/CardGames.Poker.Web/Components/Shared/ChipManagementSection.razor` (NEW)

**Structure**:
```razor
@namespace CardGames.Poker.Web.Components.Shared
@using ApexCharts
@using CardGames.Contracts.SignalR

@* Chip Management Section - Displays chip stack, add chips interface, and history chart *@

<div class="chip-management-section">
    <!-- Current Chip Stack Display -->
    <div class="chip-stack-display">
        <div class="chip-stack-label">Current Stack:</div>
        <div class="chip-stack-value">
            @ChipHistory?.CurrentChips.ToString("N0")
            @if (ChipHistory?.PendingChipsToAdd > 0)
            {
                <span class="pending-chips">(+@ChipHistory.PendingChipsToAdd.ToString("N0") pending)</span>
            }
        </div>
    </div>

    <!-- Add Chips Interface -->
    <div class="add-chips-form">
        <div class="form-group">
            <label for="chip-amount">Add Chips:</label>
            <input type="number" 
                   id="chip-amount" 
                   class="form-control" 
                   @bind="chipAmount" 
                   min="1" 
                   step="100" 
                   placeholder="Enter amount" />
        </div>
        <button class="btn btn-primary" 
                @onclick="AddChipsAsync" 
                disabled="@(isSubmitting || chipAmount <= 0)">
            <i class="fa-solid fa-plus"></i>
            Add Chips
        </button>
    </div>

    <!-- Info Message -->
    @if (!string.IsNullOrEmpty(infoMessage))
    {
        <div class="info-message @messageType">
            <i class="fa-solid @(messageType == "success" ? "fa-circle-check" : "fa-circle-info")"></i>
            @infoMessage
        </div>
    }

    <!-- Game Type Explanation -->
    <div class="chip-timing-info">
        @if (IsKingsAndLows)
        {
            <i class="fa-solid fa-info-circle"></i>
            <span>Chips are added immediately in Kings and Lows.</span>
        }
        else
        {
            <i class="fa-solid fa-clock"></i>
            <span>Chips will be added at the start of the next hand.</span>
        }
    </div>

    <!-- Chip History Chart -->
    @if (ChipHistory?.History.Any() == true)
    {
        <div class="chip-history-chart">
            <h4>Chip History (Last 30 Hands)</h4>
            <ApexChart TItem="ChipHistoryEntryDto"
                       Title="Chip Stack Over Time"
                       Options="chartOptions"
                       Height="300">
                <ApexPointSeries TItem="ChipHistoryEntryDto"
                                 Items="ChipHistory.History"
                                 Name="Chip Stack"
                                 SeriesType="SeriesType.Line"
                                 XValue="@(e => e.HandNumber)"
                                 YValue="@(e => (decimal)e.ChipStackAfterHand)"
                                 OrderBy="e => e.HandNumber" />
            </ApexChart>
        </div>
    }
    else
    {
        <div class="no-history">
            <i class="fa-regular fa-chart-line"></i>
            <span>No chip history yet. Play some hands to see your progress!</span>
        </div>
    }
</div>

@code {
    [Parameter]
    public ChipHistoryDto? ChipHistory { get; set; }

    [Parameter]
    public Guid GameId { get; set; }

    [Parameter]
    public Guid PlayerId { get; set; }

    [Parameter]
    public string? GameTypeCode { get; set; }

    [Parameter]
    public EventCallback OnChipsAdded { get; set; }

    [Inject]
    private IGamesApi GamesApiClient { get; set; } = null!;

    [Inject]
    private ILogger<ChipManagementSection> Logger { get; set; } = null!;

    private int chipAmount = 500;
    private bool isSubmitting;
    private string? infoMessage;
    private string messageType = "info";

    private bool IsKingsAndLows => 
        string.Equals(GameTypeCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);

    private ApexChartOptions<ChipHistoryEntryDto> chartOptions = new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false }
        },
        Stroke = new Stroke
        {
            Curve = Curve.Smooth,
            Width = 3
        },
        Colors = new List<string> { "#10b981" }, // Green color
        Xaxis = new XAxis
        {
            Title = new AxisTitle { Text = "Hand Number" },
            Type = XAxisType.Numeric
        },
        Yaxis = new List<YAxis>
        {
            new YAxis
            {
                Title = new AxisTitle { Text = "Chip Stack" },
                Labels = new YAxisLabels
                {
                    Formatter = "function(value) { return value.toLocaleString(); }"
                }
            }
        },
        Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = "function(value) { return value.toLocaleString() + ' chips'; }"
            }
        }
    };

    private async Task AddChipsAsync()
    {
        if (chipAmount <= 0)
        {
            infoMessage = "Please enter a positive amount.";
            messageType = "error";
            return;
        }

        isSubmitting = true;
        infoMessage = null;

        try
        {
            var request = new AddChipsRequest { Amount = chipAmount };
            var result = await GamesApiClient.AddChipsAsync(GameId, PlayerId, request);

            infoMessage = result.Message;
            messageType = "success";
            chipAmount = 500; // Reset to default

            // Notify parent component
            await OnChipsAdded.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add chips");
            infoMessage = "Failed to add chips. Please try again.";
            messageType = "error";
        }
        finally
        {
            isSubmitting = false;
        }
    }
}
```

#### 6.2 Create ChipManagementSection.razor.css
**File**: `src/CardGames.Poker.Web/Components/Shared/ChipManagementSection.razor.css` (NEW)

**Styles**:
```css
.chip-management-section {
    padding: 1rem;
}

.chip-stack-display {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1rem;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    border-radius: 8px;
    color: white;
    margin-bottom: 1rem;
}

.chip-stack-label {
    font-size: 0.875rem;
    opacity: 0.9;
    font-weight: 500;
}

.chip-stack-value {
    font-size: 1.5rem;
    font-weight: 700;
}

.pending-chips {
    font-size: 0.875rem;
    opacity: 0.85;
    margin-left: 0.5rem;
}

.add-chips-form {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 1rem;
}

.add-chips-form .form-group {
    flex: 1;
}

.add-chips-form input {
    width: 100%;
}

.add-chips-form button {
    white-space: nowrap;
}

.info-message {
    padding: 0.75rem;
    border-radius: 6px;
    margin-bottom: 1rem;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.875rem;
}

.info-message.success {
    background-color: #d1fae5;
    color: #065f46;
    border: 1px solid #6ee7b7;
}

.info-message.error {
    background-color: #fee2e2;
    color: #991b1b;
    border: 1px solid #fca5a5;
}

.chip-timing-info {
    padding: 0.75rem;
    background-color: #f3f4f6;
    border-radius: 6px;
    margin-bottom: 1rem;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.875rem;
    color: #4b5563;
}

.chip-history-chart {
    margin-top: 1.5rem;
}

.chip-history-chart h4 {
    font-size: 1rem;
    font-weight: 600;
    margin-bottom: 1rem;
    color: #374151;
}

.no-history {
    padding: 2rem;
    text-align: center;
    color: #9ca3af;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
}

.no-history i {
    font-size: 2rem;
    opacity: 0.5;
}
```

---

### Phase 7: Frontend - Integration

#### 7.1 Update DashboardState
**File**: `src/CardGames.Poker.Web/Services/DashboardState.cs`

**Changes**:
- Add "Chips" to `_sectionStates` dictionary with default `true`

**Code Modification**:
```csharp
private readonly Dictionary<string, bool> _sectionStates = new()
{
    ["Chips"] = true,          // NEW - Add this line first
    ["Leaderboard"] = true,
    ["Odds"] = true,
    ["HandHistory"] = true
};
```

#### 7.2 Integrate into TablePlay.razor
**File**: `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`

**Changes**:
1. Add `ChipManagementSection` as the first section inside `DashboardPanel`
2. Pass required parameters from SignalR state

**Code Addition** (before Leaderboard section):
```razor
<DashboardPanel>
    <!-- Chips Section - NEW -->
    <DashboardSection Title="Chips" Icon="fa-solid fa-coins"
                      IsExpanded="@DashboardState.IsSectionExpanded("Chips")"
                      IsExpandedChanged="@(value => DashboardState.SetSectionExpanded("Chips", value))">
        <ChipManagementSection ChipHistory="@_privateState?.ChipHistory"
                               GameId="@GameId"
                               PlayerId="@_currentPlayerId"
                               GameTypeCode="@_gameResponse?.GameTypeCode"
                               OnChipsAdded="@OnChipsAddedAsync" />
    </DashboardSection>

    <!-- Existing Leaderboard Section -->
    <DashboardSection Title="Leaderboard" Icon="fa-solid fa-trophy"
                      IsExpanded="@DashboardState.IsSectionExpanded("Leaderboard")"
                      IsExpandedChanged="@(value => DashboardState.SetSectionExpanded("Leaderboard", value))">
        <LeaderboardSection Players="@GetLeaderboardPlayers()" />
    </DashboardSection>
    
    <!-- ... rest of sections ... -->
</DashboardPanel>
```

**Code Addition** (in @code section):
```csharp
private async Task OnChipsAddedAsync()
{
    // Refresh state after chips are added
    // State will be automatically updated via SignalR PrivateStateUpdated event
    await Task.CompletedTask;
}
```

#### 7.3 Update GameHubClient
**File**: `src/CardGames.Poker.Web/Services/GameHubClient.cs` (or wherever SignalR client is defined)

**Changes**:
- Add handler for `ChipsAdded` event to show toast notifications

**Code Addition**:
```csharp
_hubConnection.On<ChipsAddedDto>("ChipsAdded", async (notification) =>
{
    Logger.LogInformation("Chips added: {Amount} for player {PlayerName}", 
        notification.Amount, notification.PlayerName);
    
    // Show toast notification
    await InvokeAsync(() =>
    {
        ShowToast(notification.Message ?? "Chips added successfully!", "success");
    });
});
```

---

### Phase 8: API Client Generation

#### 8.1 Update Refitter Interface
**File**: `src/CardGames.Contracts/RefitInterface.v1.cs` or similar

**Addition**:
```csharp
/// <summary>
/// Add chips to a player's stack in a game.
/// </summary>
[Post("/api/v1/games/{gameId}/players/{playerId}/add-chips")]
Task<AddChipsResponse> AddChipsAsync(
    Guid gameId, 
    Guid playerId, 
    [Body] AddChipsRequest request, 
    CancellationToken cancellationToken = default);
```

#### 8.2 Regenerate API Clients
**Command**:
```bash
cd src/CardGames.Poker.Refitter
dotnet build
```

---

## Testing Strategy

### Unit Tests

#### Backend Tests
**File**: `src/Tests/CardGames.Poker.Tests/Features/Games/Common/v1/Commands/AddChips/AddChipsCommandHandlerTests.cs`

**Test Cases**:
1. `AddChips_KingsAndLows_AppliesImmediately()`
2. `AddChips_OtherGames_BetweenHands_AppliesImmediately()`
3. `AddChips_OtherGames_DuringHand_QueuesChips()`
4. `AddChips_InvalidAmount_ReturnsError()`
5. `AddChips_PlayerNotInGame_ReturnsError()`
6. `AddChips_GameNotFound_ReturnsError()`
7. `AddChips_GameEnded_ReturnsError()`

#### Frontend Tests
**File**: `src/Tests/CardGames.Poker.Web.Tests/Components/Shared/ChipManagementSectionTests.cs` (if test infrastructure exists)

**Test Cases**:
1. Component renders with chip history
2. Component renders without chip history
3. Add chips button triggers API call
4. Validation prevents negative amounts
5. Pending chips display correctly

### Integration Tests

**File**: `src/Tests/CardGames.Poker.Tests/Integration/ChipManagementIntegrationTests.cs`

**Test Cases**:
1. End-to-end chip addition flow for Kings and Lows
2. End-to-end chip addition with queueing for Five Card Draw
3. Pending chips are applied at hand start
4. Chip history is correctly populated from hand results

### Manual Testing Checklist

- [ ] Start a Kings and Lows game, add chips, verify immediate addition
- [ ] Start a Five Card Draw game, add chips during hand, verify queuing
- [ ] Complete hands and verify chip history chart updates
- [ ] Verify pending chips show in UI correctly
- [ ] Test chart scrolling with more than 30 hands
- [ ] Test responsive design on mobile
- [ ] Verify SignalR notifications appear as toasts
- [ ] Test with multiple players adding chips simultaneously

---

## Database Migration

After completing Phase 1.1:

```bash
# Create migration
cd src/CardGames.Poker.Api
dotnet ef migrations add AddPendingChipsToGamePlayer

# Apply migration to database
dotnet ef database update
```

---

## Rollout Plan

### Phase Deployment Order

1. **Database Changes** (Phase 1)
   - Deploy migration to add `PendingChipsToAdd` column
   - Verify migration success

2. **Backend DTOs and Contracts** (Phase 2)
   - Deploy contracts assembly
   - No downtime required

3. **Backend Command Implementation** (Phase 3)
   - Deploy API with new endpoint
   - Backend is ready but not yet utilized

4. **Broadcasting and State** (Phase 4)
   - Deploy updated broadcasters and state builders
   - Chip history starts populating for new hands

5. **Frontend Package** (Phase 5)
   - Install NuGet packages
   - No user-facing changes yet

6. **Frontend Component** (Phase 6)
   - Deploy new Blazor component
   - Component exists but not visible yet

7. **Frontend Integration** (Phase 7)
   - Deploy integration changes
   - Feature is now live and visible to users

8. **API Client Generation** (Phase 8)
   - Regenerate and deploy API clients
   - Feature fully operational

---

## Configuration

### Feature Flags (Optional)

Consider adding a feature flag to control chip management visibility:

```json
{
  "FeatureFlags": {
    "ChipManagement": {
      "Enabled": true,
      "AllowChipAddition": true,
      "MaxChipsPerAdd": 10000,
      "HistoryDepth": 30
    }
  }
}
```

---

## Monitoring and Logging

### Key Metrics to Track

1. **Chip Addition Frequency**: How often players add chips
2. **Average Amount Added**: Typical chip addition amounts
3. **Queue vs. Immediate**: Ratio of queued to immediate chip additions
4. **Chart Rendering Performance**: Load time for chip history charts
5. **API Response Times**: Performance of AddChips endpoint

### Log Events

- `ChipsAddedImmediately`: Log when chips are added immediately
- `ChipsQueued`: Log when chips are queued
- `PendingChipsApplied`: Log when queued chips are applied
- `ChipHistoryBuilt`: Log when chip history is constructed
- `AddChipsError`: Log any errors in chip addition process

---

## Potential Issues and Mitigations

### Issue 1: Race Conditions
**Problem**: Multiple chip additions in rapid succession
**Mitigation**: Use optimistic concurrency with `RowVersion` on `GamePlayer` entity

### Issue 2: Large Chip History
**Problem**: Games with hundreds of hands could have large history
**Mitigation**: Only load last 30 hands, implement pagination if needed

### Issue 3: SignalR Disconnection
**Problem**: Chip addition during disconnection
**Mitigation**: Implement retry logic and show connection status banner

### Issue 4: Chart Performance
**Problem**: ApexCharts rendering lag with frequent updates
**Mitigation**: Throttle chart updates, use `ShouldRender()` optimization

---

## Future Enhancements

1. **Chip Purchase Confirmation Modal**: Add a confirmation dialog before adding chips
2. **Chip Addition History**: Separate log of all chip addition events
3. **Chip Limits**: Configurable min/max chip addition limits per game
4. **Auto-Rebuy**: Automatically add chips when stack falls below threshold
5. **Chart Export**: Allow players to export chip history as CSV/image
6. **Multiple Chart Views**: Toggle between line chart, bar chart, area chart
7. **Comparative Analytics**: Compare chip stack against other players over time
8. **Chip Achievement Badges**: Award badges for chip milestones

---

## Dependencies and Prerequisites

### Backend Dependencies
- Entity Framework Core (already installed)
- MediatR (already installed)
- SignalR (already installed)

### Frontend Dependencies
- Blazor-ApexCharts (to be installed)
- Refit (already installed for API clients)

### Development Tools
- .NET 8 SDK
- Entity Framework Core CLI tools
- Node.js (for any frontend build tools)

---

## Acceptance Criteria

- [ ] Players can view their current chip stack with pending chips inline
- [ ] Players can add chips through a validated input form
- [ ] Kings and Lows games apply chips immediately
- [ ] Other games queue chips until the next hand
- [ ] Chip history chart displays last 30 hands with scrolling
- [ ] Chart uses ApexCharts library with smooth line rendering
- [ ] Pending chips are automatically applied at hand start
- [ ] SignalR notifications appear as toast messages
- [ ] Section is collapsible and persists state
- [ ] Section appears as the first item in the dashboard
- [ ] Mobile responsive design
- [ ] All backend validations in place (positive amounts, valid game, etc.)
- [ ] Database migration successful with no data loss
- [ ] Unit tests pass with >80% coverage
- [ ] Integration tests validate end-to-end flow
- [ ] Manual testing checklist completed

---

## Timeline Estimate

| Phase | Estimated Time | Dependencies |
|-------|---------------|--------------|
| Phase 1: Database & Entity | 1-2 hours | None |
| Phase 2: DTOs & Contracts | 2-3 hours | Phase 1 |
| Phase 3: Command Implementation | 4-6 hours | Phase 2 |
| Phase 4: Broadcasting & State | 3-4 hours | Phase 3 |
| Phase 5: NuGet Package | 0.5-1 hour | None |
| Phase 6: Blazor Component | 4-6 hours | Phase 5 |
| Phase 7: Frontend Integration | 2-3 hours | Phase 6 |
| Phase 8: API Client Generation | 0.5-1 hour | Phase 3 |
| Testing & Refinement | 4-6 hours | All phases |
| **Total** | **21-32 hours** | |

---

## Conclusion

This implementation plan provides a comprehensive, step-by-step guide to adding the chip management dashboard feature. The plan follows the existing architecture patterns in the CardGames.Poker application and ensures that the feature is built with proper separation of concerns, testability, and maintainability.

Key design decisions:
- **Immediate vs. Queued**: Different game types have different chip addition timing rules
- **Chart Library**: Blazor-ApexCharts provides a mature, feature-rich charting solution
- **History Depth**: 30 hands provides adequate history without overwhelming the UI
- **State Management**: Leveraging existing SignalR infrastructure for real-time updates

The feature is designed to be incrementally deployable, allowing for testing at each phase before moving to the next.
