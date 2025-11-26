# Strada Framework

**A high-performance Unity framework unifying MVCS architecture with ECS simulation**

[![Tests](https://img.shields.io/badge/tests-352%20passing-brightgreen)]()
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue)]()
[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-purple)]()

Strada combines enterprise-grade dependency injection with performance-critical ECS, wrapped in a clean modular architecture. Build UI with familiar MVCS patterns while using ECS for high-performance simulation—without choosing between paradigms.

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Performance](#performance)
- [Documentation](#documentation)
- [Architecture](#architecture)
- [API Reference](#api-reference)
- [Testing](#testing)
- [License](#license)

---

## Features

### Dependency Injection ([docs](Documentation~/DI.md))
- **FastContainer**: Expression tree compiled factories (1.56x manual `new()` overhead)
- **Lifetimes**: Singleton, Transient, Scoped with thread-safe initialization
- **Circular Detection**: Build-time cycle detection prevents runtime errors
- **Zero-alloc Resolution**: No GC allocation for singleton/scoped paths

### Entity Component System ([docs](Documentation~/ECS.md))
- **SparseSet Storage**: Cache-friendly component iteration (6-28ns per entity)
- **Query System**: `ForEach<T1, T2, T3>()` with up to 3 component types
- **Parallel Jobs**: Burst-compiled jobs with 17x speedup over sequential
- **Entity Recycling**: Automatic index reuse with version tracking

### Messaging ([docs](Documentation~/Messaging.md))
- **StradaBus**: Unified command/query/event bus with array-indexed dispatch (4ns/dispatch)
- **Pooled Commands**: Execute ICommand objects with automatic pool return
- **Zero-alloc Publish**: Struct-based messages, no boxing

### MVCS-ECS Bridge ([docs](Documentation~/Bridge.md))
- **Event-Driven Integration**: ECS systems publish ComponentChanged events, MVCS controllers subscribe
- **ViewMediator**: Binds ECS entities to UI views with auto-sync and StradaBus integration
- **Bidirectional Flow**: Controllers send commands to ECS via StradaBus, receive events back

### Reactive Bindings ([docs](Documentation~/Bridge.md))
- **ReactiveProperty**: Observable values with change notification
- **ReactiveCollection**: Observable lists with add/remove/clear events
- **ComputedProperty**: Derived values with automatic dependency tracking

### Utilities
- **ObjectPool**: Generic pooling with lifecycle hooks (Spawn/Despawn)
- **StateMachine**: Type-safe FSM with conditional transitions
- **TimerService**: Managed timers with pause/resume support

---

## Installation

Add to your Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.strada.core": "file:../Packages/com.strada.core"
  }
}
```

Or copy the `Packages/com.strada.core` folder directly into your project.

**Requirements:**
- Unity 6000.0+ (Unity 6)
- .NET Standard 2.1

---

## Quick Start

### Dependency Injection

```csharp
using Strada.Core.DI;

// 1. Create container
var builder = new ContainerBuilder();

// 2. Register services
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
builder.Register<EnemyController>(Lifetime.Transient);

// 3. Build and resolve
using var container = builder.Build();
var player = container.Resolve<IPlayerService>();
```

### ECS System

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Systems;

// Define components (must be unmanaged structs)
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }

// Create system
public class MovementSystem : SystemBase<Position, Velocity>
{
    protected override void OnUpdateEntity(int entity, ref Position pos, ref Velocity vel, float dt)
    {
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
        pos.Z += vel.Z * dt;
    }
}
```

### Messaging

```csharp
using Strada.Core.Communication;

// Define messages as structs
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// Setup bus
var bus = new StradaBus();

// Subscribe to events
bus.Subscribe<PlayerDamaged>(e => Debug.Log($"Player took {e.Damage} damage"));

// Publish events (zero allocation)
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// Register command handlers
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnEnemyAt(cmd.X, cmd.Y));
bus.Send(new SpawnEnemy { X = 10, Y = 20 });
```

### Reactive Properties

```csharp
using Strada.Core.Bridge;

// Create reactive property
var health = new ReactiveProperty<int>(100);

// Subscribe to changes
health.Subscribe(value => healthBar.SetValue(value));

// Changes automatically notify subscribers
health.Value = 75; // healthBar updates automatically
```

---

## Performance

**Honest benchmarks** measured on Apple Silicon (Unity 6, Mono):

### DI Container

| Operation | Time | Notes |
|-----------|------|-------|
| Simple Transient | **0.11μs** | Single class, no dependencies |
| 4-Level Deep Chain | **0.27μs** | A→B→C→D dependency chain |
| Wide Service (5 deps) | **0.42μs** | Class with 5 injected dependencies |
| Singleton Lookup | **61ns** | Already-created singleton |
| Scoped Lookup | **21ns** | Within existing scope |
| Container Build (100 types) | **0.05ms** | ~0.5μs per registration |
| **vs Manual `new()`** | **1.56x** | Competitive with best Unity DI |

### ECS

| Operation | Time | Notes |
|-----------|------|-------|
| Entity Creation | **54ns** | Bare entity |
| Entity + 3 Components | **374ns** | Full entity setup |
| Single Component Query | **6.6ns/entity** | 100k entities |
| Two Component Query | **18ns/entity** | 100k entities |
| Three Component Query | **28ns/entity** | 100k entities |
| GetComponent | **67ns** | Random access |
| Simulation (100k, 10 frames) | **1.62ms/frame** | Position += Velocity |
| **Parallel Job Speedup** | **17x** | vs sequential ForEach |

### Memory

| Metric | Value |
|--------|-------|
| Memory per Entity (2 components) | 56 bytes |
| GC Allocation (Singleton resolve) | 0 bytes |
| GC Allocation (Scoped resolve) | 0 bytes |

### Comparison

| Framework | Resolution Speed | vs Manual |
|-----------|------------------|-----------|
| **Strada** | 0.11-0.27μs | **1.56x** |
| VContainer | ~0.2-0.3μs | ~2x |
| Reflex | ~0.5-1.0μs | ~3-5x |
| Zenject | ~2-5μs | ~20-50x |

---

## Documentation

| Document | Description |
|----------|-------------|
| [DI Container](Documentation~/DI.md) | Dependency injection, lifetimes, scopes |
| [ECS System](Documentation~/ECS.md) | Entities, components, queries, systems |
| [Messaging](Documentation~/Messaging.md) | StradaBus, commands, events, queries |
| [Bridge](Documentation~/Bridge.md) | Reactive properties, bindings |
| [Pooling](Documentation~/Pooling.md) | Object pools, lifecycle hooks |
| [StateMachine](Documentation~/StateMachine.md) | FSM with transitions |
| [Benchmarks](Documentation~/Benchmarks.md) | Full performance data |

---

## Architecture

```
Packages/com.strada.core/
├── Runtime/
│   ├── DI/                    # Dependency Injection
│   │   ├── ContainerBuilder.cs
│   │   ├── FastContainer.cs
│   │   ├── FastContainerScope.cs
│   │   └── Lifetime.cs
│   ├── ECS/                   # Entity Component System
│   │   ├── Core/EntityManager.cs
│   │   ├── Storage/SparseSet.cs
│   │   ├── Query/QueryBuilder.cs
│   │   ├── Systems/SystemBase.cs
│   │   └── Jobs/ParallelComponentJob.cs
│   ├── Communication/         # Unified Messaging
│   │   └── StradaBus.cs
│   ├── Commands/              # Command Pattern
│   │   ├── ICommand.cs
│   │   ├── CommandPool.cs
│   │   └── CommandSequencer.cs
│   ├── Bridge/                # MVCS-ECS Integration
│   │   ├── ReactiveProperty.cs
│   │   ├── ComputedProperty.cs
│   │   ├── ViewMediator.cs
│   │   └── BridgeEvents.cs
│   ├── Pooling/               # Object Pooling
│   │   └── ObjectPool.cs
│   └── StateMachine/          # FSM
│       └── StateMachine.cs
├── Editor/                    # Editor Tools
└── Tests/                     # Test Suite
    ├── Runtime/               # Functional Tests (269)
    └── Performance/           # Benchmarks (83)
```

---

## API Reference

### ContainerBuilder

```csharp
// Register interface → implementation
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// Register concrete type
builder.Register<MyService>(Lifetime.Transient);

// Register factory
builder.RegisterFactory<IService>(c => new ServiceImpl(c.Resolve<IDep>()));

// Register instance
builder.RegisterInstance<IConfig>(configInstance);

// Build container
IContainer container = builder.Build();
```

### IContainer

```csharp
T Resolve<T>() where T : class;
object Resolve(Type type);
bool TryResolve<T>(out T instance) where T : class;
bool IsRegistered<T>() where T : class;
IContainerScope CreateScope();
```

### EntityManager

```csharp
Entity CreateEntity();
void DestroyEntity(Entity entity);
bool Exists(Entity entity);

void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent;
void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent;
bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent;
T GetComponent<T>(Entity entity) where T : unmanaged, IComponent;
void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent;
```

### StradaBus

```csharp
// Events (pub/sub)
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;

// Struct Commands (request/response)
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;

// Object Commands (pooled, async)
void Execute(ICommand command);          // Auto-returns pooled commands
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);

// Queries (request/response with return)
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler);
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
```

### ReactiveProperty

```csharp
var prop = new ReactiveProperty<int>(initialValue);

prop.Value;                          // Get current value
prop.Value = newValue;               // Set and notify
prop.SetWithoutNotify(value);        // Set without notification
prop.Subscribe(handler);             // Subscribe to changes
prop.SubscribeAndInvoke(handler);    // Subscribe and call immediately
prop.Unsubscribe(handler);           // Remove subscription
```

### ObjectPool

```csharp
var pool = new ObjectPool<Enemy>(
    factory: () => new Enemy(),
    onSpawn: e => e.Reset(),
    onDespawn: e => e.Cleanup(),
    initialSize: 10,
    maxSize: 100
);

Enemy enemy = pool.Spawn();
pool.Despawn(enemy);
pool.Prewarm(20);
pool.Clear();
```

### StateMachine

```csharp
var fsm = new StateMachine<IState>();

fsm.AddState(new IdleState());
fsm.AddState(new WalkState());
fsm.AddState(new AttackState());

fsm.AddTransition<IdleState, WalkState>(() => input.IsMoving);
fsm.AddTransition<WalkState, IdleState>(() => !input.IsMoving);
fsm.AddAnyTransition<AttackState>(() => input.IsAttacking);

fsm.Start<IdleState>();
fsm.Update(deltaTime);
```

---

## Testing

```bash
# Run all tests (Unity must be closed)
./run_tests.sh

# Run functional tests only
UNITY_PATH="/path/to/Unity" PROJECT_PATH="/path/to/project"
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "!Performance"

# Run benchmarks only
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "Performance"
```

**Test Coverage:**
- 269 functional tests
- 83 performance benchmarks
- All tests passing

---

## License

Proprietary - All rights reserved

---

## Contributing

This is a private framework. For bug reports or feature requests, contact the maintainer.

---

*Built for Unity 6 with performance and clean architecture in mind.*
