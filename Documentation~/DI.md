# Dependency Injection

Strada's DI system provides a high-performance, expression tree compiled container with support for multiple lifetimes.

## Table of Contents

- [Quick Start](#quick-start)
- [ContainerBuilder](#containerbuilder)
- [Auto-Binding](#auto-binding)
- [Lifetimes](#lifetimes)
- [Scopes](#scopes)
- [Factory Registration](#factory-registration)
- [Instance Registration](#instance-registration)
- [Circular Dependency Detection](#circular-dependency-detection)
- [Performance](#performance)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.DI;

// 1. Create builder
var builder = new ContainerBuilder();

// 2. Register services
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
builder.Register<EnemyController>(Lifetime.Transient);

// 3. Build container (immutable after build)
using var container = builder.Build();

// 4. Resolve services
var player = container.Resolve<IPlayerService>();
```

---

## ContainerBuilder

The `ContainerBuilder` is used to configure registrations before building an immutable container.

### Register Interface to Implementation

```csharp
// Most common pattern - register interface with concrete implementation
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// Constructor dependencies are automatically resolved
public class ServiceImpl : IService
{
    public ServiceImpl(IDependency dep) { } // IDependency auto-injected
}
```

### Register Concrete Type

```csharp
// Register a concrete class directly
builder.Register<MyService>(Lifetime.Transient);
```

### Register with Factory

```csharp
// Custom factory for complex construction
builder.RegisterFactory<IService>(container =>
{
    var dep = container.Resolve<IDependency>();
    return new ServiceImpl(dep, "custom-config");
}, Lifetime.Singleton);
```

### Register Instance

```csharp
// Pre-existing instance (always singleton)
var config = LoadConfig();
builder.RegisterInstance<IConfig>(config);
```

### Build Container

```csharp
// Build returns immutable container
IContainer container = builder.Build();

// Container automatically registers itself
var self = container.Resolve<IContainer>(); // Returns same container
```

---

## Auto-Binding

Auto-binding allows automatic service registration using attributes, eliminating manual registration boilerplate.

### Attributes

```csharp
using Strada.Core.DI.Attributes;

// Base attribute with configurable lifetime
[AutoRegister(Lifetime.Singleton)]
public class MyService { }

// Convenience attributes for each lifetime
[AutoRegisterSingleton]
public class SingletonService { }

[AutoRegisterTransient]
public class TransientService { }

[AutoRegisterScoped]
public class ScopedService { }
```

### Register as Interface

```csharp
// Register implementation against interface
[AutoRegisterSingleton(As = typeof(IPlayerService))]
public class PlayerService : IPlayerService { }

// With priority (lower = registered first)
[AutoRegisterSingleton(As = typeof(IWeaponService), Priority = 10)]
public class WeaponService : IWeaponService { }

// Also register as self (both IService and ServiceImpl)
[AutoRegisterSingleton(As = typeof(IService), RegisterSelf = true)]
public class ServiceImpl : IService { }
```

### Using Auto-Binding

```csharp
var builder = new ContainerBuilder();

// Option 1: Use source generator (compile-time, zero reflection)
builder.RegisterAutoBindings();

// Option 2: Runtime reflection scanning with patterns
builder.RegisterAutoBindingsRuntime(
    includePatterns: new[] { "Game.*", "Strada.*" },
    excludePatterns: new[] { "Unity.*", "System.*" }
);

// Option 3: Full control via parameters
builder.RegisterAutoBindings(
    includePatterns: new[] { "Game.*" },
    excludePatterns: new[] { "Unity.*" },
    forceRuntimeScanning: false  // Use source generator if available
);

var container = builder.Build();
```

### Bootstrap Integration

**Recommended Approach: ModuleConfig**

Use `ModuleConfig` with `IModuleBuilder` for modular service registration:

```csharp
[CreateAssetMenu(menuName = "Strada/Game Module Config")]
public class GameModuleConfig : ModuleConfig
{
    protected override void Configure(IModuleBuilder builder)
    {
        builder
            .RegisterService<IPlayerService, PlayerService>()
            .RegisterService<IEnemyService, EnemyService>()
            .RegisterController<GameController>();
    }
}
```

See [Modules documentation](Modules.md) for the full modular architecture guide.

**Legacy Approach: BootstrapConfig (Deprecated)**

The legacy `BootstrapConfig` with auto-binding is still supported but deprecated:

```csharp
// In BootstrapConfig inspector:
// - Enable Auto Binding: true
// - Force Runtime Scanning: false (use source gen)
// - Assembly Include Patterns: "Strada.*", "Game.*"
// - Assembly Exclude Patterns: "Unity.*", "System.*"
```

### Source Generator vs Runtime Scanning

| Approach | Pros | Cons |
|----------|------|------|
| **Source Generator** | Zero reflection overhead, compile-time errors | Requires rebuild on changes |
| **Runtime Scanning** | Hot reload friendly, no rebuild needed | ~3-50ms startup scan |

**Recommendation**: Use source generator for production, runtime scanning for development.

### Performance

| Operation | Time |
|-----------|------|
| First scan (uncached) | **3-50ms** |
| Cached scan lookup | **<1ms** |
| Source generator | **0ms** (compile-time) |

---

## Lifetimes

Strada supports three lifetime scopes:

### Singleton

Single instance shared across entire application lifetime.

```csharp
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

var a = container.Resolve<IService>();
var b = container.Resolve<IService>();
Assert.AreSame(a, b); // Same instance
```

**Thread Safety**: Singleton creation uses `Volatile.Read/Write` for thread-safe lazy initialization. Container disposal is also thread-safe with a `volatile _disposed` flag and lock-based synchronization.

### Transient

New instance created on every resolve.

```csharp
builder.Register<IService, ServiceImpl>(Lifetime.Transient);

var a = container.Resolve<IService>();
var b = container.Resolve<IService>();
Assert.AreNotSame(a, b); // Different instances
```

### Scoped

Single instance within a scope, different between scopes.

```csharp
builder.Register<IService, ServiceImpl>(Lifetime.Scoped);

using (var scope1 = container.CreateScope())
{
    var a = scope1.Resolve<IService>();
    var b = scope1.Resolve<IService>();
    Assert.AreSame(a, b); // Same within scope
}

using (var scope2 = container.CreateScope())
{
    var c = scope2.Resolve<IService>();
    Assert.AreNotSame(a, c); // Different between scopes
}
```

**Important**: Scoped services cannot be resolved from root container - you must create a scope first.

---

## Scopes

Scopes provide isolated lifetimes for scoped registrations.

### Creating Scopes

```csharp
using var scope = container.CreateScope();

// Resolve within scope
var service = scope.Resolve<IScopedService>();
```

### Scope Disposal

When a scope is disposed, all scoped instances that implement `IDisposable` are disposed.

```csharp
using (var scope = container.CreateScope())
{
    var db = scope.Resolve<IDatabase>(); // IDatabase : IDisposable
} // db.Dispose() called automatically
```

### Nested Scopes

Scopes can be nested. Each scope maintains its own scoped instances.

```csharp
using var outer = container.CreateScope();
using var inner = outer.CreateScope(); // Inner scope inherits outer's singletons
```

---

## Factory Registration

For complex construction logic, use factory registration:

```csharp
// Factory with container access
builder.RegisterFactory<IService>(c =>
{
    var config = c.Resolve<IConfig>();
    var logger = c.Resolve<ILogger>();
    return new ServiceImpl(config.ConnectionString, logger);
});

// Conditional construction
builder.RegisterFactory<IPaymentProcessor>(c =>
{
    var config = c.Resolve<IConfig>();
    return config.UseSandbox
        ? new SandboxProcessor()
        : new ProductionProcessor();
});
```

---

## Instance Registration

Pre-created instances are always singletons:

```csharp
// Configuration loaded at startup
var config = JsonUtility.FromJson<GameConfig>(configJson);
builder.RegisterInstance<IConfig>(config);

// ScriptableObject assets
builder.RegisterInstance<IGameSettings>(Resources.Load<GameSettings>("Settings"));
```

---

## Circular Dependency Detection

The builder detects circular dependencies at build time:

```csharp
// This will throw at Build() time
builder.Register<ServiceA>(); // ServiceA depends on ServiceB
builder.Register<ServiceB>(); // ServiceB depends on ServiceA

var container = builder.Build(); // Throws InvalidOperationException
```

**Error Message**: `Circular dependency detected involving type 'ServiceA'`

---

## Performance

Container uses expression tree compilation for near-native performance.

### Benchmarks (Apple Silicon, Unity 6, Mono)

| Operation | Time | Notes |
|-----------|------|-------|
| Simple Transient | **0.11μs** | No dependencies |
| 4-Level Deep Chain | **0.27μs** | A→B→C→D |
| Wide Service (5 deps) | **0.42μs** | 5 injected dependencies |
| Singleton Lookup | **61ns** | After first creation |
| Scoped Lookup | **21ns** | Within scope |
| vs Manual `new()` | **1.56x** | Competitive with best |

### Zero Allocation

- Singleton resolution: **0 bytes** GC allocation
- Scoped resolution: **0 bytes** GC allocation
- Transient: Allocates only the object itself

### Thread Safety

The DI container is hardened for thread-safe operation:

- **TypeRegistry** uses `ConcurrentDictionary` with a configurable `MaxTypeCount` limit (default 8,192)
- **Container disposal** is guarded by a `volatile _disposed` flag
- **ContainerScope disposal** uses lock-based synchronization
- **InjectionProcessor** uses `ConcurrentDictionary` for reflection cache
- **LifecycleProcessor** uses lock synchronization and try-catch around lifecycle method invocations
- **AutoBindingScanner** validates types with `IsAssignableFrom` before registration and uses exception-safe logging

### Type ID System

Container uses a static type ID system for O(1) lookup:

```csharp
// Internal implementation
private static class TypeId<T>
{
    public static readonly int Id = TypeRegistry.GetId<T>();
}

// Resolution is array index lookup
public T Resolve<T>()
{
    var index = _typeIdToIndex[TypeId<T>.Id];
    return (T)_factories[index](this);
}
```

---

## Best Practices

### 1. Register Interfaces, Not Implementations

```csharp
// Good - allows swapping implementations
builder.Register<IPlayerService, PlayerService>();

// Avoid - couples to concrete type
builder.Register<PlayerService>();
```

### 2. Use Singleton for Stateless Services

```csharp
// Services with no mutable state should be singletons
builder.Register<IInputService, InputService>(Lifetime.Singleton);
builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
```

### 3. Use Scoped for Per-Request/Per-Scene

```csharp
// Scene-specific services
builder.Register<ILevelManager, LevelManager>(Lifetime.Scoped);

void OnSceneLoad()
{
    _currentScope?.Dispose();
    _currentScope = _container.CreateScope();
}
```

### 4. Dispose Container on Shutdown

```csharp
void OnApplicationQuit()
{
    _container?.Dispose(); // Disposes all singletons
}
```

### 5. Keep Constructors Simple

```csharp
// Good - just assign dependencies
public class PlayerService : IPlayerService
{
    private readonly IInputService _input;
    private readonly IAudioService _audio;

    public PlayerService(IInputService input, IAudioService audio)
    {
        _input = input;
        _audio = audio;
    }
}

// Avoid - complex logic in constructor
public PlayerService(IInputService input)
{
    _input = input;
    LoadPlayerData(); // Don't do this
    SpawnPlayer();    // Or this
}
```

---

## API Reference

### ContainerBuilder

```csharp
ContainerBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
ContainerBuilder Register<T>(Lifetime lifetime = Lifetime.Singleton)
ContainerBuilder RegisterFactory<T>(Func<IContainer, T> factory, Lifetime lifetime = Lifetime.Transient)
ContainerBuilder RegisterInstance<T>(T instance)
IContainer Build()
```

### IContainer

```csharp
T Resolve<T>() where T : class
object Resolve(Type type)
bool TryResolve<T>(out T instance) where T : class
bool IsRegistered<T>() where T : class
bool IsRegistered(Type type)
IContainerScope CreateScope()
void Dispose()
```

### IContainerScope

```csharp
T Resolve<T>() where T : class
object Resolve(Type type)
bool TryResolve<T>(out T instance) where T : class
IContainerScope CreateScope()
void Dispose()
```

### Lifetime Enum

```csharp
public enum Lifetime
{
    Singleton,  // One instance for entire application
    Transient,  // New instance every resolve
    Scoped      // One instance per scope
}
```

---

## Related Documentation

- [Modules](Modules.md) - ModuleConfig, IModuleBuilder, service registration
- [ECS System](ECS.md) - Entity Component System
- [Messaging](Messaging.md) - MessageBus communication
- [Benchmarks](Benchmarks.md) - Full performance data
