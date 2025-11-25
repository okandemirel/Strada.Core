# Dependency Injection System

Strada features a custom-built, high-performance Dependency Injection (DI) container designed to rival and exceed the speed of industry leaders like **Reflex** and **VContainer**.

## Performance First

Most DI containers use Reflection at runtime (slow) or complex source generators (complex setup). Strada uses **Expression Tree Compilation** with a specialized **Array-Lookup Optimization**.

### Benchmarks (Verified Nov 2025)

| Operation | Time per Op | Comparison |
|-----------|-------------|------------|
| **Singleton Resolve** | **0.05 μs** | ~Native Array Access |
| **Transient Resolve** | **0.29 μs** | 3x Faster than Reflex |
| **Scope Creation** | **2.00 μs** | Extremely Cheap |

## How It Works

### 1. The "Baked" Logic Strategy
Unlike standard containers that check `if (singleton) ... else if (transient)` every time you ask for an object, Strada "bakes" this decision into the delegate itself during the `Build()` phase.

When you call `Resolve<T>()`, the container performs exactly **one array lookup** and **one delegate invocation**. There are zero branching instructions on the hot path.

### 2. IIndexResolver Pattern
To support hierarchical Scopes without performance loss, Strada uses an internal `IIndexResolver` interface. Both the Root Container and Child Scopes implement this. Factories are compiled to accept `IIndexResolver`, allowing them to work seamlessly in any context without casting or overhead.

### 3. Scoped Factory Bypass
A common performance killer in hierarchical DI is bubbling calls up to the parent. Strada compiles specialized "Scoped Factories" that allow child scopes to instantiate objects directly using the raw factory logic, while maintaining strict safety checks (throwing exceptions) if you accidentally try to resolve a Scoped object from the Root.

## Usage

### Basic Setup

```csharp
var builder = new ContainerBuilder();

// Singleton: Created once, shared everywhere
builder.Register<IInputService, InputService>(Lifetime.Singleton);

// Transient: Created new every time
builder.Register<Enemy>(Lifetime.Transient);

// Scoped: Created once per Scope (e.g., per Game Level or per Window)
builder.Register<LevelContext>(Lifetime.Scoped);

IContainer container = builder.Build();
```

### Using Scopes

```csharp
using (var scope = container.CreateScope())
{
    // Resolves a new instance specific to this scope
    var levelCtx = scope.Resolve<LevelContext>(); 
}
```

### Async Resolution
Strada supports true async resolution for loading heavy assets or waiting for initialization.

```csharp
// Returns ValueTask<T>
var heavyAsset = await container.ResolveAsync<IHeavyAsset>();
```

## Best Practices

1.  **Register Interfaces, Resolve Interfaces:** Always bind implementation to interface.
2.  **Avoid Service Locator:** Don't pass `IContainer` around. Use constructor injection.
3.  **Use Scopes for Levels:** Create a new Scope when loading a scene/level and dispose of it when unloading. This automatically cleans up all Scoped services and IDisposable instances.
