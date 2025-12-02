# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for the CardGames project. ADRs document significant architectural decisions made during development.

## Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](001-signalr-for-realtime.md) | Use SignalR for Real-Time Communication | Accepted | 2024-01-01 |
| [ADR-002](002-vertical-slice-architecture.md) | Vertical Slice Architecture | Accepted | 2024-01-01 |
| [ADR-003](003-generic-card-type.md) | Generic Card Type Architecture | Accepted | 2024-01-01 |
| [ADR-004](004-event-driven-game-state.md) | Event-Driven Game State Management | Accepted | 2024-01-01 |
| [ADR-005](005-connection-tracking.md) | Connection Tracking for Player Sessions | Accepted | 2024-01-01 |

## ADR Template

When creating a new ADR, use this template:

```markdown
# ADR-NNN: Title

## Status

[Proposed | Accepted | Deprecated | Superseded]

## Context

What is the issue that we're seeing that is motivating this decision?

## Decision

What is the change that we're proposing and/or doing?

## Consequences

What becomes easier or more difficult to do because of this change?

## Alternatives Considered

What other options were considered and why were they rejected?
```
