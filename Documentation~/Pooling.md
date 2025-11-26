# Object Pooling

Strada provides generic object pooling with lifecycle hooks for efficient object reuse.

## Table of Contents

- [Quick Start](#quick-start)
- [ObjectPool](#objectpool)
- [IPoolable Interface](#ipoolable-interface)
- [Pool Configuration](#pool-configuration)
- [PoolRegistry](#poolregistry)
- [Performance](#performance)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.Pooling;

// Create pool with factory
var bulletPool = new ObjectPool<Bullet>(
    factory: () => new Bullet(),
    initialSize: 20
);

// Spawn from pool
Bullet bullet = bulletPool.Spawn();
bullet.Fire(direction);

// Return to pool when done
bulletPool.Despawn(bullet);
```

---

## ObjectPool

Generic pool for any reference type.

### Basic Construction

```csharp
// Minimal - just factory
var pool = new ObjectPool<Enemy>(() => new Enemy());

// With prewarm
var pool = new ObjectPool<Enemy>(() => new Enemy(), initialSize: 50);

// With max size limit
var pool = new ObjectPool<Enemy>(() => new Enemy(), initialSize: 10, maxSize: 100);
```

### Lifecycle Callbacks

```csharp
var pool = new ObjectPool<Projectile>(
    factory: () => new Projectile(),
    onSpawn: p =>
    {
        p.gameObject.SetActive(true);
        p.ResetTrail();
    },
    onDespawn: p =>
    {
        p.gameObject.SetActive(false);
        p.ClearTarget();
    },
    initialSize: 30
);
```

### Spawn and Despawn

```csharp
// Get object from pool (creates new if empty)
var obj = pool.Spawn();

// Return object to pool
pool.Despawn(obj);

// Spawn is always safe (returns new if pool empty)
for (int i = 0; i < 1000; i++)
{
    var bullet = pool.Spawn(); // Creates more if needed
}
```

### Pool Statistics

```csharp
int available = pool.AvailableCount; // Objects ready to spawn
int total = pool.TotalCreated;       // Total objects ever created
int active = pool.ActiveCount;       // Currently spawned objects
```

### Prewarming

```csharp
// Create objects ahead of time
pool.Prewarm(100);

// Good for loading screens
IEnumerator PrewarmPools()
{
    enemyPool.Prewarm(50);
    yield return null;
    bulletPool.Prewarm(200);
    yield return null;
    effectPool.Prewarm(100);
}
```

### Clearing

```csharp
// Remove all pooled objects (calls Dispose if implemented)
pool.Clear();

// Dispose pool entirely
pool.Dispose();
```

---

## IPoolable Interface

Implement for automatic lifecycle callbacks.

### Basic IPoolable

```csharp
public class Bullet : IPoolable
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Lifetime;

    public void OnSpawn()
    {
        // Called when spawned from pool
        Lifetime = 5f;
    }

    public void OnDespawn()
    {
        // Called when returned to pool
        Position = Vector3.zero;
        Velocity = Vector3.zero;
    }
}
```

### IPoolable with Pool Reference

```csharp
public class AutoReturnBullet : IPoolable<AutoReturnBullet>
{
    private ObjectPool<AutoReturnBullet> _pool;

    public void SetPool(ObjectPool<AutoReturnBullet> pool)
    {
        _pool = pool;
    }

    public void OnSpawn() { }
    public void OnDespawn() { }

    public void ReturnToPool()
    {
        _pool.Despawn(this);
    }
}

// Usage
var bullet = bulletPool.Spawn();
// ... later
bullet.ReturnToPool(); // Returns itself to pool
```

### MonoBehaviour Pooling

```csharp
public class PooledEnemy : MonoBehaviour, IPoolable
{
    public float health;
    public Rigidbody rb;

    public void OnSpawn()
    {
        health = 100f;
        rb.velocity = Vector3.zero;
        gameObject.SetActive(true);
    }

    public void OnDespawn()
    {
        gameObject.SetActive(false);
        transform.position = Vector3.zero;
    }
}

// Pool setup
var enemyPool = new ObjectPool<PooledEnemy>(
    factory: () =>
    {
        var go = Instantiate(enemyPrefab);
        return go.GetComponent<PooledEnemy>();
    }
);
```

---

## Pool Configuration

### Initial Size

Pre-create objects to avoid runtime allocation:

```csharp
// For objects needed immediately
var pool = new ObjectPool<Bullet>(() => new Bullet(), initialSize: 100);
```

### Max Size

Limit memory usage:

```csharp
// Pool will discard objects beyond max when despawning
var pool = new ObjectPool<Particle>(
    () => new Particle(),
    initialSize: 50,
    maxSize: 200
);

// When despawning beyond max, object is not pooled
pool.Despawn(particle); // Discarded if pool has 200+ items
```

### Custom Callbacks

For objects that don't implement IPoolable:

```csharp
var pool = new ObjectPool<ThirdPartyObject>(
    factory: () => new ThirdPartyObject(),
    onSpawn: obj => obj.Initialize(),
    onDespawn: obj => obj.Reset()
);
```

---

## PoolRegistry

Centralized pool management (optional pattern).

### Setup

```csharp
public static class Pools
{
    public static ObjectPool<Bullet> Bullets { get; private set; }
    public static ObjectPool<Enemy> Enemies { get; private set; }
    public static ObjectPool<VFX> Effects { get; private set; }

    public static void Initialize()
    {
        Bullets = new ObjectPool<Bullet>(() => new Bullet(), initialSize: 200);
        Enemies = new ObjectPool<Enemy>(() => new Enemy(), initialSize: 50);
        Effects = new ObjectPool<VFX>(() => new VFX(), initialSize: 100);
    }

    public static void Shutdown()
    {
        Bullets?.Dispose();
        Enemies?.Dispose();
        Effects?.Dispose();
    }
}

// Usage
Pools.Initialize();
var bullet = Pools.Bullets.Spawn();
```

### Type-Based Registry

```csharp
public class PoolRegistry
{
    private readonly Dictionary<Type, object> _pools = new();

    public void Register<T>(ObjectPool<T> pool) where T : class
    {
        _pools[typeof(T)] = pool;
    }

    public ObjectPool<T> Get<T>() where T : class
    {
        return (ObjectPool<T>)_pools[typeof(T)];
    }

    public T Spawn<T>() where T : class
    {
        return Get<T>().Spawn();
    }

    public void Despawn<T>(T obj) where T : class
    {
        Get<T>().Despawn(obj);
    }
}
```

---

## Performance

### Benchmarks

| Operation | Without Pool | With Pool | Speedup |
|-----------|--------------|-----------|---------|
| Create 10k objects | 45ms | 3ms | **15x** |
| GC Allocation | 400KB | 0 | **∞** |

### Memory Efficiency

```csharp
// Pool reuses objects - minimal GC pressure
for (int i = 0; i < 10000; i++)
{
    var bullet = pool.Spawn();
    // Use bullet...
    pool.Despawn(bullet);
}
// Only pool.TotalCreated objects ever allocated
```

### Spawn/Despawn Speed

| Operation | Time |
|-----------|------|
| Spawn (from pool) | ~10ns |
| Spawn (create new) | ~500ns |
| Despawn | ~8ns |

---

## Best Practices

### 1. Prewarm During Loading

```csharp
IEnumerator LoadGame()
{
    yield return ShowLoadingScreen();

    // Prewarm pools while loading
    bulletPool.Prewarm(200);
    enemyPool.Prewarm(50);
    effectPool.Prewarm(100);

    yield return LoadAssets();
    yield return HideLoadingScreen();
}
```

### 2. Reset State in OnDespawn

```csharp
public void OnDespawn()
{
    // Reset ALL mutable state
    health = maxHealth;
    position = Vector3.zero;
    velocity = Vector3.zero;
    target = null;
    isAlive = true;
}
```

### 3. Use Max Size for Memory Control

```csharp
// Limit pool growth for memory-sensitive platforms
var pool = new ObjectPool<Particle>(
    () => new Particle(),
    initialSize: 100,
    maxSize: 500 // Prevents unbounded growth
);
```

### 4. Dispose Pools on Shutdown

```csharp
void OnApplicationQuit()
{
    bulletPool?.Dispose();
    enemyPool?.Dispose();
    effectPool?.Dispose();
}
```

### 5. Don't Pool Small Structs

```csharp
// Pooling is for reference types
// Structs are already stack-allocated

// DON'T pool structs
var badPool = new ObjectPool<Vector3>(...); // Unnecessary

// DO pool reference types
var goodPool = new ObjectPool<Enemy>(...);
```

### 6. Handle Disposal Properly

```csharp
public class DisposableResource : IPoolable, IDisposable
{
    private bool _disposed;

    public void OnSpawn()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DisposableResource));
    }

    public void OnDespawn()
    {
        // Don't dispose here - just reset
    }

    public void Dispose()
    {
        _disposed = true;
        // Release unmanaged resources
    }
}
```

---

## API Reference

### ObjectPool<T>

```csharp
ObjectPool(Func<T> factory, int initialSize = 0, int maxSize = int.MaxValue)
ObjectPool(Func<T> factory, Action<T> onSpawn, Action<T> onDespawn, int initialSize = 0, int maxSize = int.MaxValue)

int AvailableCount { get; }
int TotalCreated { get; }
int ActiveCount { get; }

T Spawn()
void Despawn(T instance)
void Prewarm(int count)
void Clear()
void Dispose()
```

### IPoolable

```csharp
public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}
```

### IPoolable<T>

```csharp
public interface IPoolable<T> : IPoolable where T : class
{
    void SetPool(ObjectPool<T> pool);
}
```

---

## Related Documentation

- [ECS System](ECS.md) - Entity Component System
- [Benchmarks](Benchmarks.md) - Performance data
