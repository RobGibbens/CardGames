# Web Game Router Design

This document describes the **active** web-side game router used by the Blazor Server
app: `GameApiRouter` (interface `IGameApiRouter`) in
`src/CardGames.Poker.Web/Services/IGameApiRouter.cs`.

It is the single component the web app uses to turn a player action plus a game code
into the correct backend API call. There is no longer a wrapper-based router stack; this
dispatch-table router is the only router.

## Responsibilities

For a given game code (for example `"HOLDEM"`) the router forwards each player action to
the right Refit API client and normalizes the response into a `RouterResponse<T>`:

- `ProcessBettingActionAsync`
- `ProcessDrawAsync`
- `DropOrStayAsync`
- `KeepOrTradeAsync`
- `ProcessBuyCardAsync`
- `AcknowledgePotMatchAsync`
- plus a few game-specific actions exposed directly (Tollbooth choose-card, In-Between
  ace-choice / place-bet, fold-during-draw).

## How it works

Routing uses **one case-insensitive dispatch dictionary per action kind**, built once in
the constructor:

| Dictionary | Action | Missing-entry behavior |
|------------|--------|------------------------|
| `_bettingActionRoutes` | Betting | Throws `NotSupportedException` (required for every game) |
| `_drawRoutes` | Draw / discard | Throws `NotSupportedException` (required for every game) |
| `_dropOrStayRoutes` | Drop or stay | Returns `RouterResponse.Failure` (optional) |
| `_keepOrTradeRoutes` | Keep or trade | Returns `RouterResponse.Failure` (optional) |
| `_buyCardRoutes` | Buy card | Returns `RouterResponse.Failure` (optional) |
| `_acknowledgePotMatchRoutes` | Acknowledge pot match | Returns `RouterResponse.Failure` (optional) |

Each dictionary maps a game-code constant to a small `Route…Async` method. The method
calls the relevant API and converts the Refit `IApiResponse` into a `RouterResponse<T>`
via `RouterResponse<T>.FromRefit(...)`.

Because the maps are the single source of truth, **to audit what a game supports, search
the dictionaries for its game-code constant.** Required actions (betting, draw) fail loudly
on a missing entry so gaps are obvious; optional actions return a friendly failure because
most games legitimately do not support them.

Hold 'Em-family variants (Red River, Omaha, Nebraska, South Dakota, Bob Barker, Irish
Hold 'Em, Phil's Mom, Crazy Pineapple, Hold the Baseball, Klondike) share the Hold 'Em
betting endpoint, so they all point at the single `RouteHoldEmBettingActionAsync` method
rather than duplicating one method per variant.

## Adding a new game variant

To wire a variant up consistently in every place it is needed:

1. Add a `private const string` game-code constant.
2. Add a `Route…Async` method, or reuse an existing one (Hold 'Em-family variants reuse
   `RouteHoldEmBettingActionAsync`).
3. Register the variant in **every** action dictionary it participates in. Omitting an
   entry is the most common cause of "works for some actions but not others" bugs.
4. Add or update a test in `GameApiRouterTests`
   (`src/Tests/CardGames.Poker.Tests/Web/GameApiRouterTests.cs`) asserting the new mapping.

The same design notes are duplicated as XML/code comments on `GameApiRouter` so they are
visible directly where the routing lives.
