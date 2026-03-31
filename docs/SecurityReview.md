# Security Review — CardGames Poker Platform

**Date:** 2026-03-31
**Reviewer role:** Senior Application Security Engineer / Multiplayer Game Security Engineer / .NET Architect
**Scope:** Full solution — API, Blazor Web, SignalR, Domain, Infrastructure, Data, Configuration
**Threat model:** Real-money online poker platform deployed on the public internet with motivated adversaries

---

## Table of Contents

1. [Solution Map and Trust Boundaries](#1-solution-map-and-trust-boundaries)
2. [Authentication and Identity](#2-authentication-and-identity)
3. [Authorization](#3-authorization)
4. [Anti-Cheat / Game Integrity](#4-anti-cheat--game-integrity)
5. [SignalR Security](#5-signalr-security)
6. [API Security](#6-api-security)
7. [Database and Persistence](#7-database-and-persistence)
8. [Blazor / Client Security](#8-blazor--client-security)
9. [Secrets and Configuration](#9-secrets-and-configuration)
10. [Deployment / Production Hardening](#10-deployment--production-hardening)
11. [Fairness, Auditability, and Fraud Resistance](#11-fairness-auditability-and-fraud-resistance)
12. [Launch Blockers (Section A)](#a-launch-blockers)
13. [Anti-Cheat Architecture Review (Section B)](#b-anti-cheat-architecture-review)
14. [Top 10 Highest-Risk Issues (Section C)](#c-top-10-highest-risk-issues)
15. [Secure Rollout Checklist (Section D)](#d-secure-rollout-checklist)
16. [Missing Protections (Section E)](#e-missing-protections)

---

## 1. Solution Map and Trust Boundaries

### Entry Points Identified

| Entry Point | Project | Protocol | Trust Level |
|---|---|---|---|
| Minimal API endpoints | `CardGames.Poker.Api` | HTTPS | Untrusted (internet) |
| GameHub | `CardGames.Poker.Api/Hubs/GameHub.cs` | WebSocket/SignalR | Untrusted |
| LobbyHub | `CardGames.Poker.Api/Hubs/LobbyHub.cs` | WebSocket/SignalR | Untrusted |
| NotificationHub | `CardGames.Poker.Api/Hubs/NotificationHub.cs` | WebSocket/SignalR | Untrusted |
| LeagueHub | `CardGames.Poker.Api/Hubs/LeagueHub.cs` | WebSocket/SignalR | Untrusted |
| Blazor Server UI | `CardGames.Poker.Web` | HTTPS + WebSocket | Untrusted |
| Identity endpoints | `CardGames.Poker.Web` (Account) | HTTPS | Untrusted |

### Money-Related Flows

| Flow | Handler | Risk |
|---|---|---|
| Add chips to account | `AddAccountChipsCommandHandler` | Protected (auth required) |
| Buy-in / join game | `JoinGameCommandHandler` → `PlayerChipWalletService.TryDebitForBuyInAsync` | Auth in handler, NOT on endpoint |
| In-game add chips | `AddChipsCommandHandler` | **Unprotected** |
| Hand settlement | `HandSettlementService` → `PlayerChipWalletService.RecordHandSettlementAsync` | Server-internal, idempotent |
| Cash-out / leave | `LeaveGameCommandHandler` → `PlayerChipWalletService.CreditForCashOutAsync` | Auth in handler |

### Game-State Mutation Paths

All game actions (deal, bet, fold, call, raise, check, draw, showdown, collect antes, start hand) are invoked through HTTP POST endpoints mapped per game variant. ~94 game endpoints lack explicit authorization checks.

### RNG / Shuffle / Dealing

- **Interface:** `IRandomNumberGenerator` (`CardGames.Core/Random/IRandomNumberGenerator.cs`)
- **Implementation:** `StandardRandomNumberGenerator` (`CardGames.Core/Random/StandardRandomNumberGenerator.cs`) — uses `System.Random`
- **Consumer:** `Dealer<TCardKind>` (`CardGames.Core/Dealer/Dealer.cs`) — deals cards from shuffled deck

---

## 2. Authentication and Identity

### SEC-AUTH-01 — Header-Based Authentication Trusts Client-Supplied Identity (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authentication |
| **Location** | `CardGames.Poker.Api/Infrastructure/HeaderAuthenticationHandler.cs` (lines 32-131) |
| **Why it's a problem** | The `HeaderAuthenticationHandler` creates a fully authenticated `ClaimsPrincipal` from unverified HTTP headers (`X-User-Id`, `X-User-Name`, `X-User-Email`, `X-User-Authenticated`). Any HTTP client can send these headers directly. There is no signature, HMAC, shared secret, or token validation on these headers. The handler also reads the same values from query string parameters (lines 43-77), making URL-based impersonation possible. |
| **Exploitation** | An attacker sends `curl -H "X-User-Id: victim-guid" -H "X-User-Authenticated: true"` to any API endpoint. The request is authenticated as the victim. This grants full access to the victim's games, chip account, leagues, and actions. With header auth also passed via query string, SignalR WebSocket upgrades are equally exploitable. |
| **Impact** | Complete identity spoofing. Any player can impersonate any other player. Full account takeover for every user. |
| **Remediation** | (1) Remove header-based authentication entirely for externally facing deployments. (2) If Blazor-to-API communication requires forwarded identity, implement a cryptographic token exchange: the Web frontend obtains a short-lived signed JWT from its own Identity system, and the API validates that JWT signature. (3) If headers must be used in development, gate the `HeaderAuth` scheme behind `IHostEnvironment.IsDevelopment()` and refuse to register it in production. (4) At minimum, restrict header auth to requests originating from the Blazor backend (by IP, mTLS, or internal network only — never exposed to clients). |
| **Blocks launch** | **Yes** |

### SEC-AUTH-02 — CurrentUserService Falls Back to Spoofable Headers (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authentication |
| **Location** | `CardGames.Poker.Api/Infrastructure/CurrentUserService.cs` (lines 28-32, 53-57, 76-80, 96-99) |
| **Why it's a problem** | `CurrentUserService.UserId`, `.UserName`, `.UserEmail`, and `.IsAuthenticated` all fall back to reading `X-User-Id`, `X-User-Name`, `X-User-Email`, and `X-User-Authenticated` headers when claims are not present. Even if header auth is fixed, this service independently trusts headers. |
| **Exploitation** | Identical to SEC-AUTH-01. Anywhere `ICurrentUserService` is used for authorization decisions (JoinGame, LeaveGame, CreateGame, AddAccountChips), the caller can forge identity. |
| **Impact** | Identity spoofing across all services consuming `ICurrentUserService`. |
| **Remediation** | Remove header fallback from `CurrentUserService`. Require all identity to come from validated `ClaimsPrincipal` via `HttpContext.User`. |
| **Blocks launch** | **Yes** |

### SEC-AUTH-03 — PII Logging Enabled in Azure AD Configuration (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Authentication / Secrets |
| **Location** | `CardGames.Poker.Api/appsettings.json` (line 13: `"EnablePiiLogging": true`) |
| **Why it's a problem** | When `EnablePiiLogging` is true, Microsoft Identity logs tokens, user identifiers, and other PII to application logs. In production, this exposes sensitive data in log aggregation systems. |
| **Exploitation** | An attacker with access to log files or a log aggregation dashboard can extract user tokens and PII. |
| **Impact** | Token leakage, user privacy violation, potential account compromise. |
| **Remediation** | Set `EnablePiiLogging: false` in production configuration. Only enable it in Development via `appsettings.Development.json`. |
| **Blocks launch** | No |

### SEC-AUTH-04 — No Account Lockout or Brute-Force Protection on Identity (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Authentication |
| **Location** | `CardGames.Poker.Web/Program.cs` (lines 325-332), `CardGames.Poker.Api/Program.cs` (lines 130-136) |
| **Why it's a problem** | `AddIdentityCore<ApplicationUser>` is configured without lockout options. Default ASP.NET Core Identity lockout is enabled but the code does not explicitly configure lockout thresholds (max failed attempts, lockout duration). |
| **Exploitation** | Credential-stuffing or brute-force attacks against login endpoints. |
| **Impact** | Account compromise through password guessing. |
| **Remediation** | Explicitly configure lockout: `options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);`. |
| **Blocks launch** | No |

### SEC-AUTH-05 — Email Sender is No-Op (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Authentication |
| **Location** | `CardGames.Poker.Web/Program.cs` (line 334: `IdentityNoOpEmailSender`) |
| **Why it's a problem** | Email confirmation and password reset emails are silently discarded. `RequireConfirmedAccount = true` is set but users can never actually confirm their accounts through email. This affects password reset security and email-verified registration flows. |
| **Remediation** | Implement a real email sender for production. |
| **Blocks launch** | No (functional issue, not a direct exploit) |

---

## 3. Authorization

### SEC-AUTHZ-01 — No Global Authorization Policy; 71% of Endpoints Unprotected (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authorization |
| **Location** | `CardGames.Poker.Api/Program.cs` (line 76: `builder.Services.AddAuthorization()`) |
| **Why it's a problem** | `AddAuthorization()` is called without a `FallbackPolicy` that requires authenticated users. Without a global default, every endpoint that does not explicitly call `.RequireAuthorization()` is open to anonymous access. Out of ~131 endpoint files, only ~37 explicitly require authorization. All game commands (betting, dealing, showdowns, add chips), game queries, and avatar upload are accessible anonymously. |
| **Exploitation** | An unauthenticated attacker can: create games, join games, place bets, deal cards, trigger showdowns, add chips to any player, and query all game state. |
| **Impact** | Total bypass of access control. All game and financial operations are open to the internet. |
| **Remediation** | Add a fallback authorization policy in `Program.cs`: `builder.Services.AddAuthorization(options => { options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(); });`. Then explicitly mark truly public endpoints (health check, login) with `.AllowAnonymous()`. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-02 — AddChips (In-Game) Has No Authorization and No Ownership Check (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authorization / Financial |
| **Location** | `CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/AddChipsEndpoint.cs`, `AddChipsCommandHandler.cs` |
| **Why it's a problem** | The `POST /api/v1/games/{gameId}/players/{playerId}/add-chips` endpoint has no `.RequireAuthorization()`. The handler does not inject `ICurrentUserService` and does not verify the requesting user owns the target player seat. Anyone can add chips to any player in any game. |
| **Exploitation** | `curl -X POST .../games/{id}/players/{playerId}/add-chips -d '{"amount":1000000}'` — instantly gives a million chips to any player. |
| **Impact** | Unlimited chip injection. Complete destruction of game economy and fairness. |
| **Remediation** | (1) Add `.RequireAuthorization()` to endpoint. (2) Inject `ICurrentUserService` in handler and verify requesting user is the game host or the player themselves. (3) Validate chip addition against wallet balance. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-03 — Avatar Upload Endpoint Allows Anonymous Access (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Authorization |
| **Location** | `CardGames.Poker.Api/Features/Profile/v1/Commands/UploadAvatar/UploadAvatarEndpoint.cs` (line 62: `.AllowAnonymous()`) |
| **Why it's a problem** | The avatar upload endpoint explicitly allows anonymous access. `.DisableAntiforgery()` is also called. Any unauthenticated user can upload files to Azure Blob Storage. |
| **Exploitation** | Resource exhaustion via mass uploads. Storage cost attack. Potential hosting of malicious content under the application's domain. |
| **Impact** | Cloud storage cost abuse, content abuse, potential XSS if blob URLs are rendered without Content-Disposition headers. |
| **Remediation** | Change `.AllowAnonymous()` to `.RequireAuthorization()`. Add rate limiting. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-04 — Betting Action Handlers Do Not Verify Player Identity (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authorization / Anti-Cheat |
| **Location** | All `ProcessBettingActionCommandHandler` implementations across 10+ game variants (HoldEm, FiveCardDraw, SevenCardStud, Baseball, Tollbooth, PairPressure, GoodBadUgly, FollowTheQueen, HoldTheBaseball, TwosJacksManWithTheAxe) |
| **Why it's a problem** | The `ProcessBettingActionCommand` record contains only `GameId`, `ActionType`, and `Amount`. It does not include the acting player's identity. The handler determines the current player from `bettingRound.CurrentActorIndex` without verifying that the authenticated user matches that player. No handler injects `ICurrentUserService`. |
| **Exploitation** | Any user can submit a betting action request for any game. The server executes the action as whichever player is currently expected to act, regardless of who sent the request. Player A can force Player B to fold by sending a fold action when it is Player B's turn. |
| **Impact** | Complete game manipulation. Players can control opponents' actions. |
| **Remediation** | (1) Add user identity to the command (from `ICurrentUserService`, not from the request body). (2) In every handler, verify `currentPlayer.Player.Name == currentUserService.UserName` before processing. (3) Return an error like "Not your turn" if the identities don't match. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-05 — ProcessDraw Handlers Do Not Verify Player Identity (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Authorization / Anti-Cheat |
| **Location** | `CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs` and equivalent handlers in TwosJacksManWithTheAxe, KingsAndLows |
| **Why it's a problem** | Same pattern as SEC-AUTHZ-04. The draw command contains `GameId` and `DiscardIndices` but no player identity. The handler accepts the request and processes it for whichever player's turn it is. |
| **Exploitation** | An attacker can discard another player's cards, choosing which cards the victim keeps. |
| **Impact** | Card manipulation, opponent sabotage. |
| **Remediation** | Same as SEC-AUTHZ-04 — add player identity verification. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-06 — StartHand / DealHands Endpoints Have No Authorization (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Authorization |
| **Location** | `Features/Games/Generic/v1/Commands/StartHand/StartHandEndpoint.cs`, all `DealHandsEndpoint.cs` files |
| **Why it's a problem** | Any client can trigger dealing or starting a new hand. There is no check that the requester is the game host or dealer. |
| **Exploitation** | An attacker can repeatedly start hands or force deals, disrupting game flow. Combined with the betting action exploit, an attacker can deal, bet, and resolve games single-handedly. |
| **Impact** | Game disruption, potential chip extraction through rapid game manipulation. |
| **Remediation** | Add authorization and verify requesting user is the game host/dealer. |
| **Blocks launch** | **Yes** |

### SEC-AUTHZ-07 — Seed Development Users Endpoint Allows Anonymous Access (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Authorization |
| **Location** | `Features/Testing/v1/Commands/SeedUsers/SeedDevelopmentUsersEndpoint.cs` (line 28: `.AllowAnonymous()`), `Features/Testing/TestingApiMapGroup.cs` |
| **Why it's a problem** | Although the endpoint is gated behind `IHostEnvironment.IsDevelopment()` in `TestingApiMapGroup.cs`, if environment detection fails or is misconfigured in production, unauthenticated users can create confirmed user accounts with known passwords (`Test1234!`). |
| **Exploitation** | If deployed in non-Development mode that doesn't match `IsDevelopment()` check exactly, users are created. |
| **Impact** | Backdoor account creation. |
| **Remediation** | Add additional safeguards: require an admin secret header or API key even in development. Consider `#if DEBUG` compilation guard. |
| **Blocks launch** | No (properly gated), but verify deployment configuration carefully |

---

## 4. Anti-Cheat / Game Integrity

### SEC-CHEAT-01 — Cryptographically Weak RNG for Card Shuffling (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Anti-Cheat / Fairness |
| **Location** | `CardGames.Core/Random/StandardRandomNumberGenerator.cs` (lines 4-9) |
| **Why it's a problem** | `StandardRandomNumberGenerator` uses `System.Random`, which is a pseudo-random number generator (PRNG) seeded from the system clock. It is NOT cryptographically secure. Its output is deterministic and predictable if the seed is known or can be inferred. Additionally, `System.Random` is not thread-safe; concurrent access can corrupt its internal state, causing repeated values or degenerate sequences. |
| **Exploitation** | (1) An attacker who can observe a sequence of dealt cards (visible in their hand or community cards) may be able to reconstruct the PRNG seed and predict future cards. (2) A `new System.Random()` seeded close in time to another instance may produce identical sequences. (3) Under concurrent dealing, corrupted state could produce duplicate cards. |
| **Impact** | Predictable card order. Players can determine opponents' hole cards and upcoming community/draw cards. Absolute destruction of game fairness. |
| **Remediation** | Replace with a cryptographically secure implementation: `System.Security.Cryptography.RandomNumberGenerator.GetInt32(upperBound)`. This provides unpredictable, uniform distribution suitable for real-money card games. Create a new implementation of `IRandomNumberGenerator` that wraps this method. |
| **Blocks launch** | **Yes** |

### SEC-CHEAT-02 — No Concurrency Control on Game Actions (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Anti-Cheat / Race Condition |
| **Location** | All `ProcessBettingActionCommandHandler` implementations, `ProcessDrawCommandHandler`, `CollectAntesCommandHandler` |
| **Why it's a problem** | Game entities inherit `EntityWithRowVersion` and EF Core is configured with `IsRowVersion()`, but NO command handler catches `DbUpdateConcurrencyException`. Two concurrent requests to the same game can both read the same state, both process independently, and both save. The second save should fail with a concurrency exception, but since it's not caught, the behavior is undefined (may throw 500 or may silently succeed depending on EF Core provider behavior). There is no explicit transaction wrapping either. |
| **Exploitation** | (1) Open two browser tabs. (2) Submit a bet from both tabs simultaneously. (3) Both requests read `CurrentActorIndex = 2`. (4) Both process the action. (5) Chips are deducted twice, pot is inconsistent. (6) Alternatively, one request folds while another raises — both succeed, creating an impossible game state. |
| **Impact** | Double-spending chips, corrupted game state, impossible action sequences. |
| **Remediation** | (1) Wrap game-state-modifying handlers in explicit transactions. (2) Catch `DbUpdateConcurrencyException` and retry or return a conflict error. (3) Consider using a distributed lock (Redis SETNX) per game ID to serialize game-state mutations. |
| **Blocks launch** | **Yes** |

### SEC-CHEAT-03 — Client Can Act as Any Player (Out-of-Turn / Impersonation) (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | Anti-Cheat |
| **Location** | All game action endpoints and handlers (see SEC-AUTHZ-04, SEC-AUTHZ-05) |
| **Why it's a problem** | Since betting and draw actions don't verify the acting player matches the authenticated user, any client can submit actions that execute as another player. |
| **Exploitation** | Player A waits for Player B's turn, then sends a fold action. The server processes it as Player B's fold. Player B is eliminated from the hand without their consent. |
| **Impact** | Complete control over opponents' actions. Makes the game unplayable for honest users. |
| **Remediation** | See SEC-AUTHZ-04. |
| **Blocks launch** | **Yes** |

### SEC-CHEAT-04 — No Idempotency on AddAccountChips (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Financial / Anti-Cheat |
| **Location** | `CardGames.Poker.Api/Features/Profile/v1/Commands/AddAccountChips/AddAccountChipsCommandHandler.cs` |
| **Why it's a problem** | The command handler performs `account.Balance += request.Amount` without any idempotency key or deduplication check. If a request is retried (network timeout, client retry, load balancer retry), chips are added multiple times. |
| **Exploitation** | (1) Submit AddAccountChips request. (2) If using an intercepting proxy, replay the request N times. (3) Balance increases N times. |
| **Impact** | Unlimited chip creation through request replay. |
| **Remediation** | Add an idempotency key to the request. Check for existing ledger entries with the same key before processing. Add a unique index on the idempotency key column. |
| **Blocks launch** | **Yes** |

### SEC-CHEAT-05 — Settlement Not Wrapped in a Database Transaction (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Financial / Data Integrity |
| **Location** | `CardGames.Poker.Api/Services/HandSettlementService.cs` (lines 25-47), `PlayerChipWalletService.RecordHandSettlementAsync` (lines 89-129) |
| **Why it's a problem** | `SettleHandAsync` iterates over participants and calls `RecordHandSettlementAsync` for each. If the process fails mid-iteration (crash, timeout, database error), some players' balances are updated while others are not. The wallet service has an idempotency check (lines 100-107), so re-running should be safe, but the partial-completion scenario leaves the database in an inconsistent state until manually corrected. |
| **Exploitation** | A carefully timed server crash during settlement could leave winnings credited to some players but not debited from others, creating chips from nothing. |
| **Impact** | Inconsistent balances, phantom chips. |
| **Remediation** | Wrap the entire `SettleHandAsync` method in an explicit database transaction: `await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken); ... await transaction.CommitAsync(cancellationToken);`. |
| **Blocks launch** | **Yes** (for real-money operations) |

### SEC-CHEAT-06 — Dealer Constructor Defaults to Weak RNG (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Anti-Cheat |
| **Location** | `CardGames.Core/Dealer/Dealer.cs` (lines 14-18) |
| **Why it's a problem** | The single-argument `Dealer(IDeck<TCardKind> deck)` constructor creates a `new StandardRandomNumberGenerator()` internally. Any code path that uses this constructor instead of the two-argument version silently gets the weak RNG. |
| **Exploitation** | Even if a secure RNG implementation is created, forgotten constructor calls bypass it. |
| **Remediation** | Remove the single-argument constructor or make it require an `IRandomNumberGenerator` parameter. Register the secure RNG in DI and inject it everywhere. |
| **Blocks launch** | No (dependent on SEC-CHEAT-01 fix) |

---

## 5. SignalR Security

### SEC-SIGNALR-01 — SignalR Hubs Use Header Auth (Exploitable) (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | SignalR Security |
| **Location** | All hub classes: `GameHub.cs`, `LobbyHub.cs`, `NotificationHub.cs`, `LeagueHub.cs` — all use `[Authorize(AuthenticationSchemes = HeaderAuthenticationHandler.SchemeName)]` |
| **Why it's a problem** | All four hubs authenticate exclusively via the header-based scheme. Since headers (and query parameters for WebSocket upgrade) are client-controlled, any attacker can connect to any hub as any user. |
| **Exploitation** | Connect to `wss://server/hubs/game?userId=victim&authenticated=true`. Join any game group. Receive all private state updates for the victim. See the victim's hole cards in real time. |
| **Impact** | Complete information leakage. Attacker sees every player's cards. Unbeatable cheating advantage. |
| **Remediation** | Replace header-based auth with proper token-based auth (JWT) for SignalR connections. Use `OnMessageReceived` event to extract JWT from query string (standard SignalR pattern). |
| **Blocks launch** | **Yes** |

### SEC-SIGNALR-02 — GameHub JoinGame Does Not Verify Player Membership (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | SignalR Security |
| **Location** | `CardGames.Poker.Api/Hubs/GameHub.cs` (lines 37-57) |
| **Why it's a problem** | `JoinGame(Guid gameId)` adds the caller to the game's SignalR group and sends them the full state snapshot (public + private) without verifying the caller is actually a player or spectator in that game. Any authenticated user can join any game's SignalR group and receive all state updates. |
| **Exploitation** | Attacker calls `JoinGame(targetGameId)` for a game they're not in. They receive `TableStateUpdated` (public) and `PrivateStateUpdated` (for whoever they're impersonating). |
| **Impact** | Information disclosure. With header auth bypass, see any player's cards. |
| **Remediation** | Before adding to the group, verify the caller's user ID exists as an active player (or authorized spectator) in the game's database record. |
| **Blocks launch** | **Yes** |

### SEC-SIGNALR-03 — Private State Sent to Caller Based on Unverified User ID (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | SignalR Security |
| **Location** | `CardGames.Poker.Api/Hubs/GameHub.cs` (lines 108-138), `GameStateBroadcaster.cs` (lines 77-85) |
| **Why it's a problem** | `SendStateSnapshotToCallerAsync` calls `BuildPrivateStateAsync(gameId, privateStateUserId)` where `privateStateUserId` comes from `Context.UserIdentifier ?? userId`. Since user identity is header-spoofable, private state (hole cards) is sent to the wrong user. The `GameStateBroadcaster` sends private state via `Clients.User(userId)` which relies on `SignalRUserIdProvider` — also based on spoofable claims. |
| **Exploitation** | Attacker connects with `X-User-Email: victim@example.com`. Receives victim's private hand state including hole cards. |
| **Impact** | Full hand visibility. Perfect knowledge of opponents' cards. |
| **Remediation** | Fix authentication (SEC-AUTH-01) and add game-membership verification (SEC-SIGNALR-02). |
| **Blocks launch** | **Yes** |

### SEC-SIGNALR-04 — LeagueHub Properly Validates Group Membership (Positive Finding)

| Field | Value |
|---|---|
| **Severity** | N/A (Positive) |
| **Category** | SignalR Security |
| **Location** | `CardGames.Poker.Api/Hubs/LeagueHub.cs` (lines 56-85) |
| **Note** | `JoinManagedLeague` verifies the caller has Owner/Manager/Admin role before joining the management group. This is the correct pattern and should be replicated in GameHub. |

---

## 6. API Security

### SEC-API-01 — CORS Policy Allows Any Origin (CRITICAL)

| Field | Value |
|---|---|
| **Severity** | Critical |
| **Category** | API Security |
| **Location** | `CardGames.Poker.Api/Program.cs` (lines 83-91) |
| **Why it's a problem** | `policy.SetIsOriginAllowed(_ => true)` combined with `.AllowCredentials()` allows any website on the internet to make credentialed cross-origin requests to the API. This is the most permissive CORS configuration possible. The comment says "Allow any origin for development" but this is in the main `Program.cs`, not gated by environment. |
| **Exploitation** | An attacker hosts a malicious website that makes AJAX calls to the poker API. If a player visits that site while logged in, the attacker's JavaScript can perform any API action on behalf of the player. |
| **Impact** | Cross-site request execution. Full account compromise via victim's browser. |
| **Remediation** | In production, restrict `SetIsOriginAllowed` to the exact Blazor frontend origin(s). Gate the permissive development policy behind `IHostEnvironment.IsDevelopment()`. |
| **Blocks launch** | **Yes** |

### SEC-API-02 — Scalar API Reference Exposed in All Environments (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | API Security |
| **Location** | `CardGames.Poker.Api/Program.cs` (lines 344-346) |
| **Why it's a problem** | `app.MapScalarApiReference()` is called unconditionally, while `app.MapOpenApi()` is correctly gated behind `IsDevelopment()`. The interactive API reference UI is exposed in production, giving attackers a complete map of all endpoints, parameters, and response schemas. |
| **Exploitation** | Attacker navigates to `/scalar/v1` in production and sees the full API surface. |
| **Impact** | Information disclosure. Accelerates attack reconnaissance. |
| **Remediation** | Gate `MapScalarApiReference()` behind `if (app.Environment.IsDevelopment())`. |
| **Blocks launch** | No (informational, but should be fixed) |

### SEC-API-03 — Prometheus Metrics Endpoint Exposed Without Authentication (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | API Security |
| **Location** | `CardGames.Poker.Api/Program.cs` (line 337: `app.MapPrometheusScrapingEndpoint()`) |
| **Why it's a problem** | The Prometheus scraping endpoint is publicly accessible without authentication. Internal metrics (request rates, error counts, cache hit ratios, active connections) are exposed. |
| **Exploitation** | Attacker queries `/metrics` to gather intelligence about server load, active users, and internal behavior patterns. |
| **Impact** | Information disclosure of operational metrics. |
| **Remediation** | Restrict the Prometheus endpoint to internal networks or require authentication. |
| **Blocks launch** | No |

### SEC-API-04 — Avatar Upload Has No File Content Validation (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | API Security |
| **Location** | `CardGames.Poker.Api/Features/Profile/v1/Commands/UploadAvatar/UploadAvatarEndpoint.cs`, `Infrastructure/Storage/AvatarStorageService.cs` |
| **Why it's a problem** | The upload endpoint checks `file.ContentType` against a whitelist of MIME types, but the `Content-Type` header is client-controlled and trivially spoofed. The file extension is extracted from the user-supplied filename. No magic-number/file-signature validation is performed on the actual file bytes. The blob is stored with the client-supplied content type. |
| **Exploitation** | Upload an HTML file with `Content-Type: image/jpeg` and a `.jpg` extension. The blob is stored. If the blob container has public access (line 15: `PublicAccessType.Blob`), the file may be served as HTML in certain browser contexts, enabling stored XSS under the application's storage domain. |
| **Impact** | Stored XSS, malicious file hosting, content abuse. |
| **Remediation** | (1) Validate file magic numbers (first bytes) against expected image formats. (2) Re-encode uploaded images using a library like SixLabors.ImageSharp to strip embedded scripts. (3) Set `Content-Disposition: attachment` on blob responses. (4) Consider private blob access with SAS tokens instead of public access. |
| **Blocks launch** | No (mitigated by separate storage domain) |

### SEC-API-05 — No CSRF Protection on API Endpoints Using Header Auth (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | API Security |
| **Location** | System-wide (all API endpoints using header-based auth) |
| **Why it's a problem** | Header-based authentication combined with open CORS (`AllowCredentials` + `AllowAnyOrigin`) means cross-site requests can successfully authenticate. The avatar upload endpoint also explicitly calls `.DisableAntiforgery()`. |
| **Exploitation** | Combined with SEC-API-01. Cross-site forged requests carry credentials. |
| **Impact** | CSRF attacks can perform any API action on behalf of the victim. |
| **Remediation** | Fix CORS (SEC-API-01). If cookies are used, implement anti-forgery tokens. For JWT, ensure tokens are stored in HttpOnly cookies or require custom headers that browsers block in CORS preflight. |
| **Blocks launch** | Dependent on SEC-API-01 |

### SEC-API-06 — Rate Limiting Only Applied to Specific Endpoints (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | API Security |
| **Location** | `CardGames.Poker.Api/Program.cs` (lines 213-246) |
| **Why it's a problem** | Rate limiting is configured with a `fixed` policy (4 requests/12 seconds) and a League join request policy, but these are only applied to specific endpoints. Game action endpoints (betting, dealing) have no rate limiting. |
| **Exploitation** | Automated rapid-fire API calls to disrupt games, spam game creation, or overload the server. |
| **Impact** | Denial of service, game disruption. |
| **Remediation** | Apply a global rate limiter to all endpoints. Consider per-user rate limits on game actions. |
| **Blocks launch** | No |

---

## 7. Database and Persistence

### SEC-DB-01 — RowVersion Concurrency Tokens Exist But Are Not Enforced (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Database / Concurrency |
| **Location** | `CardGames.Poker.Api/Data/Entities/EntityWithRowVersion.cs`, all game entity classes, all command handlers |
| **Why it's a problem** | `Game`, `GamePlayer`, `PlayerChipAccount`, and other entities inherit `EntityWithRowVersion`. EF Core is configured with `IsRowVersion()`. However, no command handler catches `DbUpdateConcurrencyException`, meaning concurrency conflicts either throw unhandled 500 errors or are silently swallowed by middleware. The RowVersion is not explicitly loaded or compared in any handler logic. |
| **Impact** | See SEC-CHEAT-02. Race conditions cause inconsistent state. |
| **Remediation** | Add `try/catch (DbUpdateConcurrencyException)` blocks with retry logic in all game-state-modifying handlers. |
| **Blocks launch** | **Yes** (linked to SEC-CHEAT-02) |

### SEC-DB-02 — Hand Settlement Idempotency Check Is Not Atomic (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Database |
| **Location** | `CardGames.Poker.Api/Services/PlayerChipWalletService.cs` (lines 100-107) |
| **Why it's a problem** | The idempotency check (`AnyAsync` + insert) is not wrapped in a serializable transaction. Two concurrent settlement calls for the same hand could both pass the `AnyAsync` check and both insert ledger entries. The unique index (if present) would catch one, but the uncaught `DbUpdateException` would propagate as a 500 error. |
| **Remediation** | Use `IsolationLevel.Serializable` transaction, or rely on the unique index with proper exception handling and retry. |
| **Blocks launch** | No (partially mitigated by unique index) |

### SEC-DB-03 — No Audit Trail for Chip Additions via AddAccountChips (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Database / Auditability |
| **Location** | `CardGames.Poker.Api/Features/Profile/v1/Commands/AddAccountChips/AddAccountChipsCommandHandler.cs` |
| **Why it's a problem** | While a ledger entry is created, there is no mechanism to associate the addition with an external payment or purchase transaction. In a real-money system, chip purchases must be linked to payment processor transaction IDs. The current `TopUp` ledger entry records the amount but has no external reference. |
| **Remediation** | Add external transaction ID tracking. For real money, integrate with a payment processor and record the payment reference. |
| **Blocks launch** | No (architectural gap, not an exploit) |

---

## 8. Blazor / Client Security

### SEC-BLAZOR-01 — Blazor Frontend Sends Full User Identity in HTTP Headers (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Client Security |
| **Location** | `CardGames.Poker.Web/Infrastructure/AuthenticationStateHandler.cs` (lines 14-73) |
| **Why it's a problem** | The Blazor frontend's `AuthenticationStateHandler` DelegatingHandler adds `X-User-Id`, `X-User-Name`, `X-User-Email`, `X-User-DisplayName`, `X-Auth-Provider`, and `X-User-Authenticated` headers to every outgoing API request. While the intent is for server-to-server identity forwarding, these headers are visible in browser developer tools and can be replicated by any HTTP client. |
| **Impact** | Facilitates the header spoofing attacks described in SEC-AUTH-01. |
| **Remediation** | Replace with a cryptographic token exchange mechanism. The Blazor backend should request a short-lived JWT from an internal token service and forward that instead of raw identity headers. |
| **Blocks launch** | **Yes** (linked to SEC-AUTH-01) |

### SEC-BLAZOR-02 — SignalR Hub Client Sends User Identity in Headers and Query String (High)

| Field | Value |
|---|---|
| **Severity** | High |
| **Category** | Client Security |
| **Location** | `CardGames.Poker.Web/Services/GameHubClient.cs` (lines 117-136) |
| **Why it's a problem** | The GameHubClient adds `X-User-Id`, `X-User-Name`, `X-User-Email` headers to the SignalR connection. For WebSocket upgrade, some of these may end up in the URL as query parameters, exposing PII in server logs and browser history. |
| **Impact** | PII leakage in URLs and logs. Combined with SEC-AUTH-01, enables impersonation. |
| **Remediation** | Use JWT tokens for SignalR authentication instead of identity headers. |
| **Blocks launch** | **Yes** (linked to SEC-SIGNALR-01) |

### SEC-BLAZOR-03 — Development Configuration Enables DetailedErrors (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Client Security |
| **Location** | `CardGames.Poker.Api/appsettings.Development.json` (line 90: `"DetailedErrors": true`) |
| **Why it's a problem** | In development, detailed error messages are shown. If this configuration leaks to production, stack traces and internal details would be exposed to clients. |
| **Impact** | Information disclosure. |
| **Remediation** | Ensure `appsettings.Production.json` does not contain `DetailedErrors` and that the production environment is correctly configured. The current `appsettings.Production.json` is empty (`{}`), which is correct. |
| **Blocks launch** | No |

---

## 9. Secrets and Configuration

### SEC-SECRETS-01 — Azure AD Tenant ID and Client ID in Source Control (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Secrets |
| **Location** | `CardGames.Poker.Api/appsettings.json` (lines 2-14) |
| **Why it's a problem** | Azure AD B2C `TenantId` (`a7647f67-3916-4ad7-b0dd-3634131cc81b`) and `ClientId` (`9f72e28f-dbab-4dfe-b235-3901934816f5`) are committed to source control. While these are not secret in the same way as client secrets, they enable targeted phishing, brute-force attacks against the B2C tenant, and social engineering. |
| **Remediation** | Move to environment variables or Azure Key Vault for production. For open-source repos, consider using placeholder values. |
| **Blocks launch** | No |

### SEC-SECRETS-02 — MediatR License Key Hardcoded in appsettings.json and Program.cs (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Secrets |
| **Location** | `CardGames.Poker.Api/appsettings.json` (line 16), `CardGames.Poker.Api/Program.cs` (line 159) |
| **Why it's a problem** | The MediatR license key JWT is committed to source control in two places. While this is a commercial license key (not a security credential), its exposure could lead to license abuse. |
| **Remediation** | Move license keys to user secrets or environment variables. |
| **Blocks launch** | No |

### SEC-SECRETS-03 — Development Seed Users Have Real Email Addresses and Known Passwords (Medium)

| Field | Value |
|---|---|
| **Severity** | Medium |
| **Category** | Secrets / Privacy |
| **Location** | `CardGames.Poker.Api/appsettings.Development.json` (lines 2-82) |
| **Why it's a problem** | Development seed data includes what appear to be real people's email addresses (e.g., `lynnegibbens@gmail.com`) with a shared password (`Test1234!`). If these are real email addresses, this is a privacy concern. If the seed endpoint were accessible in production, these accounts would be created with known credentials. |
| **Remediation** | Use clearly fake email addresses for development (e.g., `user1@test.local`). Never use real email addresses in committed configuration. |
| **Blocks launch** | No |

### SEC-SECRETS-04 — Connection String in Web appsettings.json (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Secrets |
| **Location** | `CardGames.Poker.Web/appsettings.json` (line 3) |
| **Why it's a problem** | A localdb connection string is committed. While this is a development-only local database, committed connection strings set a bad precedent. |
| **Remediation** | Use environment variables or user secrets for connection strings. |
| **Blocks launch** | No |

---

## 10. Deployment / Production Hardening

### SEC-DEPLOY-01 — HSTS Only Enabled in Non-Development Environments (Positive)

The Blazor Web project correctly calls `app.UseHsts()` only in non-development (line 361 of `CardGames.Poker.Web/Program.cs`). However, the API project (`CardGames.Poker.Api/Program.cs`) does NOT call `UseHsts()` at all.

**Recommendation:** Add `app.UseHsts()` to the API project for production.

### SEC-DEPLOY-02 — Health Check Endpoint Unauthenticated (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Deployment |
| **Location** | `CardGames.Poker.Api/Program.cs` (line 350: `app.UseHealthChecks("/health")`) |
| **Why it's a problem** | Health check is publicly accessible. While typically intentional for load balancers, it could disclose database connectivity status. |
| **Remediation** | Verify health check response does not include sensitive details. Consider restricting to internal networks. |
| **Blocks launch** | No |

### SEC-DEPLOY-03 — Forwarded Headers Configuration Clears Known Networks/Proxies (Low)

| Field | Value |
|---|---|
| **Severity** | Low |
| **Category** | Deployment |
| **Location** | `CardGames.Poker.Web/Program.cs` (lines 336-344) |
| **Why it's a problem** | `KnownNetworks.Clear()` and `KnownProxies.Clear()` accept forwarded headers from any source. An attacker can spoof `X-Forwarded-For` to bypass IP-based restrictions. |
| **Remediation** | In production, configure `KnownNetworks` and `KnownProxies` to the actual reverse proxy addresses. |
| **Blocks launch** | No |

---

## 11. Fairness, Auditability, and Fraud Resistance

### Is the Game Engine Server-Authoritative?

**Partially.** The game state (cards, pots, betting rounds, phases) is stored server-side in SQL Server and manipulated exclusively by server-side handlers. Clients do not send card values or game state — they send action requests (bet, fold, call, raise, draw). This is the correct architecture.

**However**, the server does not verify WHO is requesting the action. The "server-authoritative" property breaks down because the server blindly executes requests from any source as the current player. This is effectively equivalent to letting clients choose which player to act as.

### Are Game State Transitions Enforced Centrally?

**Yes, with gaps.** Phase transitions (CollectingAntes → Dealing → BettingRound1 → ... → Showdown) are enforced by the `BaseGameFlowHandler` and variant-specific handlers. The server tracks the current phase and only accepts phase-appropriate actions.

**However**, without concurrency controls, two requests in the same phase can both be processed, causing phase-transition corruption.

### Is Every Balance-Changing Action Auditable?

**Partially.** The `PlayerChipLedgerEntry` system provides an audit trail for:
- TopUp (chip purchases)
- BringIn (buy-in validation)
- HandSettlement (per-hand win/loss)
- CashOut (leaving table)

**Gaps:**
- In-game AddChips has no audit trail linked to wallet
- No external payment reference for TopUp
- No IP address or device fingerprint recorded with actions

### Can Settlement Be Reconstructed After Disputes?

**Partially.** Hand history (`HandHistory`, `BettingActionRecord` entities) and chip ledger entries provide some reconstruction capability. However, the audit trail does not capture who initiated each API call (no IP logging, no request correlation).

### Is There Sufficient Logging for Cheating Investigation?

**No.** Current logging records game state changes but does not capture:
- Requesting user's identity per action
- IP addresses
- Client fingerprints
- Request timing patterns
- Suspicious behavior indicators
- Session/device associations

---

## A. Launch Blockers

The following issues **must** be fixed before going live with real money:

| # | ID | Issue | Category |
|---|---|---|---|
| 1 | SEC-AUTH-01 | Header-based auth trusts client-supplied identity | Authentication |
| 2 | SEC-AUTH-02 | CurrentUserService falls back to spoofable headers | Authentication |
| 3 | SEC-AUTHZ-01 | No global authorization policy; 71% of endpoints unprotected | Authorization |
| 4 | SEC-AUTHZ-02 | AddChips (in-game) has no auth and no ownership check | Authorization/Financial |
| 5 | SEC-AUTHZ-04 | Betting action handlers do not verify player identity | Authorization/Anti-Cheat |
| 6 | SEC-AUTHZ-05 | Draw handlers do not verify player identity | Authorization/Anti-Cheat |
| 7 | SEC-AUTHZ-06 | StartHand/DealHands endpoints have no authorization | Authorization |
| 8 | SEC-CHEAT-01 | Cryptographically weak RNG for card shuffling | Fairness |
| 9 | SEC-CHEAT-02 | No concurrency control on game actions | Race Conditions |
| 10 | SEC-CHEAT-04 | No idempotency on AddAccountChips | Financial |
| 11 | SEC-CHEAT-05 | Settlement not wrapped in database transaction | Financial |
| 12 | SEC-SIGNALR-01 | SignalR hubs use spoofable header auth | SignalR |
| 13 | SEC-SIGNALR-02 | GameHub JoinGame does not verify player membership | SignalR |
| 14 | SEC-API-01 | CORS allows any origin with credentials | API Security |
| 15 | SEC-AUTHZ-03 | Avatar upload allows anonymous access | Authorization |

---

## B. Anti-Cheat Architecture Review

### Is the Current Design Truly Server-Authoritative?

**No.** While game state is stored server-side and clients submit "requests" rather than "facts," the system fails the server-authoritative test in critical ways:

1. **Identity is client-controlled:** The server trusts client-supplied headers for user identity. This means the server cannot distinguish between Player A and an attacker impersonating Player A.

2. **Action attribution is broken:** Betting and draw actions are executed for "the current player" regardless of who submitted the request. The server correctly enforces turn order but does not enforce that the correct person is taking the turn.

3. **No mutual exclusion:** Without concurrency controls, the server processes duplicate or conflicting actions simultaneously.

### Every Place Where the Client Has Too Much Trust

| Trust Violation | Location | Impact |
|---|---|---|
| Client supplies user identity via headers | `HeaderAuthenticationHandler`, `CurrentUserService` | Full impersonation |
| Client controls SignalR identity | Hub connection query params | Private state access |
| Any client can submit game actions | All betting/draw endpoints | Action impersonation |
| Any client can trigger dealing | Deal/StartHand endpoints | Game flow control |
| Any client can add chips | AddChips endpoint | Unlimited chips |
| Client controls avatar upload | UploadAvatar endpoint | Storage abuse |
| Client can join any game's SignalR group | GameHub.JoinGame | Information leakage |
| Client supplies content type for uploads | AvatarStorageService | Content-type spoofing |

### Can a Determined Player Cheat Today?

**Yes, trivially.** A determined player with basic HTTP knowledge (cURL, browser dev tools) can:

1. **See all opponents' cards** by connecting to the GameHub with a spoofed identity
2. **Force opponents to fold** by submitting fold actions during their turns
3. **Add unlimited chips** to their account or game seat
4. **Predict future cards** by analyzing the weak PRNG
5. **Double-process bets** via race conditions
6. **Impersonate any player** in any action via header spoofing
7. **Disrupt any game** by triggering deals, showdowns, or settlements out of order

---

## C. Top 10 Highest-Risk Issues

Ranked by practical exploitability × impact:

| Rank | ID | Issue | Severity | Exploitability | Impact |
|---|---|---|---|---|---|
| 1 | SEC-AUTH-01 | Header auth trusts client headers | Critical | Trivial (cURL) | Full identity spoofing |
| 2 | SEC-AUTHZ-04 | Betting handlers don't verify player identity | Critical | Trivial | Control any player's actions |
| 3 | SEC-AUTHZ-01 | No global authorization; 71% endpoints open | Critical | Trivial | Anonymous access to everything |
| 4 | SEC-CHEAT-01 | Weak RNG (System.Random) for shuffling | Critical | Moderate (observation + math) | Predict all cards |
| 5 | SEC-API-01 | CORS allows any origin + credentials | Critical | Easy (malicious site) | Cross-site full compromise |
| 6 | SEC-AUTHZ-02 | Anonymous unlimited chip injection | Critical | Trivial (cURL) | Destroy game economy |
| 7 | SEC-SIGNALR-01 | SignalR uses spoofable header auth | Critical | Trivial (WebSocket) | See all private cards |
| 8 | SEC-CHEAT-02 | No concurrency on game actions | Critical | Moderate (parallel requests) | Double-spend, corrupt state |
| 9 | SEC-CHEAT-04 | No idempotency on chip additions | High | Easy (replay) | Infinite chip creation |
| 10 | SEC-CHEAT-05 | Settlement not transactional | High | Moderate (timing) | Phantom chip creation |

---

## D. Secure Rollout Checklist

Before public launch, verify each item:

### Authentication & Identity
- [ ] Header-based authentication is removed or restricted to verified internal traffic only
- [ ] `CurrentUserService` no longer falls back to HTTP headers
- [ ] JWT or token-based auth is used for all API and SignalR connections
- [ ] Account lockout is configured (max attempts, lockout duration)
- [ ] Email sender is implemented (not no-op)
- [ ] Password policy meets minimum strength requirements
- [ ] `EnablePiiLogging` is `false` in production

### Authorization
- [ ] Global fallback authorization policy requires authenticated users
- [ ] Every endpoint is explicitly `.RequireAuthorization()` or `.AllowAnonymous()` with justification
- [ ] All game action handlers verify the authenticated user matches the acting player
- [ ] Game host/dealer privileges are checked for deal/start/settings operations
- [ ] In-game AddChips requires authentication and ownership verification
- [ ] Avatar upload requires authentication

### Anti-Cheat & Game Integrity
- [ ] `StandardRandomNumberGenerator` replaced with `System.Security.Cryptography.RandomNumberGenerator`
- [ ] `Dealer` constructor requires injected cryptographic RNG
- [ ] All game-state-modifying handlers use explicit database transactions
- [ ] `DbUpdateConcurrencyException` is caught and handled in all game handlers
- [ ] Distributed lock (e.g., Redis) serializes game mutations per game ID
- [ ] Idempotency keys on all financial operations (AddAccountChips, settlements)
- [ ] Player identity verified in ALL action handlers (bet, fold, call, raise, draw, discard)

### SignalR
- [ ] All hubs use JWT-based authentication
- [ ] `GameHub.JoinGame` verifies caller is a member of the game
- [ ] Private state is only sent to verified game participants
- [ ] Hub methods validate all parameters server-side

### API Hardening
- [ ] CORS restricted to exact frontend origin(s) in production
- [ ] Scalar/OpenAPI reference gated behind development check
- [ ] Prometheus endpoint restricted or authenticated
- [ ] Global rate limiting applied
- [ ] Anti-forgery tokens for state-changing operations
- [ ] File upload validates content (magic numbers, re-encoding)

### Database
- [ ] All financial operations wrapped in transactions
- [ ] Concurrency tokens enforced with proper retry logic
- [ ] Audit trail includes requesting user, IP, timestamp for all balance changes
- [ ] Connection strings stored in environment variables or Key Vault

### Deployment
- [ ] HSTS enabled on API project
- [ ] `DetailedErrors` is `false` in production
- [ ] Development seed endpoint unreachable in production
- [ ] Health checks do not leak sensitive information
- [ ] Forwarded headers configured with known proxies
- [ ] Azure AD credentials and tenant info not committed to source

### Monitoring & Fraud Detection
- [ ] Request logging includes user identity, IP, and correlation ID
- [ ] Alerting on unusual patterns (rapid actions, impossible timing, large chip movements)
- [ ] Hand history sufficient for dispute resolution
- [ ] Admin tools for investigating suspected cheating

---

## E. Missing Protections

Even if no specific bug is visible today, the following critical protections are entirely absent:

| Protection | Status | Why It Matters |
|---|---|---|
| **Player action signing** | Missing | No mechanism to prove a player intentionally took an action (for dispute resolution) |
| **Distributed game locks** | Missing | No mechanism to serialize game mutations across multiple API instances |
| **IP/device fingerprinting** | Missing | Cannot detect multi-accounting or collusion |
| **Suspicious behavior detection** | Missing | No automated flagging of impossible timing, win-rate anomalies, or pattern indicators |
| **Rate limiting on game actions** | Missing | No throttle on betting, dealing, or game creation |
| **Session binding** | Missing | No mechanism to bind a game session to a specific browser/device/IP |
| **Action replay protection** | Missing | No nonce or sequence number on game actions |
| **Spectator vs player separation** | Missing | No spectator mode with properly restricted state |
| **Multi-table abuse prevention** | Missing | No limit on concurrent game sessions per player |
| **Payment processor integration** | Missing | Chip purchases have no external payment validation |
| **KYC / identity verification** | Missing | No age/identity verification for real-money gambling compliance |
| **Regulatory compliance framework** | Missing | No jurisdiction-specific gambling regulation compliance |
| **External RNG audit capability** | Missing | No mechanism for third-party RNG certification |
| **Game outcome logging for regulatory audit** | Missing | Hand histories are not in a format suitable for regulatory submission |
| **Encrypted PII at rest** | Missing | Player PII (email, name) stored in plaintext in database |
| **Anti-collusion detection** | Missing | No analysis of correlated player behavior across tables |
| **Responsible gambling controls** | Missing | No self-exclusion, deposit limits, or session time limits |

---

*End of Security Review*
