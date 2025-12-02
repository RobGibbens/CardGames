# CardGames Documentation

Welcome to the CardGames documentation! This directory contains comprehensive documentation for developers, players, and contributors.

## Documentation Index

### For Players

- **[User Guide](USER_GUIDE.md)** - Complete guide to playing poker on CardGames
  - Game variants (Hold'em, Omaha, Stud, etc.)
  - Betting actions and controls
  - Chat and communication features
  - Settings and preferences

### For Developers

- **[Developer Setup Guide](DEVELOPER_SETUP.md)** - Getting started with local development
  - Prerequisites and installation
  - Building and running the application
  - Running tests
  - Debugging tips

- **[SignalR API Documentation](SIGNALR_API.md)** - Real-time communication API reference
  - Hub methods (client → server)
  - Client events (server → client)
  - Event payloads
  - Best practices

- **[Integration Test Suite](INTEGRATION_TESTS.md)** - End-to-end testing documentation
  - Test coverage overview
  - Gameplay flow tests
  - Writing new tests

- **[Performance Testing Guide](PERFORMANCE_TESTING.md)** - Load and performance testing
  - Load testing SignalR connections
  - Benchmark testing
  - Metrics and monitoring
  - Performance targets

### Architecture

- **[Architecture Decision Records (ADRs)](adr/README.md)** - Key design decisions
  - [ADR-001: Use SignalR for Real-Time Communication](adr/001-signalr-for-realtime.md)
  - [ADR-002: Vertical Slice Architecture](adr/002-vertical-slice-architecture.md)
  - [ADR-003: Generic Card Type Architecture](adr/003-generic-card-type.md)
  - [ADR-004: Event-Driven Game State Management](adr/004-event-driven-game-state.md)
  - [ADR-005: Connection Tracking for Player Sessions](adr/005-connection-tracking.md)

- **[Architecture Overview](../architecture/README.md)** - Solution structure and design patterns

## Quick Links

| Task | Document |
|------|----------|
| Set up development environment | [Developer Setup Guide](DEVELOPER_SETUP.md) |
| Understand the SignalR API | [SignalR API Documentation](SIGNALR_API.md) |
| Learn to play poker on CardGames | [User Guide](USER_GUIDE.md) |
| Run performance tests | [Performance Testing Guide](PERFORMANCE_TESTING.md) |
| Understand architecture decisions | [ADRs](adr/README.md) |

## Contributing to Documentation

When adding new documentation:

1. Use Markdown format
2. Follow the existing document structure
3. Add links to the documentation index (this file)
4. Include code examples where relevant
5. Keep content up-to-date with code changes

## Related Resources

- [Main README](../README.md) - Project overview and getting started
- [Copilot Instructions](../src/copilot_instructions.md) - Coding conventions and guidelines
- [NuGet Packages](https://www.nuget.org/profiles/EluciusFTW) - Published library packages
