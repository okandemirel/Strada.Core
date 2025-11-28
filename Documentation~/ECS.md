# Entity Component System

Strada's ECS provides high-performance entity management with SparseSet storage and cache-friendly iteration.

## Table of Contents

- [Quick Start](#quick-start)
- [Components](#components)
- [Entities](#entities)
- [Queries](#queries)
- [Systems](#systems)
- [Parallel Jobs](#parallel-jobs)
- [Performance](#performance)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

// 1. Create EntityManager
var entityManager = new EntityManager();

// 2. Define components (must be unmanaged structs)
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }

// 3. Create entities with components
var entity = entityManager.CreateEntity();
entityManager.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });
entityManager.AddComponent(entity, new Velocity { X = 1, Y = 0, Z = 0 });

// 4. Query and update
entityManager.ForEach<Position, Velocity>((int idx, ref Position pos, ref Velocity vel) =>
{
    pos.X += vel.X * deltaTime;
    pos.Y += vel.Y * deltaTime;
    pos.Z += vel.Z * deltaTime;
});
```

---

## Components

Components are data containers. They must be unmanaged structs implementing `IComponent`.

### Defining Components

```csharp
// Simple data component
public struct Position : IComponent
{
    public float X, Y, Z;
}

// Component with multiple fields
public struct Health : IComponent
{
    public int Current;
    public int Max;
    public float RegenRate;
}

// Tag component (no data, just marks entities)
public struct PlayerTag : IComponent { }

// Component with fixed arrays (must use unsafe)
public unsafe struct Inventory : IComponent
{
    public fixed int Items[10];
    public int Count;
}
```

### Component Constraints

- Must be `unmanaged` (no references, no managed types)
- Must implement `IComponent`
- Keep small for cache efficiency (< 64 bytes ideal)

```csharp
// INVALID - contains reference type
public struct Invalid : IComponent
{
    public string Name; // Error: string is managed
}

// VALID - use fixed char array instead
public unsafe struct Valid : IComponent
{
    public fixed char Name[32];
}
```

---

## Entities

Entities are lightweight identifiers with version tracking for safe references.

### Creating Entities

```csharp
// Create bare entity
Entity entity = entityManager.CreateEntity();

// Entity properties
int index = entity.Index;     // Unique index
int version = entity.Version; // Version for validity checking
```

### Entity Lifecycle

```csharp
// Create
var entity = entityManager.CreateEntity();

// Check existence
bool exists = entityManager.Exists(entity);

// Destroy
entityManager.DestroyEntity(entity);

// After destruction, Exists returns false
Assert.IsFalse(entityManager.Exists(entity));
```

### Entity Recycling

Destroyed entity indices are recycled with incremented versions:

```csharp
var entity1 = entityManager.CreateEntity(); // Index=1, Version=1
entityManager.DestroyEntity(entity1);

var entity2 = entityManager.CreateEntity(); // Index=1, Version=2

// Old reference is invalid
Assert.IsFalse(entityManager.Exists(entity1)); // Version mismatch
Assert.IsTrue(entityManager.Exists(entity2));
```

---

## Queries

Queries iterate over entities matching component requirements.

### ForEach (Extension Method)

```csharp
using Strada.Core.ECS.Query;

// Single component
entityManager.ForEach<Position>((int entity, ref Position pos) =>
{
    pos.Y -= 9.8f * deltaTime; // Gravity
});

// Two components
entityManager.ForEach<Position, Velocity>((int entity, ref Position pos, ref Velocity vel) =>
{
    pos.X += vel.X * deltaTime;
    pos.Y += vel.Y * deltaTime;
    pos.Z += vel.Z * deltaTime;
});

// Three components
entityManager.ForEach<Position, Velocity, Health>((int e, ref Position p, ref Velocity v, ref Health h) =>
{
    // Only entities with ALL three components
    p.X += v.X;
    h.Current -= 1;
});
```

### QueryBuilder

For advanced queries with filtering:

```csharp
// Build query
var query = entityManager.Query()
    .Select<Position, Velocity>();

// Execute
query.ForEach((int entity, ref Position pos, ref Velocity vel) =>
{
    pos.X += vel.X;
});
```

### Filtered Queries

```csharp
// Filter by component presence
var query = entityManager.Query()
    .Select<Position>()
    .Without<Dead>(); // Exclude entities with Dead component

query.ForEach((int e, ref Position p) =>
{
    // Only alive entities
});
```

---

## Systems

Systems encapsulate update logic with automatic query iteration.

### Basic System

```csharp
using Strada.Core.ECS.Systems;

public class MovementSystem : SystemBase
{
    protected override void OnUpdate(float deltaTime)
    {
        ForEach<Position, Velocity>((int e, ref Position p, ref Velocity v) =>
        {
            p.X += v.X * deltaTime;
            p.Y += v.Y * deltaTime;
            p.Z += v.Z * deltaTime;
        });
    }
}
```

### Generic System (Auto-Query)

```csharp
// Automatically iterates entities with Position and Velocity
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

### System Lifecycle

```csharp
public class GameSystem : SystemBase
{
    protected override void OnInitialize()
    {
        // Called once when system starts
        Debug.Log("System initialized");
    }

    protected override void OnUpdate(float deltaTime)
    {
        // Called every frame
    }

    protected override void OnDispose()
    {
        // Called when system is destroyed
        Debug.Log("System disposed");
    }
}
```

### System with Dependencies

Systems support dependency injection:

```csharp
public class DamageSystem : SystemBase
{
    [Inject]
    public void Inject(EntityManager em, MessageBus bus)
    {
        // EntityManager and MessageBus injected automatically
    }

    protected override void OnUpdate(float deltaTime)
    {
        ForEach<Health, Damage>((int e, ref Health h, ref Damage d) =>
        {
            h.Current -= d.Amount;
            if (h.Current <= 0)
            {
                Publish(new EntityDied { EntityId = e });
            }
        });
    }
}
```

---

## Parallel Jobs

For maximum performance, use Burst-compiled parallel jobs.

### Defining a Job

```csharp
using Unity.Burst;
using Strada.Core.ECS.Jobs;

[BurstCompile]
public struct MoveJob : IJobComponent<Position, Velocity>
{
    public float DeltaTime;

    [BurstCompile]
    public void Execute(int entity, ref Position pos, ref Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
        pos.Y += vel.Y * DeltaTime;
        pos.Z += vel.Z * DeltaTime;
    }
}
```

### Scheduling Jobs

```csharp
// Schedule parallel job
var job = new MoveJob { DeltaTime = Time.deltaTime };
var handle = entityManager.ScheduleParallel<MoveJob, Position, Velocity>(job);
handle.Complete();

// Or run immediately (blocking)
entityManager.RunParallel<MoveJob, Position, Velocity>(job);
```

### Performance Comparison

| Method | 100k Entities | Speedup |
|--------|--------------|---------|
| ForEach (sequential) | 17ms | 1x |
| Parallel Job (Burst) | 1ms | **17x** |

---

## Performance

### Benchmarks (Apple Silicon, Unity 6, Mono)

| Operation | Time | Notes |
|-----------|------|-------|
| Entity Creation | **54ns** | Bare entity |
| Entity + 1 Component | **149ns** | With Position |
| Entity + 3 Components | **374ns** | Full entity |
| Entity Destruction | **180ns** | With cleanup |
| Single Component Query | **6.6ns/entity** | 100k entities |
| Two Component Query | **18ns/entity** | 100k entities |
| Three Component Query | **28ns/entity** | 100k entities |
| GetComponent | **67ns** | Random access |
| SetComponent | **76ns** | Update value |
| HasComponent | **78ns** | Check existence |

### Memory Usage

| Metric | Value |
|--------|-------|
| Per Entity (2 components) | 56 bytes |
| Theoretical Minimum | 28 bytes |
| Overhead | ~100% |

### SparseSet Storage

Components are stored in SparseSet data structures for cache-friendly iteration:

```
Dense Array:  [comp0][comp1][comp2][comp3]...  <- Contiguous memory
Sparse Array: [?][3][0][?][1][2][?]...         <- Index mapping
```

Benefits:
- O(1) add/remove/has operations
- Cache-friendly iteration (dense array)
- No entity fragmentation

---

## Best Practices

### 1. Keep Components Small

```csharp
// Good - minimal data
public struct Position : IComponent { public float X, Y, Z; } // 12 bytes

// Avoid - too much data
public struct BadComponent : IComponent
{
    public float X, Y, Z;
    public float VelX, VelY, VelZ;
    public int Health, MaxHealth;
    public int Damage;
    // Split into multiple components instead
}
```

### 2. Use Tags for Filtering

```csharp
// Tag component - zero size
public struct Enemy : IComponent { }
public struct Player : IComponent { }

// Query only enemies
entityManager.ForEach<Position, Enemy>((int e, ref Position p, ref Enemy _) =>
{
    // Only enemy positions
});
```

### 3. Prefer Queries Over Direct Access

```csharp
// Good - batch processing
entityManager.ForEach<Health>((int e, ref Health h) =>
{
    h.Current += h.RegenRate * deltaTime;
});

// Avoid - individual access in loops
foreach (var entity in entities)
{
    var health = entityManager.GetComponent<Health>(entity);
    // Less efficient
}
```

### 4. Use Parallel Jobs for Heavy Work

```csharp
// For 1000+ entities with compute-heavy work
[BurstCompile]
public struct AIJob : IJobComponent<AIState, Position>
{
    [BurstCompile]
    public void Execute(int e, ref AIState ai, ref Position pos)
    {
        // Complex pathfinding calculations
        // Burst-compiled for maximum speed
    }
}
```

### 5. Dispose EntityManager

```csharp
void OnDestroy()
{
    entityManager?.Dispose();
}
```

---

## API Reference

### EntityManager

```csharp
// Entity operations
Entity CreateEntity()
void DestroyEntity(Entity entity)
bool Exists(Entity entity)
int EntityCount { get; }

// Component operations
void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
void AddComponent<T>(Entity entity) where T : unmanaged, IComponent
void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent
bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent

// Queries
QueryBuilder Query()
void Clear()
void Dispose()
```

### Entity

```csharp
public readonly struct Entity
{
    public int Index { get; }
    public int Version { get; }
}
```

### SystemBase

```csharp
protected EntityManager EntityManager { get; }
protected MessageBus Bus { get; }

protected virtual void OnInitialize() { }
protected abstract void OnUpdate(float deltaTime);
protected virtual void OnDispose() { }

protected void ForEach<T1>(QueryDelegate<T1> action);
protected void ForEach<T1, T2>(QueryDelegate<T1, T2> action);
protected void ForEach<T1, T2, T3>(QueryDelegate<T1, T2, T3> action);

protected Entity CreateEntity();
protected void DestroyEntity(Entity entity);
protected void Publish<T>(T evt) where T : struct;
protected void Send<T>(T cmd) where T : struct;
```

---

## Related Documentation

- [DI Container](DI.md) - Dependency injection for systems
- [Messaging](Messaging.md) - MessageBus communication
- [Benchmarks](Benchmarks.md) - Full performance data
