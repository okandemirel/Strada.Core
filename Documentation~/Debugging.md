# Debugging & Troubleshooting

This guide covers common issues, debugging strategies, and troubleshooting tips for Strada Framework.

## Table of Contents

- [DI Container Issues](#di-container-issues)
- [ECS Debugging](#ecs-debugging)
- [Messaging Issues](#messaging-issues)
- [Module System Issues](#module-system-issues)
- [Reactive Binding Issues](#reactive-binding-issues)
- [Performance Debugging](#performance-debugging)
- [Hot Reload Issues](#hot-reload-issues)
- [Editor Tools](#editor-tools)

---

## DI Container Issues

### "Type not registered" Exception

**Symptom:**
```
InvalidOperationException: Type 'IPlayerService' is not registered in the container
```

**Causes & Solutions:**

1. **Service not registered**
   ```csharp
   // Ensure service is registered in ModuleConfig
   protected override void Configure(IModuleBuilder builder)
   {
       builder.RegisterService<IPlayerService, PlayerService>();
   }
   ```

2. **Module not added to GameBootstrapperConfig**
   - Open GameBootstrapperConfig asset
   - Verify your ModuleConfig is in the Modules list
   - Check module is enabled

3. **Registration order issue**
   ```csharp
   // If ServiceA depends on ServiceB, register ServiceB first
   // Or let the container handle it - it resolves dependencies automatically
   builder.RegisterService<IServiceB, ServiceB>();
   builder.RegisterService<IServiceA, ServiceA>(); // Depends on IServiceB
   ```

4. **Wrong lifetime for scoped service**
   ```csharp
   // Scoped services can't be resolved from root container
   // Wrong:
   var service = container.Resolve<IScopedService>();

   // Correct:
   using var scope = container.CreateScope();
   var service = scope.Resolve<IScopedService>();
   ```

### Circular Dependency Detected

**Symptom:**
```
InvalidOperationException: Circular dependency detected involving type 'ServiceA'
```

**Solution:**
Break the cycle using one of these patterns:

```csharp
// Option 1: Lazy initialization
public class ServiceA
{
    private readonly Lazy<IServiceB> _serviceB;

    public ServiceA(Lazy<IServiceB> serviceB)
    {
        _serviceB = serviceB;
    }
}

// Option 2: Method injection
public class ServiceA
{
    private IServiceB _serviceB;

    public void SetServiceB(IServiceB serviceB)
    {
        _serviceB = serviceB;
    }
}

// Option 3: Restructure dependencies
// Often circular deps indicate design issues - consider a mediator
```

### Container Already Built

**Symptom:**
```
InvalidOperationException: Cannot register after container is built
```

**Solution:**
All registrations must happen before `Build()`:

```csharp
// Wrong:
var container = builder.Build();
builder.Register<IService, Service>(); // Too late!

// Correct:
builder.Register<IService, Service>();
var container = builder.Build();
```

### Debugging Resolution

Enable verbose logging:

```csharp
// In GameBootstrapperConfig inspector
// Enable "Verbose Logging" checkbox

// Or manually:
Debug.Log($"Registered types: {container.GetRegisteredTypes().Count}");
Debug.Log($"Is registered: {container.IsRegistered<IMyService>()}");
```

---

## ECS Debugging

### Entity Not Found

**Symptom:**
Component operations fail or return default values.

**Diagnosis:**

```csharp
// Check if entity exists
if (!entityManager.Exists(entity))
{
    Debug.LogError($"Entity {entity.Index} v{entity.Version} no longer exists");
    return;
}

// Check entity version (stale reference)
var current = entityManager.GetEntity(entity.Index);
if (current.Version != entity.Version)
{
    Debug.LogError($"Stale entity reference. Expected v{entity.Version}, got v{current.Version}");
}
```

**Common Causes:**
- Entity was destroyed but reference kept
- Entity index was recycled with new version
- Accessing entity from wrong EntityManager

### Component Not Found

**Symptom:**
```
KeyNotFoundException: Entity does not have component of type 'Health'
```

**Diagnosis:**

```csharp
// Always check before accessing
if (entityManager.HasComponent<Health>(entity))
{
    var health = entityManager.GetComponent<Health>(entity);
}

// Or use TryGet pattern
if (entityManager.TryGetComponent<Health>(entity, out var health))
{
    // Use health
}
```

### Query Returns No Entities

**Diagnosis:**

```csharp
// Check entity count
Debug.Log($"Total entities: {entityManager.EntityCount}");

// Check component existence
int withPosition = 0;
int withVelocity = 0;

entityManager.ForEach<Position>((e, ref Position p) => withPosition++);
entityManager.ForEach<Velocity>((e, ref Velocity v) => withVelocity++);

Debug.Log($"Entities with Position: {withPosition}");
Debug.Log($"Entities with Velocity: {withVelocity}");

// Combined query
int both = 0;
entityManager.ForEach<Position, Velocity>((e, ref Position p, ref Velocity v) => both++);
Debug.Log($"Entities with both: {both}");
```

### Using Entity Inspector

1. Open: **Strada → Debugger → Entity Inspector**
2. Select entity from list
3. View all components and their values
4. Modify values in real-time (Play mode)

---

## Messaging Issues

### Event Not Received

**Symptom:**
Subscribers don't receive published events.

**Diagnosis:**

```csharp
// 1. Check subscriber count
int count = bus.GetSubscriberCount<MyEvent>();
Debug.Log($"Subscribers for MyEvent: {count}");

// 2. Verify subscription happened before publish
// Wrong order:
bus.Publish(new MyEvent());  // Published too early!
bus.Subscribe<MyEvent>(OnEvent);

// Correct order:
bus.Subscribe<MyEvent>(OnEvent);
bus.Publish(new MyEvent());

// 3. Check you're using the same bus instance
Debug.Log($"Publisher bus: {publisherBus.GetHashCode()}");
Debug.Log($"Subscriber bus: {subscriberBus.GetHashCode()}");
```

### Memory Leak from Subscriptions

**Symptom:**
Subscriber callbacks called on destroyed objects.

**Solution:**

```csharp
public class EnemyController : MonoBehaviour
{
    private Action<DamageEvent> _handler;

    void OnEnable()
    {
        _handler = OnDamage;
        bus.Subscribe(_handler);
    }

    void OnDisable()
    {
        bus.Unsubscribe(_handler); // Always unsubscribe!
    }
}

// Or use BindingScope
private BindingScope _scope;

void OnEnable()
{
    _scope = new BindingScope();
    _scope.Subscribe(bus, OnDamage);
}

void OnDisable()
{
    _scope.Dispose(); // Cleans up all subscriptions
}
```

### Using Bus Debugger

1. Open: **Strada → Debugger → Bus Debugger**
2. See all registered event types
3. View subscriber counts
4. Monitor messages in real-time (Play mode)

---

## Module System Issues

### Module Not Loading

**Symptom:**
Module's `Initialize()` not called.

**Checklist:**

1. ✅ Module asset exists as ScriptableObject
2. ✅ Module added to GameBootstrapperConfig.Modules list
3. ✅ Module.Enabled is true
4. ✅ GameBootstrapper component is in scene
5. ✅ GameBootstrapper has correct config assigned

### System Not Running

**Symptom:**
ECS System's `OnUpdate` not called.

**Checklist:**

1. ✅ System added to ModuleConfig.Systems list
2. ✅ System entry is enabled
3. ✅ Correct UpdatePhase selected
4. ✅ System has `[StradaSystem]` attribute
5. ✅ Clicked "Discover" in ModuleConfig inspector

**Debug system registration:**

```csharp
protected override void OnInitialize()
{
    Debug.Log($"[{GetType().Name}] System initialized");
}

protected override void OnUpdate(float deltaTime)
{
    Debug.Log($"[{GetType().Name}] Update called, dt={deltaTime}");
}
```

### Module Priority Issues

**Symptom:**
Dependencies not available during Initialize.

**Solution:**
Set proper priorities (lower = initializes first):

```csharp
// CoreModuleConfig
public override int Priority => 0; // Loads first

// GameModuleConfig
public override int Priority => 100; // Loads after core

// UIModuleConfig
public override int Priority => 200; // Loads last
```

---

## Reactive Binding Issues

### Property Not Updating UI

**Symptom:**
ReactiveProperty value changes but UI doesn't update.

**Diagnosis:**

```csharp
// 1. Verify subscription exists
var health = new ReactiveProperty<int>(100);
health.Subscribe(v => Debug.Log($"Health changed to: {v}"));
health.Value = 50; // Should log

// 2. Check if using SetWithoutNotify accidentally
health.SetWithoutNotify(25); // Won't trigger subscribers!
health.Value = 25; // Will trigger subscribers

// 3. Verify same value check
health.Value = 50;
health.Value = 50; // Won't trigger (same value)
```

### ComputedProperty Not Recalculating

**Symptom:**
Computed value is stale after dependency change.

**Diagnosis:**

```csharp
// Ensure all dependencies are passed to constructor
var health = new ReactiveProperty<int>(100);
var maxHealth = new ReactiveProperty<int>(100);

// Wrong - missing dependency
var percent = new ComputedProperty<float>(
    () => (float)health.Value / maxHealth.Value,
    health  // Missing maxHealth!
);

// Correct
var percent = new ComputedProperty<float>(
    () => (float)health.Value / maxHealth.Value,
    health, maxHealth  // All dependencies listed
);
```

### Memory Leak from Bindings

**Symptom:**
Disposed objects still receiving updates.

**Solution:**

```csharp
// Always use BindingScope
private BindingScope _scope;

void OnEnable()
{
    _scope = new BindingScope();
    _scope.Bind(model.Health, UpdateHealthBar);
    _scope.Bind(model.Score, UpdateScoreText);
}

void OnDisable()
{
    _scope?.Dispose(); // Cleans all bindings
}
```

---

## Performance Debugging

### Identifying Slow Systems

1. Open: **Strada → Debugger → System Profiler**
2. View per-system execution times
3. Identify systems taking > 1ms
4. Sort by time to find bottlenecks

### ECS Query Optimization

```csharp
// Profile query performance
var sw = System.Diagnostics.Stopwatch.StartNew();

entityManager.ForEach<Position, Velocity, Health>(
    (int e, ref Position p, ref Velocity v, ref Health h) =>
    {
        // Work
    });

sw.Stop();
Debug.Log($"Query took {sw.Elapsed.TotalMilliseconds:F2}ms");
```

**Optimization Tips:**

```csharp
// 1. Use parallel jobs for 1000+ entities
var job = new MoveJob { DeltaTime = dt };
entityManager.RunParallel<MoveJob, Position, Velocity>(job);

// 2. Keep components small (< 64 bytes)
// 3. Use tag components for filtering
// 4. Batch operations instead of individual calls
```

### DI Resolution Performance

```csharp
// Profile resolution
var sw = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < 10000; i++)
{
    var service = container.Resolve<IMyService>();
}

sw.Stop();
var nsPerResolve = (sw.Elapsed.TotalMilliseconds * 1_000_000) / 10000;
Debug.Log($"Resolution: {nsPerResolve:F1}ns per call");
```

### Memory Profiling

```csharp
// Check GC allocations
long before = GC.GetTotalMemory(false);

// Do operations
for (int i = 0; i < 1000; i++)
{
    bus.Publish(new MyEvent { Value = i });
}

long after = GC.GetTotalMemory(false);
Debug.Log($"Allocated: {after - before} bytes");
// Should be 0 for struct events
```

---

## Hot Reload Issues

### Config Changes Not Applying

**Symptom:**
ModuleConfig changes not reflected in Play mode.

**Checklist:**

1. ✅ Hot Reload enabled: **Strada → Settings → Hot Reload → Enable Hot Reload**
2. ✅ Notifications enabled (helps debugging)
3. ✅ Config is saved (Ctrl+S)
4. ✅ Playing in Editor (not standalone build)

### Entity State Lost on Reload

**Symptom:**
Entities reset after domain reload.

**Note:** Hot reload preserves config, not entity state. Entity state is runtime-only.

**Workaround for testing:**

```csharp
// Save state before reload
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void SaveState()
{
    // Serialize entity state if needed
}
```

---

## Editor Tools

### Strada Dashboard

**Location:** Strada → Dashboard (Ctrl+Shift+Alt+D)

**Features:**
- Module status overview
- Quick access to all tools
- Framework health check

### Entity Inspector

**Location:** Strada → Debugger → Entity Inspector (Ctrl+Shift+E)

**Features:**
- Browse all entities
- View/edit components
- Track entity lifecycle

### System Profiler

**Location:** Strada → Debugger → System Profiler (Ctrl+Shift+P)

**Features:**
- Per-system timing
- Frame time breakdown
- Performance history

### Bus Debugger

**Location:** Strada → Debugger → Bus Debugger (Ctrl+Shift+B)

**Features:**
- Event type list
- Subscriber counts
- Message flow visualization

### Dependency Graph

**Location:** Strada → Debugger → Dependency Graph (Ctrl+Shift+D)

**Features:**
- Visualize service dependencies
- Detect circular references
- Module relationships

### Time Machine

**Location:** Strada → Debugger → Time Machine (Ctrl+Shift+T)

**Features:**
- Record entity states
- Playback timeline
- Debug state transitions

---

## Common Error Messages

| Error | Meaning | Solution |
|-------|---------|----------|
| `Type not registered` | Service missing from DI | Register in ModuleConfig |
| `Circular dependency` | A→B→A dependency chain | Break cycle with Lazy<T> |
| `Entity does not exist` | Stale entity reference | Check Exists() before use |
| `Component not found` | Entity lacks component | Check HasComponent() first |
| `Already disposed` | Using disposed container | Check disposal order |
| `Cannot register after build` | Late registration attempt | Register before Build() |
| `Scoped from root` | Scoped service at root | Use CreateScope() first |

---

## Debugging Checklist

When something doesn't work:

1. **Check Console** - Look for exceptions/warnings
2. **Enable Verbose Logging** - In GameBootstrapperConfig
3. **Use Editor Tools** - Dashboard, Inspectors, Debuggers
4. **Verify Registration** - Is service/system registered?
5. **Check Lifecycle** - Is Initialize() called? Is Update() running?
6. **Profile Performance** - Use System Profiler
7. **Isolate the Issue** - Create minimal reproduction

---

## Getting Help

If you're stuck:

1. Check this documentation first
2. Search existing code for patterns
3. Use the Editor debugging tools
4. Enable verbose logging for more context
5. Create minimal reproduction case

---

## Related Documentation

- [DI Container](DI.md) - Dependency injection
- [ECS System](ECS.md) - Entity Component System
- [Modules](Modules.md) - Module configuration
- [Messaging](Messaging.md) - MessageBus
- [Sync](Sync.md) - Reactive bindings
- [Benchmarks](Benchmarks.md) - Performance reference
