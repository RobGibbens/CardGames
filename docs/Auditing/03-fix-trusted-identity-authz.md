# Fix plan: stop trusting client-supplied identity for private state and table actions

**Audit finding:** “Hole-card leak and acting for others: player identity is taken on trust.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Authentication trust boundaries, authorization on game mutations, SignalR private state, and commands that accept `PlayerId` from the client.

---

## 1. Problem summary

The stack treats **who you are** as whatever the HTTP/SignalR client puts in **custom headers or query strings**. Game **mutations** mostly do **not** require authentication and do **not** bind actions to the authenticated seat. Together:

| Issue | What runs today | Impact |
|-------|-----------------|--------|
| **A. Spoofable authentication** | `HeaderAuthenticationHandler` builds a principal from `X-User-*` headers or `?userId=&userEmail=&authenticated=true` with **no cryptographic proof**. | Any caller who can reach the API can claim another user’s email/id. |
| **B. SignalR private state** | `GameHub.JoinGame` → `BuildPrivateStateAsync(gameId, Context.UserIdentifier)` with **no check** that the caller is seated at that table. | Spoofed email → victim’s hole cards in `PrivateStateUpdated`. |
| **C. Anonymous table actions** | Game minimal-API routes (e.g. Hold’em `POST .../betting/actions`) have **no** `.RequireAuthorization()`. `ProcessBettingActionCommand` has **no** `PlayerId`; handler uses `bettingRound.CurrentActorIndex` only. | Anyone can POST fold/check/bet for **whoever is currently to act**. |
| **D. Client-supplied `PlayerId`** | Several commands trust `request.PlayerId` without matching `ICurrentUserService`. | Act as another seated player on draw/special actions. |
| **E. Weak seat binding on chips** | `AddChipsCommandHandler` uses `command.PlayerId` with **no** `ICurrentUserService` check. | Add chips to arbitrary `PlayerId` at a table. |

**Exposure note:** In Aspire/dev, the API may be reachable on `localhost` or via service discovery (`https+http://api`). **CORS** (`SetIsOriginAllowed(_ => true)` + credentials) does not protect server-to-server or direct `curl`. Treat the API as **attacker-reachable** unless network policy restricts it.

**What already does better (patterns to reuse):**

- `LeaveGameCommandHandler` — requires `ICurrentUserService`, resolves `GamePlayer` by `currentUserName` / seat.
- `GetRabbitHuntQueryHandler` — `IsAuthenticated` + seated player lookup by email/name/`ExternalId`.
- `ToggleOddsVisibility` / `UpdateTableSettings` / `DeleteGame` — host/creator checks via `ICurrentUserService`.

---

## 2. Intended behavior (spec)

### 2.1 Authentication

1. **Production:** The API must **not** accept bare `X-User-*` headers from arbitrary clients.
2. **Legitimate path:** Blazor Web (authenticated via ASP.NET Core Identity / external login) calls the API with identity the API can **verify** (see §4).
3. **Unauthenticated** requests to game mutation endpoints → **401 Unauthorized** (not anonymous success).

### 2.2 Authorization — seated player

For any command that changes **this table’s** state on behalf of a human:

1. Caller is authenticated (§2.1).
2. Caller resolves to exactly one `GamePlayer` row for `command.GameId` with `Status != Left` (same matching rules as `LeaveGame` / `GetRabbitHunt`: email, name, `Player.ExternalId`).
3. If the command is **seat-scoped** (bet, draw, discard, etc.), `GamePlayer` must be the **current actor** (or allowed simultaneous-action participant — see §5.3).

Return **403 Forbidden** or domain error `NotAuthorized` / `NotPlayerTurn` (match existing `OneOf` patterns); do not mutate state.

### 2.3 Authorization — host / table admin

Commands that change **table settings**, delete game, resolve join requests, toggle odds for everyone, etc. keep or add **host/creator** checks (existing `IsAuthorized` patterns in Common handlers).

### 2.4 SignalR

1. `JoinGame(gameId)` only if authenticated **and** seated at `gameId` (or explicit spectator policy — **default: seated only** for private payloads).
2. `PrivateStateUpdated` only for the **caller’s** seat; never build private state for a spoofed identifier.
3. Public `TableStateUpdated` may still go to all group members (no hole cards in public DTO when other audits are fixed).

### 2.5 Client contract

- **Remove** `PlayerId` from request bodies where the actor must be “me” (§6). Web Refit clients should stop sending it; API ignores client `PlayerId` and uses resolved `GamePlayer.Id` internally.
- **Exception:** Host-only actions that intentionally target another player (e.g. host adding chips **to** someone else) must validate **host** identity, not trust `PlayerId` alone.

---

## 3. Attack scenarios (concrete — use in tests)

### 3.1 Steal hole cards via SignalR

1. Attacker connects to `/hubs/game?authenticated=true&userEmail=victim@example.com` (no valid login).
2. `HeaderAuthenticationHandler` succeeds; `SignalRUserIdProvider` sets user id to email.
3. `JoinGame(tableGuid)` → `BuildPrivateStateAsync` matches `gp.Player.Email == victim@example.com`.
4. **Expected after fix:** Hub rejects join or returns no private state; connection must not receive victim’s cards.

### 3.2 Act on someone else’s turn (Hold’em)

1. Table in PreFlop; seat 2 is current actor.
2. Attacker `POST /api/v1/games/hold-em/{id}/betting/actions` with `{ "actionType": "Fold" }` **without** auth headers.
3. Handler folds **seat 2**.
4. **Expected after fix:** 401 without auth; 403/`NotPlayerTurn` if authenticated as another seated player.

### 3.3 Kings and Lows draw as victim

1. `POST .../draw-cards` with `{ "playerId": "<victim PlayerId>", "discardIndices": [] }`.
2. Handler validates turn for that `PlayerId` but **not** caller identity (`DrawCardsCommandHandler` ~56–77).
3. **Expected after fix:** Only victim’s own login can pass `PlayerId` matching their seat; ideally command has **no** `PlayerId`.

### 3.4 Add chips to arbitrary player

1. `POST .../games/{id}/chips` (AddChips) with victim’s `PlayerId`.
2. `AddChipsCommandHandler` applies chips (~66–108) with no auth.
3. **Expected after fix:** 401, or 403 unless caller is that player (or host where intended).

---

## 4. Authentication architecture (choose and implement one)

The repo already registers **JWT** (`AddMicrosoftIdentityWebApi`) and **HeaderAuth** (`SmartAuth` forward selector in `Program.cs` ~50–74). Game routes use **neither** today.

### Option 1 (recommended for this codebase): **BFF + signed internal identity**

Keep server-side Refit from Blazor (`AuthenticationStateHandler` in `CardGames.Poker.Web/Infrastructure/AuthenticationStateHandler.cs`), but **stop treating raw headers as proof**.

1. **Shared secret** (config): `InternalIdentity:SigningKey` (32+ bytes), known only to Web + API (Aspire user secrets / Key Vault).
2. **Web:** Replace plain headers with a short-lived signed token, e.g. header `X-Internal-User-Token` = Base64url(HMAC-SHA256(payload, key)) over JSON:

   ```json
   { "sub": "<identity user id>", "email": "<email>", "name": "<display>", "exp": <unix> }
   ```

   Mint token in `AuthenticationStateHandler` only when `user.Identity.IsAuthenticated`.
3. **API:** New `InternalUserTokenAuthenticationHandler` (or extend `HeaderAuthenticationHandler`):
   - **Reject** unsigned `X-User-*` in Production (`IHostEnvironment.IsProduction()`).
   - **Validate** HMAC, `exp`, constant-time compare; emit normal `ClaimsPrincipal` (`sub`, `email`).
4. **Development:** Allow unsigned headers only when `InternalIdentity:AllowUnsafeHeaders=true` (default false in CI).

**Files:**

- `CardGames.Poker.Web/Infrastructure/AuthenticationStateHandler.cs` — mint token.
- `CardGames.Poker.Api/Infrastructure/HeaderAuthenticationHandler.cs` — remove or gate legacy behavior.
- New: `CardGames.Poker.Api/Infrastructure/InternalUserTokenAuthenticationHandler.cs`
- `CardGames.Poker.Api/Program.cs` — register scheme; update `SmartAuth` forward selector.
- `appsettings.Development.json` / Aspire parameters for secrets.

### Option 2: **JWT only on API**

Web obtains access token (Azure AD / Identity) and sends `Authorization: Bearer`. API `RequireAuthorization()` on all game groups. More moving parts; align with existing `AddMicrosoftIdentityWebApi` if tenants are already configured.

### Option 3: **Network-only API (insufficient alone)**

Bind API to internal network and block public ingress. Still fix §5 authorization (defense in depth); do **not** rely on network alone if any route is public.

**Plan default:** Implement **Option 1** unless the team standardizes on Option 2.

### 4.1 SignalR WebSocket identity

`GameHubClient.GetHubUrl` (~251–278) puts identity in **query string** (visible in logs/proxies). After Option 1:

- Pass `access_token` or signed token via query **only for negotiate**, or use `AccessTokenProvider` on `HubConnectionBuilder` with the same signed token the API validates.
- Remove trusting `authenticated=true` without a valid signature.

**Files:** `CardGames.Poker.Web/Services/GameHubClient.cs`, `NotificationHubClient.cs`, `LeagueHubClient.cs` (same header pattern).

### 4.2 CORS (production hardening)

`Program.cs` ~86–91: replace `SetIsOriginAllowed(_ => true)` with configured Web origin(s) when not Development.

---

## 5. Authorization layer (required regardless of auth option)

### 5.1 `IGamePlayerResolver` (new)

**Path:** `src/CardGames.Poker.Api/Services/GamePlayerResolver.cs` (+ interface in `Infrastructure` or `Services`).

```csharp
public interface IGamePlayerResolver
{
    Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, CancellationToken ct);
    // Optional: Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, Guid playerId, CancellationToken ct);
}
```

**Logic:** Copy matching from `GetRabbitHuntQueryHandler` (~58–66) and `LeaveGameCommandHandler` (~50–53):

- Require `ICurrentUserService.IsAuthenticated`.
- Match `Player.Email`, `Player.Name`, `Player.ExternalId` to `UserEmail`, `UserName`, `UserId`.
- `Status != Left`.

Centralize here — do not duplicate matching in 40 handlers.

### 5.2 MediatR pipeline: `GameAuthorizationBehavior`

**Path:** `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameAuthorizationBehavior.cs`

Register in `Program.cs` **before** `GameStateBroadcastingBehavior` (~163–166).

**Marker interfaces** (new, small files):

```csharp
public interface IRequiresAuthentication { }

public interface IRequiresSeatedPlayer : IGameStateChangingCommand, IRequiresAuthentication { }

public interface IRequiresCurrentActor : IRequiresSeatedPlayer
{
    // behavior loads game + betting round / draw index and compares seat
}
```

**Behavior:**

- If `IRequiresAuthentication` and `!currentUserService.IsAuthenticated` → throw `UnauthorizedAccessException` or return typed error (prefer consistent `OneOf` error if handler uses it — may need `IAuthorizationFilter` at endpoint level instead for uniform 401; see §5.4).
- If `IRequiresSeatedPlayer` → resolve seated `GamePlayer`; if null → forbidden.
- If `IRequiresCurrentActor` → enforce actor (§5.3).

**Apply markers** to all `IGameStateChangingCommand` records that represent human actions (betting, draw, discard, showdown trigger, join/leave, etc.). **Exclude** `ContinuousPlayBackgroundService` / timer paths — they use MediatR from server without HTTP user; keep those handlers **without** `IRequiresAuthentication` or use an internal “system” bypass only in hosted services (do not expose bypass to HTTP).

### 5.3 Actor rules (implement in behavior or shared helper)

| Action family | Current actor source | Rule |
|---------------|---------------------|------|
| Community/draw betting (`ProcessBettingAction*`) | `BettingRound.CurrentActorIndex` vs `GamePlayer.SeatPosition` | Seated player’s seat must equal `CurrentActorIndex`. |
| Draw (`ProcessDraw`, `DrawCards`, Irish discard, etc.) | `Game.CurrentDrawPlayerIndex` | Seat must match. |
| Bob Barker showcase | `SelectShowcaseCommand.PlayerSeatIndex ?? CurrentDrawPlayerIndex` | **Remove** client `PlayerSeatIndex` override; always use resolved seated player’s seat; must be in eligible set. |
| Simultaneous phases (`DropOrStay`, `KeepOrTrade`, Bob Barker draw phase timer) | All eligible seats | Authenticated user must be one of eligible seated players acting for **self** only. |
| `PerformShowdown` | N/A (table transition) | Require **any seated** player (or any connected seated player — product choice: **seated + authenticated**). |
| `StartHand` / `CollectAntes` / `DealHands` | Often host or any player | Match existing product rules: at minimum **authenticated + seated**; prefer **dealer or host** if already documented in UI. |
| `AddChips` | Self-service rebuy | `command.PlayerId` must equal resolved seated `GamePlayer.PlayerId` OR caller is host/creator. |

**Hold’em `NotPlayerTurn`:** `ProcessBettingActionErrorCode.NotPlayerTurn` exists but Hold’em handler may not return it today — wire authorization **before** handler and add tests.

### 5.4 Minimal API `RequireAuthorization()`

On **every** game `MapGroup` in `Features/Games/**/V1.cs` and `Common/v1/V1.cs`:

```csharp
var mapGroup = app.MapGroup("...")
    .RequireAuthorization("SmartAuth"); // or dedicated policy
```

Split policies if needed:

- `GamePlay` — authenticated + seated checks in MediatR.
- `League` — already uses `RequireAuthorization()` on many league endpoints.

**Read-only public queries** (`GetGame`, `GetGameRules`, `AvailablePokerGames`) — product decision:

- Either remain anonymous for lobby browsing, or require auth. **Do not** expose private data on anonymous routes.

Register in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GamePlay", policy =>
        policy.RequireAuthenticatedUser()
              .AddAuthenticationSchemes("SmartAuth"));
});
```

### 5.5 `GameHub` authorization

**File:** `src/CardGames.Poker.Api/Hubs/GameHub.cs`

In `JoinGame`:

1. Inject `IGamePlayerResolver` (or `CardsDbContext` + `ICurrentUserService`).
2. After auth, `var seated = await resolver.ResolveSeatedPlayerAsync(gameId, ct)`.
3. If `seated is null` → `throw new HubException("Not seated at this table")` (do not send private state).
4. Call `BuildPrivateStateAsync(gameId, stableId)` where `stableId` is **`seated.Player.Email ?? seated.Player.Name ?? seated.Player.ExternalId`** — not raw spoofed string from connection without verification.

Optional: `OnConnectedAsync` — do not add to groups until `JoinGame` succeeds (already the case).

---

## 6. Commands that must stop trusting client `PlayerId`

| Command | Endpoint / file | Fix |
|---------|-----------------|-----|
| `DrawCardsCommand` | `KingsAndLows/.../DrawCardsEndpoint.cs` | Remove `PlayerId` from request; resolve actor from `IGamePlayerResolver` + `CurrentDrawPlayerIndex`. |
| `DeckDrawCommand` | `KingsAndLows/.../DeckDrawEndpoint.cs` | Same. |
| `DropOrStayCommand` | `KingsAndLows/.../DropOrStayEndpoint.cs` | Same; simultaneous phase = must be self. |
| `KeepOrTradeCommand` | `ScrewYourNeighbor/.../KeepOrTradeEndpoint.cs` | Same. |
| `AceChoiceCommand` | `InBetween/.../AceChoiceEndpoint.cs` | Same. |
| `PlaceBetCommand` | `InBetween/.../PlaceBetEndpoint.cs` | Same + InBetween turn rules. |
| `ProcessBuyCardCommand` | `Baseball/.../ProcessBuyCardEndpoint.cs` | Resolve player from current user; must be `BuyCardOffer` actor. |
| `AddChipsCommand` | `Common/.../AddChips` | Require auth; `PlayerId` must match caller or host. |
| `SelectShowcaseCommand` | `BobBarker/.../SelectShowcaseCommand.cs` | Remove optional `PlayerSeatIndex` from client; use seated player seat only. |

**Betting / draw without `PlayerId` (still broken today due to no auth):**

- `ProcessBettingActionCommand` (HoldEm, FCD, Stud, …) — add `IRequiresCurrentActor`.
- `ProcessDrawCommand`, `ProcessDiscardCommand`, `FoldDuringDrawCommand`, `TollboothChooseCard`, etc. — add markers + actor checks.

**Web updates:** Regenerate or hand-edit Refit contracts in `CardGames.Contracts` / `CardGames.Poker.Web` so TablePlay stops sending `PlayerId` for self actions. Grep Web for `PlayerId` on API calls.

---

## 7. `ICurrentUserService` cleanup

**File:** `Infrastructure/CurrentUserService.cs`

After Option 1, **remove** fallback that reads raw `X-User-*` headers directly (~28–31, 53–56, 76–79, 96–98) in Production — identity must come **only** from validated `ClaimsPrincipal`.

Keep header fallbacks only behind `AllowUnsafeHeaders` for local dev.

---

## 8. Implementation order (checklist)

1. **Config & auth handler** — Option 1 token mint + validate; gate legacy headers (§4).
2. **`IGamePlayerResolver`** + unit tests for email/name/externalId matching.
3. **`GameAuthorizationBehavior`** + marker interfaces; register pipeline.
4. **`.RequireAuthorization()`** on all game route groups (§5.4).
5. **`GameHub.JoinGame`** seated check (§5.5).
6. **High-risk handlers first:**
   - `ProcessBettingActionCommandHandler` (HoldEm) — current actor.
   - `DrawCardsCommandHandler`, `DropOrStay`, `KeepOrTrade`, `PlaceBet`, `AceChoice`, `ProcessBuyCard`.
   - `AddChipsCommandHandler`.
7. **Mark remaining** `IGameStateChangingCommand` implementations (use `rg "IGameStateChangingCommand"`).
8. **Web:** `AuthenticationStateHandler`, `GameHubClient` token; remove spoofable query params in prod.
9. **CORS** tighten for production (§4.2).
10. **Integration tests** (§9).

---

## 9. Tests

### 9.1 Authentication

| Test | Assert |
|------|--------|
| `POST` betting action with **no** auth | 401 |
| `POST` with forged `X-User-Email` only (Production config) | 401 |
| `POST` with valid internal token | 200/4xx domain errors only |

Use `WebApplicationFactory` with `InternalIdentity:AllowUnsafeHeaders=false`.

### 9.2 Authorization — betting

| Test | Assert |
|------|--------|
| Player A authenticated, B is current actor, A posts fold | `NotPlayerTurn` or 403 |
| Player B authenticated, B is current actor, B posts fold | Success |

Extend `HoldEmHandLifecycleTests` or `ProcessBettingActionCommandHandlerTests` with authenticated client factory if needed.

### 9.3 Authorization — `DrawCards`

| Test | Assert |
|------|--------|
| Authenticated as A, body `playerId` = B | Reject |
| Authenticated as B, B’s turn | Success |

### 9.4 SignalR

Integration test (or hub test with `IHubContext`):

- Connection with token for user A, `JoinGame` for table where A is **not** seated → hub error, no `PrivateStateUpdated` with cards.
- Seated user receives private state with hole cards.

### 9.5 Regression

- `LeaveGame`, `GetRabbitHunt`, league endpoints still work with new auth.
- Timer / `ContinuousPlayBackgroundService` still completes hands (no HTTP user on those MediatR calls).

**Commands:**

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~Auth|FullyQualifiedName~Betting|FullyQualifiedName~DrawCards"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj
```

---

## 10. Acceptance criteria

- [ ] Production API rejects unauthenticated game **mutations**.
- [ ] Production API rejects unsigned / invalid identity spoofing (`X-User-*` alone).
- [ ] Authenticated user **cannot** receive `PrivateStateUpdated` for a table they are not seated at.
- [ ] Authenticated user **cannot** perform betting/draw/special actions unless they are the current actor (or allowed simultaneous participant).
- [ ] Client `PlayerId` cannot be used to act as another player (removed or verified).
- [ ] `AddChips` cannot target another player without host authorization.
- [ ] Background timer auto-actions still work without human HTTP identity.
- [ ] Documented config keys for signing secret and dev-only unsafe headers.

---

## 11. File reference (primary touch points)

| Area | Path |
|------|------|
| Header auth (legacy) | `CardGames.Poker.Api/Infrastructure/HeaderAuthenticationHandler.cs` |
| Current user | `CardGames.Poker.Api/Infrastructure/CurrentUserService.cs`, `ICurrentUserService.cs` |
| Auth wiring | `CardGames.Poker.Api/Program.cs` (~50–93, MediatR behaviors ~163+) |
| SignalR hub | `CardGames.Poker.Api/Hubs/GameHub.cs` |
| SignalR user id | `CardGames.Poker.Api/Infrastructure/SignalRUserIdProvider.cs` |
| Private state lookup | `CardGames.Poker.Api/Services/TableStateBuilder.cs` `BuildPrivateStateAsync` (~327–377) |
| Web API identity forward | `CardGames.Poker.Web/Infrastructure/AuthenticationStateHandler.cs` |
| Web SignalR URL | `CardGames.Poker.Web/Services/GameHubClient.cs` |
| Hold’em betting endpoint | `Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionEndpoint.cs` |
| Hold’em betting handler | `.../ProcessBettingActionCommandHandler.cs` (no player id; uses `CurrentActorIndex`) |
| K&L draw | `Features/Games/KingsAndLows/v1/Commands/DrawCards/DrawCardsCommandHandler.cs` |
| Add chips | `Features/Games/Common/v1/Commands/AddChips/AddChipsCommandHandler.cs` |
| Good pattern | `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameCommandHandler.cs` |
| Good pattern | `Features/Games/Common/v1/Queries/GetRabbitHunt/GetRabbitHuntQueryHandler.cs` |

---

## 12. Out of scope (separate work)

- Fold-win hole-card visibility (`docs/auditing/fix-fold-win-hole-card-leak.md`).
- Raise stack cap (`docs/auditing/fix-raise-stack-cap.md`).
- Rate limiting / WAF.
- Full Azure AD B2C migration (unless Option 2 chosen).

---

## 13. Audit cross-reference

This plan addresses **authentication trust** and **authorization to act / receive private state**. Fixing authorization alone without fixing authentication (signed token or JWT) leaves spoofing intact; implement **§4 and §5 together**.
