# Stateless Integration: Requirements, Architecture, and Design

## 1. Purpose

Define how the Stateless state machine library integrates with the existing GameRules model (including phase categories) and how AutoActionService relies on machine triggers instead of hardcoded phase sets. The goal is to make poker-variant game flow explicit, testable, and extensible with minimal code changes for new variants.

## 2. Scope

In scope:
- Poker variants only (same scope as current GameRules and IGameFlowHandler work).
- Phase transitions, phase categories, and trigger-based routing.
- AutoActionService refactor to consult a flow machine instead of phase string sets.

Out of scope:
- Non-poker game support (requires new abstractions).
- UI redesign or non-flow gameplay logic.
- Database schema changes.

## 3. Requirements

### 3.1 Functional Requirements

- FR1: The system shall define all valid phase transitions for a game type via Stateless state machines.
- FR2: The system shall build state machines from GameRules when possible, with support for custom guarded transitions.
- FR3: The system shall expose phase category metadata for any phase in the machine.
- FR4: AutoActionService shall determine which command to run by evaluating allowed triggers from the machine, not by hardcoded phase sets.
- FR5: The system shall validate attempted transitions and reject invalid transitions with clear errors.
- FR6: Each game type shall be able to override or extend transitions without changing global services.

### 3.2 Non-Functional Requirements

- NFR1: Transitions must be deterministic and thread-safe per game instance.
- NFR2: State machine creation must be fast and cacheable per game type.
- NFR3: The design should remain compatible with MediatR command handlers.
- NFR4: The implementation should remain easy to test with unit tests for transition validity.

## 4. Assumptions

- GameRules.Phases is the source of truth for phase ordering and categories.
- Phase categories are required for AutoAction routing (Betting, Drawing, Decision, Special, Resolution).
- Existing game-specific logic remains in domain services and handlers, not in the state machine.

## 5. High-Level Architecture

Components:
- GameRules: phase descriptors with category and valid next phases.
- IGameFlowStateMachineFactory: builds and caches Stateless machines per game type.
- IGameFlowHandler: wraps the machine to drive transitions and custom guards.
- AutoActionService: queries allowed triggers from machine to select action.

Data flow:
1) Load Game and GameRules.
2) Build or retrieve cached machine for game type.
3) Machine computes allowed triggers from current phase.
4) AutoActionService selects a trigger and invokes the corresponding command.
5) The command updates game state and fires the trigger (or returns error).

## 6. GameRules Model Additions

### 6.1 Phase Descriptor

Extend GameRules to describe categories and transitions explicitly.

```csharp
public sealed class GamePhaseDescriptor
{
    public required string PhaseId { get; init; }
    public required PhaseCategory CategoryType { get; init; }
    public IReadOnlyList<string> ValidNextPhases { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Triggers { get; init; } = Array.Empty<string>();
}
```

Notes:
- ValidNextPhases expresses topology.
- Triggers optionally enumerates named triggers; if empty, a default trigger name is synthesized per next phase.

### 6.2 PhaseCategory

```csharp
public enum PhaseCategory
{
    Setup,
    Collection,
    Dealing,
    Betting,
    Drawing,
    Decision,
    Special,
    Resolution
}
```

## 7. Stateless Integration Design

### 7.1 Core Interfaces

```csharp
public interface IGameFlowStateMachine
{
    string CurrentState { get; }
    IReadOnlyList<string> GetPermittedTriggers(string state);
    string Fire(string trigger);
    PhaseCategory GetCategory(string state);
}

public interface IGameFlowStateMachineFactory
{
    IGameFlowStateMachine Create(GameRules rules);
}
```

### 7.2 Stateless Machine Implementation

```csharp
using Stateless;

public sealed class PokerFlowStateMachine : IGameFlowStateMachine
{
    private readonly StateMachine<string, string> _machine;
    private readonly IReadOnlyDictionary<string, PhaseCategory> _categories;

    public PokerFlowStateMachine(GameRules rules)
    {
        _machine = new StateMachine<string, string>(nameof(Phases.WaitingToStart));
        _categories = rules.Phases.ToDictionary(p => p.PhaseId, p => p.CategoryType, StringComparer.OrdinalIgnoreCase);

        foreach (var phase in rules.Phases)
        {
            var config = _machine.Configure(phase.PhaseId);

            if (phase.Triggers.Count > 0)
            {
                foreach (var trigger in phase.Triggers)
                {
                    var next = ResolveNextPhase(phase, trigger);
                    if (next != null)
                    {
                        config.Permit(trigger, next);
                    }
                }
            }
            else
            {
                foreach (var next in phase.ValidNextPhases)
                {
                    var trigger = $"Next:{next}";
                    config.Permit(trigger, next);
                }
            }
        }
    }

    public string CurrentState => _machine.State;

    public IReadOnlyList<string> GetPermittedTriggers(string state)
    {
        _machine.State = state;
        return _machine.PermittedTriggers.ToList();
    }

    public string Fire(string trigger)
    {
        _machine.Fire(trigger);
        return _machine.State;
    }

    public PhaseCategory GetCategory(string state)
    {
        return _categories.TryGetValue(state, out var category) ? category : PhaseCategory.Special;
    }

    private static string? ResolveNextPhase(GamePhaseDescriptor phase, string trigger)
    {
        // Map trigger to next phase. If Triggers are explicit, keep a parallel mapping in GameRules.
        // Placeholder: require trigger names to match "Next:{phaseId}" when Triggers is used.
        return phase.ValidNextPhases.FirstOrDefault(p => string.Equals($"Next:{p}", trigger, StringComparison.OrdinalIgnoreCase));
    }
}
```

### 7.3 Factory and Caching

```csharp
public sealed class GameFlowStateMachineFactory : IGameFlowStateMachineFactory
{
    private readonly ConcurrentDictionary<string, IGameFlowStateMachine> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IGameFlowStateMachine Create(GameRules rules)
    {
        return _cache.GetOrAdd(rules.GameTypeCode, _ => new PokerFlowStateMachine(rules));
    }
}
```

### 7.4 IGameFlowHandler Usage

```csharp
public sealed class SevenCardStudFlowHandler : IGameFlowHandler
{
    private readonly IGameFlowStateMachineFactory _factory;

    public SevenCardStudFlowHandler(IGameFlowStateMachineFactory factory)
    {
        _factory = factory;
    }

    public string GetNextPhase(Game game, string currentPhase, string trigger)
    {
        var rules = GetGameRules();
        var machine = _factory.Create(rules);
        machine.GetPermittedTriggers(currentPhase);
        return machine.Fire(trigger);
    }

    public PhaseCategory GetPhaseCategory(Game game, string phase)
    {
        var machine = _factory.Create(GetGameRules());
        return machine.GetCategory(phase);
    }
}
```

## 8. AutoActionService Refactor Design

### 8.1 Current Problem

AutoActionService uses hardcoded phase string sets (BettingPhases, DrawPhases, DropOrStayPhases). This breaks OCP for new game types and phases.

### 8.2 New Strategy

AutoActionService should:
- Resolve the game flow machine for the game type.
- Ask for permitted triggers for the current phase.
- Pick a trigger based on policy and phase category.
- Execute the command mapped to that trigger.

### 8.3 Trigger-to-Command Mapping

Define a single registry mapping triggers to MediatR commands.

```csharp
public interface IAutoActionRouter
{
    Task<bool> TryExecuteAsync(Game game, string trigger, CancellationToken ct);
}

public sealed class AutoActionRouter : IAutoActionRouter
{
    private readonly IMediator _mediator;

    public async Task<bool> TryExecuteAsync(Game game, string trigger, CancellationToken ct)
    {
        return trigger switch
        {
            "StartHand" => await SendStartHand(game, ct),
            "CollectAntes" => await SendCollectAntes(game, ct),
            "Deal" => await SendDeal(game, ct),
            "BettingAction" => await SendBettingAction(game, ct),
            "Draw" => await SendDraw(game, ct),
            "Showdown" => await SendShowdown(game, ct),
            _ => false
        };
    }

    private Task<bool> SendStartHand(Game game, CancellationToken ct) =>
        _mediator.Send(new StartHandCommand(game.Id), ct).ContinueWith(t => t.Result.IsT0);

    // Additional command wrappers omitted for brevity.
}
```

### 8.4 AutoActionService Flow

```csharp
public sealed class AutoActionService
{
    private readonly IGameFlowHandlerFactory _flowHandlerFactory;
    private readonly IAutoActionRouter _router;

    public async Task TryAutoAdvanceAsync(Game game, CancellationToken ct)
    {
        var flowHandler = _flowHandlerFactory.GetHandler(game.GameType?.Code ?? "FIVECARDDRAW");
        var phase = game.CurrentPhase;

        var rules = flowHandler.GetGameRules();
        var machine = flowHandler.GetStateMachine(rules);
        var triggers = machine.GetPermittedTriggers(phase);

        var category = machine.GetCategory(phase);
        var ordered = OrderTriggers(triggers, category);

        foreach (var trigger in ordered)
        {
            if (await _router.TryExecuteAsync(game, trigger, ct))
            {
                return;
            }
        }
    }

    private static IReadOnlyList<string> OrderTriggers(IReadOnlyList<string> triggers, PhaseCategory category)
    {
        // Example ordering per category. Adjust to match your behavior.
        return category switch
        {
            PhaseCategory.Collection => Prefer(triggers, "CollectAntes"),
            PhaseCategory.Dealing => Prefer(triggers, "Deal"),
            PhaseCategory.Betting => Prefer(triggers, "BettingAction"),
            PhaseCategory.Drawing => Prefer(triggers, "Draw"),
            PhaseCategory.Resolution => Prefer(triggers, "Showdown"),
            _ => triggers
        };
    }

    private static IReadOnlyList<string> Prefer(IReadOnlyList<string> triggers, string preferred)
    {
        if (!triggers.Contains(preferred, StringComparer.OrdinalIgnoreCase))
        {
            return triggers;
        }

        return new[] { preferred }.Concat(triggers.Where(t => !string.Equals(t, preferred, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
```

### 8.5 Trigger Definitions

Recommended canonical triggers:
- StartHand
- CollectAntes
- Deal
- BettingAction
- Draw
- Showdown
- CompleteHand
- Special:DropOrStay
- Special:PotMatching
- Special:PlayerVsDeck

Each game type can optionally define additional trigger names in GameRules.

## 9. GameRules Example with Categories and Triggers

```csharp
public static GameRules CreateGameRules() => new()
{
    GameTypeCode = "FIVECARDDRAW",
    Phases = new List<GamePhaseDescriptor>
    {
        new()
        {
            PhaseId = nameof(Phases.CollectingAntes),
            CategoryType = PhaseCategory.Collection,
            ValidNextPhases = [nameof(Phases.Dealing)],
            Triggers = ["CollectAntes"]
        },
        new()
        {
            PhaseId = nameof(Phases.Dealing),
            CategoryType = PhaseCategory.Dealing,
            ValidNextPhases = [nameof(Phases.FirstBettingRound)],
            Triggers = ["Deal"]
        },
        new()
        {
            PhaseId = nameof(Phases.FirstBettingRound),
            CategoryType = PhaseCategory.Betting,
            ValidNextPhases = [nameof(Phases.DrawPhase), nameof(Phases.Showdown)],
            Triggers = ["BettingAction", "Showdown"]
        },
        new()
        {
            PhaseId = nameof(Phases.DrawPhase),
            CategoryType = PhaseCategory.Drawing,
            ValidNextPhases = [nameof(Phases.Showdown)],
            Triggers = ["Draw"]
        },
        new()
        {
            PhaseId = nameof(Phases.Showdown),
            CategoryType = PhaseCategory.Resolution,
            ValidNextPhases = [nameof(Phases.Complete)],
            Triggers = ["Showdown"]
        }
    }
};
```

## 10. Error Handling

- If AutoActionService requests a trigger not permitted for the current state, the machine throws InvalidOperationException. Catch and log with game id and phase.
- If GameRules are missing a phase, the machine should treat it as PhaseCategory.Special and allow no triggers.

## 11. Testing Strategy

- Unit tests per game type ensure transition graph is correct.
- Unit tests for AutoActionService verify trigger ordering per PhaseCategory.
- Integration tests compare old behavior to new for common phases (StartHand, Deal, Showdown).

## 12. Migration Plan

1) Introduce PhaseCategory and ValidNextPhases in GameRules.
2) Implement Stateless state machine and factory.
3) Add triggers to GameRules for existing poker variants.
4) Update IGameFlowHandler to use the machine.
5) Refactor AutoActionService to query triggers instead of hardcoded sets.
6) Add tests and feature flag for rollback.

## 13. Open Questions

- Do we standardize triggers globally, or allow per-game trigger naming with a mapping table?
- Should trigger mapping to commands live in AutoActionService or a dedicated router?
- Do we require GameRules to always include ValidNextPhases for all phases?
