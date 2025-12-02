# Performance Testing Guide

This guide documents the approach to performance and load testing for the CardGames application, focusing on concurrent connection handling and real-time communication.

## Table of Contents

- [Overview](#overview)
- [Test Categories](#test-categories)
- [Load Testing SignalR](#load-testing-signalr)
- [Benchmark Testing](#benchmark-testing)
- [Metrics and Monitoring](#metrics-and-monitoring)
- [Performance Targets](#performance-targets)
- [Running Performance Tests](#running-performance-tests)
- [Interpreting Results](#interpreting-results)

---

## Overview

Performance testing for CardGames focuses on three areas:

1. **SignalR Connection Capacity** - How many concurrent players can connect
2. **Real-Time Latency** - How quickly events reach clients
3. **Domain Logic Performance** - Hand evaluation and simulation speed

---

## Test Categories

### 1. Connection Load Tests

Test the maximum number of concurrent SignalR connections:

| Scenario | Description | Target |
|----------|-------------|--------|
| Idle connections | Connected clients not actively playing | 10,000+ |
| Active gameplay | Players at tables, receiving events | 1,000+ |
| Peak traffic | Simultaneous connections during peak | 5,000+ |

### 2. Message Throughput Tests

Test event broadcast capacity:

| Scenario | Description | Target |
|----------|-------------|--------|
| Betting round | 10-player table, actions broadcast | <50ms latency |
| Showdown | Hand reveals, animations | <100ms latency |
| Tournament | 100 tables, synchronized events | <200ms latency |

### 3. Hand Evaluation Benchmarks

Test core poker logic performance:

| Scenario | Description | Target |
|----------|-------------|--------|
| Single hand | Evaluate one 7-card hand | <1μs |
| Simulation (1K hands) | Monte Carlo simulation | <100ms |
| Simulation (100K hands) | Full equity calculation | <10s |

---

## Load Testing SignalR

### Tool: k6 with xk6-websocket

[k6](https://k6.io/) is a modern load testing tool that supports WebSocket connections.

#### Installation

```bash
# Install k6
brew install k6  # macOS
# or
sudo apt-get install k6  # Linux

# Install xk6-websocket extension
xk6 build --with github.com/grafana/xk6-websocket
```

#### Basic Connection Test

```javascript
// load-test-connections.js
import { WebSocket } from 'k6/experimental/websockets';
import { check } from 'k6';

export const options = {
    stages: [
        { duration: '1m', target: 100 },   // Ramp up to 100 connections
        { duration: '5m', target: 100 },   // Hold at 100 connections
        { duration: '1m', target: 500 },   // Ramp up to 500 connections
        { duration: '5m', target: 500 },   // Hold at 500 connections
        { duration: '1m', target: 0 },     // Ramp down
    ],
};

export default function () {
    const url = 'wss://localhost:7001/gamehub';
    const ws = new WebSocket(url);
    
    ws.onopen = () => {
        console.log('Connected');
        
        // Join lobby
        ws.send(JSON.stringify({
            protocol: 'json',
            version: 1,
            type: 1,
            target: 'JoinLobby',
            arguments: []
        }));
    };
    
    ws.onmessage = (e) => {
        const message = JSON.parse(e.data);
        check(message, {
            'received message': (m) => m !== null,
        });
    };
    
    ws.onerror = (e) => {
        console.log('Error:', e.message);
    };
    
    // Keep connection open for duration
    sleep(600); // 10 minutes
    
    ws.close();
}
```

#### Active Gameplay Test

```javascript
// load-test-gameplay.js
import { WebSocket } from 'k6/experimental/websockets';
import { sleep } from 'k6';

export const options = {
    stages: [
        { duration: '2m', target: 50 },  // 50 concurrent players
        { duration: '10m', target: 50 }, // Sustained gameplay
        { duration: '1m', target: 0 },
    ],
};

const tableId = 'test-table-' + __VU;

export default function () {
    const ws = new WebSocket('wss://localhost:7001/gamehub');
    
    ws.onopen = () => {
        // Join table
        ws.send(JSON.stringify({
            protocol: 'json',
            version: 1,
            type: 1,
            target: 'JoinTable',
            arguments: [tableId, 'Player' + __VU]
        }));
    };
    
    ws.onmessage = (e) => {
        const message = JSON.parse(e.data);
        
        // Simulate player action when it's their turn
        if (message.target === 'PlayerTurn' && 
            message.arguments[0].playerName === 'Player' + __VU) {
            
            // Random delay to simulate thinking
            sleep(Math.random() * 5);
            
            // Send action
            ws.send(JSON.stringify({
                protocol: 'json',
                version: 1,
                type: 1,
                target: 'Check',
                arguments: [tableId]
            }));
        }
    };
    
    sleep(600);
    ws.close();
}
```

#### Running k6 Tests

```bash
# Basic test
k6 run load-test-connections.js

# With custom thresholds
k6 run --vus 100 --duration 5m load-test-connections.js

# Output to JSON
k6 run --out json=results.json load-test-connections.js
```

### Tool: NBomber (.NET)

For .NET-native load testing, use [NBomber](https://nbomber.com/):

```csharp
// LoadTests/SignalRLoadTest.cs
using NBomber.Contracts;
using NBomber.CSharp;
using Microsoft.AspNetCore.SignalR.Client;

public class SignalRLoadTest
{
    public static void Run()
    {
        var scenario = Scenario.Create("signalr_connections", async context =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7001/gamehub")
                .Build();
            
            await connection.StartAsync();
            
            await connection.InvokeAsync("JoinLobby");
            
            // Measure latency of heartbeat
            var start = DateTime.UtcNow;
            await connection.InvokeAsync("Heartbeat");
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;
            
            await connection.StopAsync();
            
            return Response.Ok(sizeBytes: 0, latencyMs: latency);
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30)),
            Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromMinutes(5)),
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30))
        );
        
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
```

---

## Benchmark Testing

### BenchmarkDotNet for Domain Logic

The project uses BenchmarkDotNet for micro-benchmarks:

```bash
cd src/CardGames.Core.Benchmarks
dotnet run -c Release
```

### Example Benchmark

```csharp
[MemoryDiagnoser]
public class HandEvaluationBenchmarks
{
    private readonly Card[] _sevenCards;
    
    public HandEvaluationBenchmarks()
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();
        _sevenCards = dealer.DealCards(7).ToArray();
    }
    
    [Benchmark]
    public HandType EvaluateHoldemHand()
    {
        var hand = new HoldemHand(_sevenCards[..2], _sevenCards[2..7]);
        return hand.Type;
    }
    
    [Benchmark]
    public long CalculateHandStrength()
    {
        var hand = new HoldemHand(_sevenCards[..2], _sevenCards[2..7]);
        return hand.Strength;
    }
    
    [Benchmark]
    public HoldemSimulationResult SimulateThousandHands()
    {
        return new HoldemSimulation()
            .WithPlayer("A", "Ah Kh".ToCards())
            .WithPlayer("B", "Qd Qc".ToCards())
            .SimulateWithFullDeck(1000);
    }
}
```

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- --filter *EvaluateHoldemHand*

# Generate markdown report
dotnet run -c Release -- --exporters markdown
```

---

## Metrics and Monitoring

### Key Metrics to Track

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `signalr_connections_total` | Current connection count | >80% capacity |
| `signalr_message_latency_ms` | P95 message delivery time | >100ms |
| `signalr_messages_sent_total` | Messages sent per second | Baseline +50% |
| `http_request_duration_ms` | API response time | P95 >200ms |
| `gc_pause_time_ms` | GC pause duration | >50ms |

### ASP.NET Core Metrics

Enable metrics in `Program.cs`:

```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConnectionMappingService, ConnectionMappingService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("signalr", () =>
    {
        var connections = serviceProvider.GetRequiredService<IConnectionMappingService>();
        // Custom health check logic
        return HealthCheckResult.Healthy();
    });
```

### Grafana Dashboard

For production monitoring, export metrics to Prometheus and visualize in Grafana:

```yaml
# docker-compose.yml
services:
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    ports:
      - "9090:9090"
  
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
```

---

## Performance Targets

### Connection Targets

| Deployment | Target Connections | Expected Latency |
|------------|-------------------|------------------|
| Development | 100 | <20ms |
| Staging | 1,000 | <50ms |
| Production (single) | 5,000 | <100ms |
| Production (scaled) | 50,000+ | <100ms |

### Message Latency Targets

| Event Type | P50 | P95 | P99 |
|------------|-----|-----|-----|
| Betting Action | 10ms | 30ms | 50ms |
| Card Dealt | 15ms | 40ms | 75ms |
| Showdown | 20ms | 60ms | 100ms |
| Chat Message | 30ms | 75ms | 150ms |

### Throughput Targets

| Scenario | Messages/Second |
|----------|-----------------|
| Single table (10 players) | 100 |
| 100 active tables | 10,000 |
| Tournament (1000 players) | 50,000 |

---

## Running Performance Tests

### Prerequisites

1. Build in Release mode:
   ```bash
   dotnet build -c Release
   ```

2. Start the application:
   ```bash
   dotnet run -c Release --project CardGames.Poker.Api
   ```

### Quick Benchmark

```bash
cd src/CardGames.Core.Benchmarks
dotnet run -c Release --filter *Quick*
```

### Full Benchmark Suite

```bash
cd src/CardGames.Core.Benchmarks
dotnet run -c Release -- --job short
```

### Load Test

```bash
# Using k6
k6 run --vus 50 --duration 5m load-test.js

# Using NBomber
dotnet run --project LoadTests/SignalRLoadTest.csproj
```

---

## Interpreting Results

### Benchmark Results

```
|            Method |      Mean |    Error |   StdDev | Allocated |
|------------------ |----------:|---------:|---------:|----------:|
| EvaluateHoldemHand|   0.82 μs | 0.016 μs | 0.015 μs |         - |
| SimulateThousand  |  85.32 ms | 1.643 ms | 1.537 ms |   2.41 MB |
```

**Key indicators:**
- **Mean** - Average execution time
- **Error** - Standard error margin
- **Allocated** - Memory allocated per operation

### Load Test Results

```
scenarios: signalr_connections
  - duration: 00:05:00
  - step_name: sustained_load
  - ok: 2500
  - fail: 0
  - rps: 8.33
  - latency_p50: 12 ms
  - latency_p95: 45 ms
  - latency_p99: 78 ms
```

**Key indicators:**
- **ok/fail** - Successful vs failed operations
- **rps** - Requests per second achieved
- **latency_p95** - 95th percentile latency (most important)

### When to Investigate

- P95 latency exceeds target by >20%
- Error rate >0.1%
- Memory allocation increasing linearly (potential leak)
- GC pauses affecting latency

---

## Best Practices

1. **Run benchmarks on isolated hardware** - Avoid running on development machines with other processes

2. **Use Release configuration** - Debug builds have significant overhead

3. **Warm up before measuring** - BenchmarkDotNet handles this automatically

4. **Test with realistic data** - Use production-like card combinations and player counts

5. **Monitor memory** - Use `[MemoryDiagnoser]` to track allocations

6. **Establish baselines** - Commit baseline results and compare future runs

7. **Test failure scenarios** - Include tests for disconnections, timeouts

8. **Scale gradually** - Ramp up load slowly to identify breaking points
