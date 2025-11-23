# Dependency Injection

Complete guide to Strada's powerful DI container.

---

## Overview

Strada's DI container provides:
- **Constructor injection** - Automatic dependency resolution
- **Three lifetimes** - Singleton, Transient, Scoped
- **Interface binding** - Decouple abstractions from implementations
- **Instance registration** - Pre-created objects
- **Factory support** - Lazy instantiation
- **Circular dependency detection** - Prevents infinite loops
- **Performance** - <0.1ms resolution time

---

## Registration

### Interface to Implementation

The most common pattern:

```csharp
public void Install(IContainerBuilder builder)
{
    builder.Register<IPlayerService, PlayerService>()
           .WithLifetime(Lifetime.Singleton);
}
```

This binds `IPlayerService` to `PlayerService`. When something requests `IPlayerService`, it gets a `PlayerService` instance.

### Concrete Types

Register a concrete class directly:

```csharp
builder.Register<PlayerController>()
       .WithLifetime(Lifetime.Singleton);
```

### Instances

Register a pre-created object:

```csharp
var config = Resources.Load<CD_Player>("PlayerConfig");
builder.RegisterInstance(config);
```

The exact instance will be returned when resolved.

### Factories

Lazy instantiation with custom logic:

```csharp
builder.RegisterFactory<IProjectile>(() =>
{
    var prefab = Resources.Load<GameObject>("Projectile");
    return Object.Instantiate(prefab).GetComponent<Projectile>();
});
```

The factory is called each time the type is resolved.

---

## Lifetimes

### Singleton

**One instance for the entire application lifetime.**

```csharp
builder.Register<IGameManager, GameManager>()
       .WithLifetime(Lifetime.Singleton);
```

**Use for**:
- Game managers
- Services that maintain global state
- Configuration objects
- Audio managers

**Characteristics**:
- ✅ Memory efficient (one instance)
- ✅ Fast resolution (cached)
- ⚠️ Shared state across entire app

**Example**:
```csharp
var manager1 = container.Resolve<IGameManager>();
var manager2 = container.Resolve<IGameManager>();

Assert.AreSame(manager1, manager2); // Same instance
```

### Transient

**New instance every time it's resolved.**

```csharp
builder.Register<IProjectile, Projectile>()
       .WithLifetime(Lifetime.Transient);
```

**Use for**:
- Short-lived objects
- Commands
- Requests
- Objects that don't share state

**Characteristics**:
- ✅ No shared state
- ✅ Always fresh instance
- ⚠️ Higher memory allocation

**Example**:
```csharp
var projectile1 = container.Resolve<IProjectile>();
var projectile2 = container.Resolve<IProjectile>();

Assert.AreNotSame(projectile1, projectile2); // Different instances
```

### Scoped

**One instance per scope (e.g., per level, per session).**

```csharp
builder.Register<ILevelManager, LevelManager>()
       .WithLifetime(Lifetime.Scoped);
```

**Use for**:
- Level-specific managers
- Session state
- Per-scene services

**Characteristics**:
- ✅ Isolated per scope
- ✅ Shared within scope
- ⚠️ Requires scope management

**Example**:
```csharp
using (var scope = container.CreateScope())
{
    var manager1 = scope.Resolve<ILevelManager>();
    var manager2 = scope.Resolve<ILevelManager>();

    Assert.AreSame(manager1, manager2); // Same within scope
}

using (var scope2 = container.CreateScope())
{
    var manager3 = scope2.Resolve<ILevelManager>();

    Assert.AreNotSame(manager1, manager3); // Different scope
}
```

---

## Resolution

### Constructor Injection (Recommended)

The DI container automatically injects dependencies:

```csharp
public class PlayerController
{
    private readonly IPlayerService _service;
    private readonly IInputService _input;
    private readonly CD_Player _config;

    // All dependencies auto-injected
    public PlayerController(
        IPlayerService service,
        IInputService input,
        CD_Player config)
    {
        _service = service;
        _input = input;
        _config = config;
    }
}
```

### Manual Resolution

For cases where constructor injection isn't possible:

```csharp
public void Initialize(IContainer container)
{
    var service = container.Resolve<IPlayerService>();
    service.Initialize();
}
```

### TryResolve

Safe resolution that doesn't throw:

```csharp
if (container.TryResolve<IOptionalService>(out var service))
{
    service.DoOptionalWork();
}
```

---

## Advanced Patterns

### Multiple Registrations

Register multiple implementations:

```csharp
builder.Register<IWeapon, Pistol>("pistol");
builder.Register<IWeapon, Rifle>("rifle");
builder.Register<IWeapon, Shotgun>("shotgun");
```

Resolve by name:

```csharp
var pistol = container.Resolve<IWeapon>("pistol");
var rifle = container.Resolve<IWeapon>("rifle");
```

### Conditional Registration

Register based on conditions:

```csharp
if (Application.isEditor)
{
    builder.Register<IAnalytics, MockAnalytics>();
}
else
{
    builder.Register<IAnalytics, RealAnalytics>();
}
```

### Generic Registration

Register generic types:

```csharp
builder.Register(typeof(IRepository<>), typeof(Repository<>));
```

Resolve:

```csharp
var playerRepo = container.Resolve<IRepository<Player>>();
var enemyRepo = container.Resolve<IRepository<Enemy>>();
```

### Decorator Pattern

Wrap services with additional behavior:

```csharp
// Base service
builder.Register<ILogger, ConsoleLogger>()
       .WithLifetime(Lifetime.Singleton);

// Decorator
builder.RegisterFactory<ILogger>(container =>
{
    var baseLogger = container.Resolve<ConsoleLogger>();
    return new TimestampLogger(baseLogger);
});
```

---

## Registration Validation

Strada validates registrations at build time:

```csharp
try
{
    var container = builder.Build();
}
catch (RegistrationException ex)
{
    Debug.LogError($"Invalid registration: {ex.Message}");
}
```

Common errors caught:

### Circular Dependencies

```csharp
// ❌ Error: A depends on B, B depends on A
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { }
}
```

**Solution**: Use an interface or event system to break the cycle.

### Missing Registrations

```csharp
// ❌ Error: IPlayerService not registered
public class PlayerController
{
    public PlayerController(IPlayerService service) { }
}
```

**Solution**: Register the missing dependency:
```csharp
builder.Register<IPlayerService, PlayerService>();
```

### Constructor Ambiguity

```csharp
// ❌ Error: Multiple constructors
public class MyService
{
    public MyService(IServiceA a) { }
    public MyService(IServiceB b) { }
}
```

**Solution**: Use only one public constructor for DI.

---

## Performance Optimization

### Registration Order

Register frequently-used services first:

```csharp
// High-frequency services first
builder.Register<IInputService, InputService>();
builder.Register<IPhysicsService, PhysicsService>();

// Low-frequency services last
builder.Register<IAnalytics, Analytics>();
```

### Pre-warming

Resolve singletons at startup:

```csharp
public void Initialize(IContainer container)
{
    // Pre-warm singletons
    container.Resolve<IGameManager>();
    container.Resolve<IAudioManager>();
    container.Resolve<IInputService>();
}
```

### Avoid Transient for Heavy Objects

```csharp
// ❌ Bad: Creates new GameObject each time
builder.Register<IExpensiveObject, ExpensiveMonoBehaviour>()
       .WithLifetime(Lifetime.Transient);

// ✅ Good: Reuse or use object pooling
builder.Register<IExpensiveObject, ExpensiveMonoBehaviour>()
       .WithLifetime(Lifetime.Singleton);
```

---

## Testing with DI

### Mock Dependencies

```csharp
[Test]
public void PlayerController_TakeDamage_CallsService()
{
    // Arrange
    var mockService = Substitute.For<IPlayerService>();
    var controller = new PlayerController(mockService);

    // Act
    controller.TakeDamage(10);

    // Assert
    mockService.Received(1).ApplyDamage(10);
}
```

### Test Container

Create isolated containers for tests:

```csharp
[SetUp]
public void Setup()
{
    var builder = new ContainerBuilder();
    builder.Register<ITestService, MockTestService>();

    _testContainer = builder.Build();
}

[Test]
public void TestWithContainer()
{
    var service = _testContainer.Resolve<ITestService>();
    Assert.IsNotNull(service);
}

[TearDown]
public void TearDown()
{
    _testContainer?.Dispose();
}
```

---

## Best Practices

### ✅ Do

**Prefer Constructor Injection**
```csharp
// ✅ Good
public class MyController
{
    public MyController(IService service) { }
}
```

**Use Interfaces**
```csharp
// ✅ Good
builder.Register<IService, ServiceImpl>();
```

**Register in Module Installers**
```csharp
// ✅ Good
public void Install(IContainerBuilder builder)
{
    builder.Register<IService, ServiceImpl>();
}
```

**Keep Dependencies Minimal**
```csharp
// ✅ Good: 2-3 dependencies
public MyController(IService service, IConfig config) { }
```

### ❌ Don't

**Use Service Locator Pattern**
```csharp
// ❌ Bad: Hidden dependency
public class MyController
{
    public void DoWork()
    {
        var service = ServiceLocator.Get<IService>();
    }
}
```

**Inject Container**
```csharp
// ❌ Bad: Defeats purpose of DI
public class MyController
{
    public MyController(IContainer container)
    {
        _service = container.Resolve<IService>();
    }
}
```

**Over-inject**
```csharp
// ❌ Bad: Too many dependencies
public MyController(
    IService1 s1, IService2 s2, IService3 s3,
    IService4 s4, IService5 s5, IService6 s6) { }

// ✅ Better: Use a facade or mediator
public MyController(IControllerFacade facade) { }
```

---

## Common Patterns

### Configuration Pattern

```csharp
public void Install(IContainerBuilder builder)
{
    // Load config from resources
    var config = Resources.Load<CD_Game>("GameConfig");

    // Register as singleton instance
    builder.RegisterInstance(config);

    // Services can now depend on config
    builder.Register<IGameService, GameService>();
}

public class GameService
{
    private readonly CD_Game _config;

    public GameService(CD_Game config)
    {
        _config = config;
    }
}
```

### Factory Pattern

```csharp
public interface IEnemyFactory
{
    Enemy CreateEnemy(EnemyType type);
}

public class EnemyFactory : IEnemyFactory
{
    private readonly IContainer _container;

    public EnemyFactory(IContainer container)
    {
        _container = container;
    }

    public Enemy CreateEnemy(EnemyType type)
    {
        var enemy = _container.Resolve<Enemy>();
        enemy.Initialize(type);
        return enemy;
    }
}
```

### Repository Pattern

```csharp
public interface IRepository<T>
{
    T Get(int id);
    void Save(T entity);
}

public class PlayerRepository : IRepository<Player>
{
    private readonly IDataService _dataService;

    public PlayerRepository(IDataService dataService)
    {
        _dataService = dataService;
    }

    public Player Get(int id)
    {
        return _dataService.Load<Player>(id);
    }

    public void Save(Player entity)
    {
        _dataService.Save(entity);
    }
}
```

---

## Troubleshooting

### "Type not registered"

**Problem**: Trying to resolve a type that wasn't registered.

**Solution**:
```csharp
builder.Register<IMissingService, MissingServiceImpl>();
```

### "Circular dependency detected"

**Problem**: A → B → A dependency cycle.

**Solution**: Inject an interface or use events:
```csharp
// Before: A → B → A
// After: A → B → IEvent → A (broken cycle)
```

### "Multiple constructors found"

**Problem**: Class has multiple public constructors.

**Solution**: Keep only one public constructor:
```csharp
public class MyService
{
    // ✅ One public constructor for DI
    public MyService(IDependency dep) { }

    // ✅ Private constructor for tests
    private MyService() { }
}
```

---

## API Reference

### IContainerBuilder

```csharp
public interface IContainerBuilder
{
    IRegistration Register<TInterface, TImplementation>();
    IRegistration Register<TConcrete>();
    void RegisterInstance<T>(T instance);
    void RegisterFactory<T>(Func<T> factory);
    IContainer Build();
}
```

### IContainer

```csharp
public interface IContainer
{
    T Resolve<T>();
    object Resolve(Type type);
    bool TryResolve<T>(out T instance);
    IScope CreateScope();
    void Dispose();
}
```

### IRegistration

```csharp
public interface IRegistration
{
    IRegistration WithLifetime(Lifetime lifetime);
    IRegistration WithName(string name);
}
```

---

**Previous**: [Overview](Overview.md) | **Next**: [MVCS Pattern](MVCS.md)
