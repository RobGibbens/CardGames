# Basher History

## Project Learnings (from import)
- Test style: xUnit + FluentAssertions under src/Tests.
- Requested by: Rob Gibbens.
- Solution-level verification commands use dotnet build/test in src/CardGames.sln.

## Learnings

- OAuth-first auth page prioritization was consolidated in `.ai-team/decisions.md`; regression checks should ensure external providers remain primary while local forms stay available as fallback.
