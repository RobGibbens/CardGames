# Razz

Razz is a Stud-style, ante-based variant added with game type code `RAZZ`.

## Rules

- Deal pattern matches Seven Card Stud:
  - Third street: 2 down + 1 up
  - Fourth, fifth, sixth: 1 up each
  - Seventh: 1 down
- Each player builds the best five-card low from seven cards.
- Aces are low.
- Straights and flushes do not count against low hands.
- There is no eight-or-better qualifier.

## Implementation Notes

- Domain metadata and rules live under `src/CardGames.Poker/Games/Razz`.
- API flow uses `RazzFlowHandler` and reuses Stud-style command paths where behavior matches.
- Showdown output for Razz is formatted as explicit low-hand text (for example `6-4-3-2-1 low`).
- Odds support includes a dedicated `CalculateRazzOdds` path.

## Web Integration

- `RAZZ` is wired into game routing and Stud-style play handling.
- The game uses image name `razz.png` in metadata and web assets.

## Test Coverage Added

- Unit tests for game metadata/rules.
- Unit tests for Razz hand evaluation behavior.
- Unit tests for Razz odds calculation.
- Integration tests for Razz flow handler and start/phase progression.
