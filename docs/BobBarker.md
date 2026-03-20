# Bob Barker

Bob Barker is a production-ready Hold'Em-family poker variant built on Omaha-style betting flow with a separate showcase side contest.

## Rules Summary

- Each player posts blinds using the standard Hold'Em/Omaha blind flow.
- Each player receives five face-down hole cards.
- The dealer places one hidden card in the center as the showcase target card.
- Before pre-flop betting, each player selects exactly one of their five hole cards as a showcase card.
- The remaining four hole cards play like Omaha: the main hand must use exactly two hole cards plus three community cards.
- At showdown, the pot splits in half:
  - One half goes to the best traditional poker hand.
  - One half goes to the showcase card closest to the dealer card without going over.
- Showcase Ace handling is low by default and becomes high only when the dealer card is an Ace.

## Implementation Notes

- Bob Barker uses a dedicated showcase-selection endpoint instead of reusing a generic draw or discard endpoint.
- The selected showcase card is persisted in variant state so it can be hidden during play, excluded from main-hand evaluation, and revealed in showdown results.
- The hidden dealer showcase card remains face-down in public table state until showdown.
- Public showdown payloads include Bob Barker-specific data for the dealer card, showcase winners, showcase card values, and showcase payout amounts.
- The web UI reuses the existing draw-panel seam for showcase selection and extends the showdown overlay for Bob Barker-specific results.

## Main Touchpoints

- Domain game and rules: `src/CardGames.Poker/Games/BobBarker/`
- API flow handler: `src/CardGames.Poker.Api/GameFlow/BobBarkerFlowHandler.cs`
- Bob Barker feature endpoints: `src/CardGames.Poker.Api/Features/Games/BobBarker/`
- Showdown settlement: `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- State projection: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
- Web router: `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`
- Table UI and showdown display: `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` and `src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor`

## Verification

Recommended verification commands:

```bash
dotnet build src/CardGames.sln
dotnet test src/CardGames.sln
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj -nologo --no-restore --filter "FullyQualifiedName~GameApiRouterTests|FullyQualifiedName~BobBarkerShowdownOverlayTests"
dotnet build src/CardGames.Poker.Refitter
```

The Bob Barker web-facing tests cover router selection for betting/showcase actions and Bob Barker showdown overlay ordering/display-name behavior.