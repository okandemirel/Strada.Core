# Core Concepts

Understanding Strada's fundamental architecture patterns.

---

## The Hybrid Approach

Strada combines two powerful paradigms:

### MVCS (Model-View-Controller-Service)
**Best for**: Business logic, UI, user interaction, complex state management

```
User Input → Controller → Service → Model → Event → View
```

- **Clean code**: Separation of concerns
- **Testable**: Easy to mock and unit test
- **Maintainable**: Clear responsibilities

### ECS (Entity Component System)
**Best for**: Performance-critical operations, data transformations, simulations

```
Components → Systems → Entities (Data-Oriented)
```

- **High performance**: Cache-friendly, SIMD-ready
- **Scalable**: Handles thousands of entities
- **Parallel**: Job system integration

### The Bridge: Commands & Events

```
MVCS ──[Commands]──→ ECS
MVCS ←──[Events]──── ECS
```

Seamless communication between both paradigms.

---

## 1. Dependency Injection (DI)

### What is DI?

Instead of creating dependencies manually:

```csharp
// ❌ Bad: Hard to test, tightly coupled
public class PlayerController
{
    private PlayerService _service = new PlayerService();
}
```

Use constructor injection:

```csharp
// ✅ Good: Testable, flexible
public class PlayerController
{
    private readonly IPlayerService _service;

    public PlayerController(IPlayerService service)
    {
        _service = service; // Injected by container
    }
}
```

### Lifetimes

**Singleton** - One instance for the entire application
```csharp
builder.Register<IGameManager, GameManager>()
       .WithLifetime(Lifetime.Singleton);
```

**Transient** - New instance every time
```csharp
builder.Register<IProjectile, Projectile>()
       .WithLifetime(Lifetime.Transient);
```

**Scoped** - One instance per scope (e.g., per level)
```csharp
builder.Register<ILevelManager, LevelManager>()
       .WithLifetime(Lifetime.Scoped);
```

### Registration

```csharp
public void Install(IContainerBuilder builder)
{
    // Interface → Implementation
    builder.Register<IService, ServiceImpl>();

    // Concrete instance
    builder.RegisterInstance(myConfig);

    // Factory
    builder.RegisterFactory<IPlayer>(() => new Player());
}
```

### Resolution

```csharp
// Automatic (via constructor)
public MyClass(IService service) { }

// Manual
var service = container.Resolve<IService>();
```

---

## 2. MVCS Architecture

### Model
**Responsibility**: Data and business rules

```csharp
public class PlayerModel
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }

    public bool IsDead => Health <= 0;

    public void TakeDamage(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
    }
}
```

### View
**Responsibility**: Presentation and user input

```csharp
public class PlayerView : MonoBehaviour
{
    [SerializeField] private Slider _healthBar;

    public void UpdateHealth(int current, int max)
    {
        _healthBar.value = (float)current / max;
    }
}
```

### Controller
**Responsibility**: Coordinate between Model and View

```csharp
public class PlayerController
{
    private readonly PlayerModel _model;
    private readonly PlayerView _view;
    private readonly IEventBus _eventBus;

    public PlayerController(PlayerModel model, PlayerView view, IEventBus eventBus)
    {
        _model = model;
        _view = view;
        _eventBus = eventBus;

        _eventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent evt)
    {
        _model.TakeDamage(evt.Amount);
        _view.UpdateHealth(_model.Health, _model.MaxHealth);

        if (_model.IsDead)
        {
            _eventBus.Publish(new PlayerDiedEvent());
        }
    }
}
```

### Service
**Responsibility**: Shared business logic and utilities

```csharp
public interface IScoreService
{
    int GetScore();
    void AddScore(int points);
}

public class ScoreService : IScoreService
{
    private int _score;

    public int GetScore() => _score;

    public void AddScore(int points)
    {
        _score += points;
    }
}
```

---

## 3. Event System

### Publishing Events

```csharp
public class WeaponController
{
    private readonly IEventBus _eventBus;

    public void Fire()
    {
        _eventBus.Publish(new WeaponFiredEvent
        {
            Position = transform.position,
            Direction = transform.forward
        });
    }
}
```

### Subscribing to Events

```csharp
public class AudioController
{
    public AudioController(IEventBus eventBus)
    {
        eventBus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
    }

    private void OnWeaponFired(WeaponFiredEvent evt)
    {
        PlaySoundAt(evt.Position);
    }
}
```

### Event Types

```csharp
public struct WeaponFiredEvent
{
    public Vector3 Position;
    public Vector3 Direction;
    public int Damage;
}
```

---

## 4. Module System

### What is a Module?

A self-contained, reusable piece of functionality:

```
MyModule/
├── Scripts/
│   ├── Models/
│   ├── Views/
│   ├── Controllers/
│   ├── Services/
│   └── Data/
├── MyModuleInstaller.cs
└── MyModule.asmdef
```

### Module Installer

```csharp
public class WeaponModule : IModuleInstaller
{
    public void Install(IContainerBuilder builder)
    {
        // Register dependencies
        builder.Register<IWeaponService, WeaponService>();
        builder.Register<WeaponController>();
    }

    public void Initialize(IContainer container)
    {
        // Initialize after all modules loaded
        var controller = container.Resolve<WeaponController>();
        controller.Initialize();
    }

    public void Shutdown()
    {
        // Cleanup
    }
}
```

### Module Dependencies

Modules can depend on other modules:

```csharp
[ModuleDependency(typeof(CoreModule))]
[ModuleDependency(typeof(InputModule))]
public class GameplayModule : IModuleInstaller
{
    // Will be loaded after Core and Input
}
```

---

## 5. ECS Integration

### When to Use ECS

Use ECS for:
- ✅ Thousands of similar entities (bullets, particles, enemies)
- ✅ Data transformations (position updates, physics)
- ✅ Performance-critical loops
- ✅ Parallel processing

Use MVCS for:
- ✅ Complex business logic
- ✅ UI and user interaction
- ✅ Unique game objects (player, boss)
- ✅ State machines

### ECS Components

```csharp
public struct BallComponent : IComponentData
{
    public float Radius;
    public float Mass;
}

public struct VelocityComponent : IComponentData
{
    public float3 Value;
}
```

### ECS Systems

```csharp
public partial class BallPhysicsSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities.ForEach((ref VelocityComponent velocity) =>
        {
            velocity.Value += new float3(0, -9.81f, 0) * deltaTime;
        }).ScheduleParallel();
    }
}
```

### MVCS ↔ ECS Communication

**MVCS to ECS** (via Commands):
```csharp
// In MVCS Controller
commandBus.Send(new SpawnBallCommand
{
    Position = spawnPoint,
    Velocity = initialVelocity
});
```

**ECS to MVCS** (via Events):
```csharp
// In ECS System
eventBus.Publish(new BallLandedEvent
{
    Position = landPosition,
    Score = calculatedScore
});
```

---

## 6. ScriptableObject Configs

### Why ScriptableObjects?

- Designer-friendly (editable in Inspector)
- Asset-based (version controlled)
- Reusable across scenes
- Validated automatically

### Structure

**CD_* (Config Data)** - Unity ScriptableObject:
```csharp
[CreateAssetMenu(fileName = "CD_BallPhysics", menuName = "Strada/Configs/BallPhysics")]
public class CD_BallPhysics : ScriptableObject
{
    public BallPhysicsConfig Config = new BallPhysicsConfig();

    private void OnValidate()
    {
        if (!Config.IsValid())
        {
            Debug.LogWarning($"[CD_BallPhysics] Invalid configuration in {name}");
        }
    }
}
```

**Value Object** - Pure C# data:
```csharp
[Serializable]
public class BallPhysicsConfig
{
    public float Radius = 0.5f;
    public float Mass = 1.0f;
    public float Bounciness = 0.7f;

    public bool IsValid()
    {
        return Radius > 0 && Mass > 0 && Bounciness >= 0 && Bounciness <= 1;
    }
}
```

### Usage

```csharp
public class BallService
{
    private readonly CD_BallPhysics _config;

    public BallService(CD_BallPhysics config)
    {
        _config = config;
    }

    public void SpawnBall()
    {
        var radius = _config.Config.Radius;
        var mass = _config.Config.Mass;
        // Use config values...
    }
}
```

---

## 7. Testing

### Unit Tests

```csharp
[Test]
public void PlayerTakeDamage_ReducesHealth()
{
    // Arrange
    var model = new PlayerModel { Health = 100 };

    // Act
    model.TakeDamage(30);

    // Assert
    Assert.AreEqual(70, model.Health);
}
```

### Integration Tests

```csharp
[UnityTest]
public IEnumerator ModuleInitialization_RegistersServices()
{
    // Arrange
    var app = new StradaApplication();
    app.RegisterModule<TestModule>();

    // Act
    app.Initialize();
    yield return null;

    // Assert
    var service = app.Container.Resolve<ITestService>();
    Assert.IsNotNull(service);
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────┐
│         Strada Application              │
├─────────────────────────────────────────┤
│                                         │
│  ┌──────────────┐    ┌──────────────┐  │
│  │  MVCS Layer  │    │  ECS Layer   │  │
│  ├──────────────┤    ├──────────────┤  │
│  │              │    │              │  │
│  │  Models      │    │  Components  │  │
│  │  Views       │◄──►│  Systems     │  │
│  │  Controllers │    │  Entities    │  │
│  │  Services    │    │              │  │
│  │              │    │              │  │
│  └──────┬───────┘    └──────┬───────┘  │
│         │                   │          │
│         └───────┬───────────┘          │
│                 │                      │
│         ┌───────▼───────┐              │
│         │  Event Bus    │              │
│         │  DI Container │              │
│         │  Module Mgr   │              │
│         └───────────────┘              │
│                                         │
└─────────────────────────────────────────┘
```

---

## Best Practices Summary

### ✅ Do
- Use DI for all dependencies
- Keep Models pure (no Unity references)
- Use events for decoupled communication
- Validate configs with IsValid()
- Write tests for business logic
- Use ECS for performance-critical code

### ❌ Don't
- Use Singletons (use DI instead)
- Put business logic in MonoBehaviours
- Tightly couple modules
- Skip validation
- Mix MVCS and ECS responsibilities
- Resolve dependencies manually

---

## Next Steps

- **[First Module](FirstModule.md)** - Build a complete module
- **[Architecture Deep Dive](../02-Architecture/Overview.md)** - Detailed architecture docs
- **[Tutorials](../05-Tutorials/)** - Hands-on examples

---

**Previous**: [Quick Start](QuickStart.md) | **Next**: [First Module](FirstModule.md)
