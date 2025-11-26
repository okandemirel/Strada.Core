# Messaging System

Strada provides zero-allocation messaging through `StradaBus` and `CommandBus` for decoupled communication.

## Table of Contents

- [Quick Start](#quick-start)
- [StradaBus](#stradabus)
  - [Events (Pub/Sub)](#events-pubsub)
  - [Commands](#commands)
  - [Queries](#queries)
- [CommandBus](#commandbus)
- [Performance](#performance)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.Communication;

// Create bus
var bus = new StradaBus();

// Define messages as structs (zero allocation)
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// Subscribe to events
bus.Subscribe<PlayerDamaged>(e => HandleDamage(e));

// Publish events
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// Register command handlers
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnAt(cmd.X, cmd.Y));

// Send commands
bus.Send(new SpawnEnemy { X = 100, Y = 50 });
```

---

## StradaBus

The unified messaging bus supporting events, commands, and queries.

### Events (Pub/Sub)

Events are fire-and-forget messages with multiple subscribers.

```csharp
// Define event
public struct EnemyKilled
{
    public int EntityId;
    public int ScoreValue;
    public float X, Y;
}

// Subscribe
bus.Subscribe<EnemyKilled>(OnEnemyKilled);

void OnEnemyKilled(EnemyKilled e)
{
    score += e.ScoreValue;
    SpawnParticles(e.X, e.Y);
}

// Multiple subscribers allowed
bus.Subscribe<EnemyKilled>(e => PlaySound("kill"));
bus.Subscribe<EnemyKilled>(e => UpdateKillCount());

// Publish to all subscribers
bus.Publish(new EnemyKilled
{
    EntityId = 42,
    ScoreValue = 100,
    X = 15.5f,
    Y = 20.0f
});

// Unsubscribe when done
bus.Unsubscribe<EnemyKilled>(OnEnemyKilled);
```

### Commands

Commands are single-handler messages for direct actions.

```csharp
// Define command
public struct DealDamage
{
    public int TargetId;
    public int Amount;
    public bool IsCritical;
}

// Register single handler
bus.RegisterCommandHandler<DealDamage>(HandleDamage);

void HandleDamage(DealDamage cmd)
{
    var health = GetHealth(cmd.TargetId);
    health.Current -= cmd.IsCritical ? cmd.Amount * 2 : cmd.Amount;
}

// Send command
bus.Send(new DealDamage
{
    TargetId = 1,
    Amount = 25,
    IsCritical = true
});
```

### Queries

Queries return values from handlers.

```csharp
// Define query with result type
public struct GetPlayerHealth : IQuery<int>
{
    public int PlayerId;
}

// Register query handler (interface style)
public class HealthQueryHandler : IQueryHandler<GetPlayerHealth, int>
{
    public int Handle(ref GetPlayerHealth query)
    {
        return _healthSystem.GetHealth(query.PlayerId);
    }
}

bus.RegisterQueryHandler<GetPlayerHealth, int>(new HealthQueryHandler());

// Or use delegate style
bus.RegisterQueryHandler<GetPlayerHealth, int>(q => _health[q.PlayerId]);

// Execute query
int health = bus.Query<GetPlayerHealth, int>(new GetPlayerHealth { PlayerId = 1 });
```

### Ref Parameters

For maximum performance, use ref parameters to avoid struct copies:

```csharp
// Publish by ref (zero copy)
var evt = new LargeEvent { /* lots of data */ };
bus.Publish(ref evt);

// Send by ref
var cmd = new LargeCommand { /* lots of data */ };
bus.Send(ref cmd);

// Query by ref
var query = new ComplexQuery { /* parameters */ };
var result = bus.Query<ComplexQuery, Result>(ref query);
```

---

## CommandBus

Specialized command bus with DI integration.

### Basic Usage

```csharp
using Strada.Core.Commands;

var commandBus = new CommandBus();

// Register handler
commandBus.RegisterHandler<MoveCommand>(HandleMove);

// Or use interface
public class MoveHandler : ICommandHandler<MoveCommand>
{
    public void Handle(MoveCommand cmd) { /* ... */ }
}
commandBus.RegisterHandler(new MoveHandler());

// Send command
commandBus.Send(new MoveCommand { Direction = Vector3.forward });
```

### DI Integration

CommandBus can auto-resolve handlers from container:

```csharp
// Setup with container
var commandBus = new CommandBus(container);

// Register handler type in DI
builder.Register<ICommandHandler<MoveCommand>, MoveHandler>();

// CommandBus resolves from container if no explicit handler
commandBus.Send(new MoveCommand()); // Auto-resolves MoveHandler
```

### Command Objects (OOP Style)

For complex commands with async support:

```csharp
// Define command object
public class LoadLevelCommand : ICommand
{
    public string LevelName { get; set; }

    public void Execute()
    {
        SceneManager.LoadScene(LevelName);
    }
}

// Execute
commandBus.Execute(new LoadLevelCommand { LevelName = "Level1" });

// Async command
public class LoadAssetsCommand : IAsyncCommand
{
    public string[] AssetPaths { get; set; }

    public void Execute(Action onComplete)
    {
        LoadAssetsAsync(AssetPaths, onComplete);
    }
}

commandBus.ExecuteAsync(new LoadAssetsCommand { AssetPaths = paths }, () =>
{
    Debug.Log("Assets loaded!");
});
```

### Pooled Commands

Reuse command objects to avoid allocation:

```csharp
public class SpawnCommand : ICommand, IPooledCommand
{
    public float X, Y;
    private ObjectPool<SpawnCommand> _pool;

    public void SetPool(ObjectPool<SpawnCommand> pool) => _pool = pool;
    public void ReturnToPool() => _pool.Despawn(this);

    public void Execute()
    {
        SpawnAt(X, Y);
    }
}

// Commands auto-return to pool after execution
commandBus.Execute(spawnPool.Spawn());
```

---

## Performance

### Benchmarks

StradaBus uses array-indexed dispatch for O(1) message routing:

| Operation | Time |
|-----------|------|
| Publish (1 subscriber) | ~20ns |
| Publish (10 subscribers) | ~100ns |
| Send Command | ~15ns |
| Query | ~20ns |

### Zero Allocation

All message types must be structs:

```csharp
// GOOD - struct, no allocation
public struct DamageEvent { public int Amount; }

// BAD - class, allocates
public class DamageEvent { public int Amount; } // Don't do this
```

### Internal Architecture

```csharp
// Type ID system for O(1) lookup
private static class EventTypeId<T>
{
    public static readonly int Id = Interlocked.Increment(ref _nextTypeId);
}

// Array-indexed dispatch
public void Publish<T>(ref T evt)
{
    var id = EventTypeId<T>.Id;
    var channel = _eventChannels[id];
    channel?.Publish(ref evt);
}
```

---

## Best Practices

### 1. Use Structs for Messages

```csharp
// Good - struct
public struct PlayerJumped { public int PlayerId; }

// Avoid - class (allocates)
public class PlayerJumped { public int PlayerId; }
```

### 2. Keep Messages Small

```csharp
// Good - minimal data
public struct ItemCollected
{
    public int ItemId;
    public int PlayerId;
}

// Avoid - too much data
public struct ItemCollected
{
    public int ItemId;
    public int PlayerId;
    public string ItemName;    // Don't include strings
    public ItemData FullData;  // Don't include large objects
}
```

### 3. Unsubscribe to Prevent Leaks

```csharp
public class EnemyController : MonoBehaviour
{
    private StradaBus _bus;
    private Action<DamageEvent> _handler;

    void OnEnable()
    {
        _handler = OnDamage;
        _bus.Subscribe(_handler);
    }

    void OnDisable()
    {
        _bus.Unsubscribe(_handler);
    }
}
```

### 4. Use Commands for Side Effects

```csharp
// Events - notify about something that happened
bus.Publish(new EnemyDied { EntityId = 42 });

// Commands - request an action
bus.Send(new SpawnReward { Position = pos });
```

### 5. Use Queries for Data Retrieval

```csharp
// Query - get data without side effects
var health = bus.Query<GetHealth, int>(new GetHealth { EntityId = id });

// Don't use events/commands for getting data
```

---

## API Reference

### IStradaBus

```csharp
// Events
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;
void Publish<TEvent>(ref TEvent evt) where TEvent : struct;
int GetSubscriberCount<TEvent>() where TEvent : struct;

// Commands
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;
void Send<TCommand>(ref TCommand command) where TCommand : struct;

// Queries
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler)
    where TQuery : struct, IQuery<TResult>;
void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
    where TQuery : struct, IQuery<TResult>;
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
TResult Query<TQuery, TResult>(ref TQuery query) where TQuery : struct, IQuery<TResult>;

// Lifecycle
void Clear();
void Dispose();
```

### ICommandBus

```csharp
void Send<TCommand>(TCommand command) where TCommand : struct;
void Send<TCommand>(ref TCommand command) where TCommand : struct;
void Execute(ICommand command);
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);
void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct;
void RegisterHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
```

### Interfaces

```csharp
public interface IQuery<TResult> { }

public interface IQueryHandler<TQuery, TResult> where TQuery : struct, IQuery<TResult>
{
    TResult Handle(ref TQuery query);
}

public interface ICommand
{
    void Execute();
}

public interface IAsyncCommand
{
    void Execute(Action onComplete);
}

public interface ICommandHandler<TCommand> where TCommand : struct
{
    void Handle(TCommand command);
}
```

---

## Related Documentation

- [DI Container](DI.md) - Dependency injection
- [ECS System](ECS.md) - Entity Component System
- [Bridge](Bridge.md) - Reactive properties
