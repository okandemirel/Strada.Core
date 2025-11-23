# Strada API Quick Reference

Fast reference for common Strada APIs.

---

## Dependency Injection

### Registration

```csharp
// Interface to implementation
builder.Register<IService, ServiceImpl>()
       .WithLifetime(Lifetime.Singleton);

// Concrete type
builder.Register<MyService>()
       .WithLifetime(Lifetime.Transient);

// Instance
var config = Resources.Load<CD_Config>("Config");
builder.RegisterInstance(config);

// Factory
builder.RegisterFactory<IFactory>(() => new FactoryImpl());
```

### Resolution

```csharp
// Constructor injection (preferred)
public MyClass(IService service) { }

// Manual resolution
var service = container.Resolve<IService>();

// Safe resolution
if (container.TryResolve<IService>(out var service))
{
    service.DoWork();
}
```

### Lifetimes

```csharp
Lifetime.Singleton   // One instance per container
Lifetime.Transient   // New instance every time
Lifetime.Scoped      // One instance per scope
```

---

## Events

### Define Event

```csharp
public struct PlayerDamagedEvent
{
    public int Damage;
    public Vector3 Position;
}
```

### Publish Event

```csharp
public class DamageController
{
    private readonly IEventBus _eventBus;

    public void ApplyDamage(int amount)
    {
        _eventBus.Publish(new PlayerDamagedEvent
        {
            Damage = amount,
            Position = transform.position
        });
    }
}
```

### Subscribe to Event

```csharp
public class HealthController
{
    public HealthController(IEventBus eventBus)
    {
        eventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent evt)
    {
        Debug.Log($"Player took {evt.Damage} damage");
    }
}
```

### Unsubscribe

```csharp
eventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
```

---

## Modules

### Define Module

```csharp
using Strada.Core.DI;
using Strada.Core.Modules;

public class MyModule : IModuleInstaller
{
    public void Install(IContainerBuilder builder)
    {
        // Register dependencies
        builder.Register<IMyService, MyService>();
    }

    public void Initialize(IContainer container)
    {
        // Initialize after all modules loaded
    }

    public void Shutdown()
    {
        // Cleanup
    }
}
```

### Register Module

```csharp
public class GameBootstrap : MonoBehaviour
{
    private StradaApplication _app;

    private void Awake()
    {
        _app = new StradaApplication();
        _app.RegisterModule<MyModule>();
        _app.Initialize();
    }

    private void OnDestroy()
    {
        _app?.Shutdown();
    }
}
```

### Module Dependencies

```csharp
[ModuleDependency(typeof(CoreModule))]
public class GameModule : IModuleInstaller
{
    // Will load after CoreModule
}
```

---

## ScriptableObject Configs

### Define Config

```csharp
// Value object
[Serializable]
public class PlayerConfig
{
    public float MaxHealth = 100f;
    public float Speed = 5f;

    public bool IsValid()
    {
        return MaxHealth > 0 && Speed > 0;
    }
}

// ScriptableObject
[CreateAssetMenu(fileName = "CD_Player", menuName = "Strada/Configs/Player")]
public class CD_Player : ScriptableObject
{
    public PlayerConfig Config = new PlayerConfig();

    private void OnValidate()
    {
        if (!Config.IsValid())
        {
            Debug.LogWarning($"Invalid config in {name}");
        }
    }
}
```

### Use Config

```csharp
public class PlayerService
{
    private readonly CD_Player _config;

    public PlayerService(CD_Player config)
    {
        _config = config;
    }

    public void Initialize()
    {
        var maxHealth = _config.Config.MaxHealth;
        var speed = _config.Config.Speed;
    }
}
```

---

## ECS Integration

### Define Components

```csharp
using Unity.Entities;
using Unity.Mathematics;

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

### Define System

```csharp
using Unity.Entities;
using Unity.Mathematics;

public partial class PhysicsSystem : SystemBase
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

### Baker (Authoring)

```csharp
using Unity.Entities;
using UnityEngine;

public class BallAuthoring : MonoBehaviour
{
    public float Radius = 0.5f;
    public float Mass = 1.0f;

    class Baker : Baker<BallAuthoring>
    {
        public override void Bake(BallAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new BallComponent
            {
                Radius = authoring.Radius,
                Mass = authoring.Mass
            });
        }
    }
}
```

---

## Commands (MVCS → ECS)

### Define Command

```csharp
public struct SpawnBallCommand
{
    public Vector3 Position;
    public Vector3 Velocity;
}
```

### Send Command

```csharp
public class GameController
{
    private readonly ICommandBus _commandBus;

    public void SpawnBall(Vector3 pos, Vector3 vel)
    {
        _commandBus.Send(new SpawnBallCommand
        {
            Position = pos,
            Velocity = vel
        });
    }
}
```

### Handle Command (in ECS)

```csharp
public partial class SpawnBallSystem : SystemBase
{
    private ICommandBus _commandBus;

    protected override void OnCreate()
    {
        _commandBus = StradaApplication.Instance.Container
                      .Resolve<ICommandBus>();

        _commandBus.Subscribe<SpawnBallCommand>(OnSpawnBall);
    }

    private void OnSpawnBall(SpawnBallCommand cmd)
    {
        var entity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(entity, new BallComponent
        {
            // Initialize from command
        });
    }
}
```

---

## MVCS Pattern

### Model

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

```csharp
public class PlayerController
{
    private readonly PlayerModel _model;
    private readonly PlayerView _view;
    private readonly IEventBus _eventBus;

    public PlayerController(
        PlayerModel model,
        PlayerView view,
        IEventBus eventBus)
    {
        _model = model;
        _view = view;
        _eventBus = eventBus;

        _eventBus.Subscribe<PlayerDamagedEvent>(OnDamaged);
    }

    private void OnDamaged(PlayerDamagedEvent evt)
    {
        _model.TakeDamage(evt.Damage);
        _view.UpdateHealth(_model.Health, _model.MaxHealth);
    }
}
```

### Service

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

## Testing

### Unit Test

```csharp
using NUnit.Framework;

[TestFixture]
public class PlayerModelTests
{
    [Test]
    public void TakeDamage_ReducesHealth()
    {
        // Arrange
        var model = new PlayerModel
        {
            Health = 100,
            MaxHealth = 100
        };

        // Act
        model.TakeDamage(30);

        // Assert
        Assert.AreEqual(70, model.Health);
    }

    [Test]
    public void TakeDamage_BelowZero_ClampsToZero()
    {
        // Arrange
        var model = new PlayerModel { Health = 50 };

        // Act
        model.TakeDamage(100);

        // Assert
        Assert.AreEqual(0, model.Health);
        Assert.IsTrue(model.IsDead);
    }
}
```

### Integration Test

```csharp
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;

[TestFixture]
public class ModuleIntegrationTests
{
    private StradaApplication _app;

    [SetUp]
    public void Setup()
    {
        _app = new StradaApplication();
        _app.RegisterModule<TestModule>();
    }

    [UnityTest]
    public IEnumerator Module_Initializes_Successfully()
    {
        // Act
        _app.Initialize();
        yield return null;

        // Assert
        var service = _app.Container.Resolve<ITestService>();
        Assert.IsNotNull(service);
    }

    [TearDown]
    public void TearDown()
    {
        _app?.Shutdown();
    }
}
```

---

## Validation

### Implement IsValid()

```csharp
[Serializable]
public class WeaponConfig
{
    public float Damage = 10f;
    public float FireRate = 1f;

    public bool IsValid()
    {
        if (Damage <= 0)
            return false;

        if (FireRate <= 0)
            return false;

        return true;
    }
}
```

### OnValidate()

```csharp
[CreateAssetMenu(fileName = "CD_Weapon", menuName = "Strada/Configs/Weapon")]
public class CD_Weapon : ScriptableObject
{
    public WeaponConfig Config = new WeaponConfig();

    private void OnValidate()
    {
        if (!Config.IsValid())
        {
            Debug.LogWarning($"[CD_Weapon] Invalid config in {name}");
        }
    }
}
```

### Custom Validator

```csharp
using Strada.Core.Editor.Validation;

public class WeaponValidator : AssetValidator
{
    public override string ValidatorName => "Weapon Validator";
    public override string Category => "Gameplay";

    public override bool CanValidate(Object asset)
    {
        return asset is CD_Weapon;
    }

    public override ValidationResult Validate(Object asset)
    {
        var result = new ValidationResult();
        var weapon = asset as CD_Weapon;
        var path = AssetDatabase.GetAssetPath(asset);

        ValidateRange(result, weapon.Config.Damage, 1f, 100f, "Damage", path);

        return result;
    }
}
```

---

## Editor Tools

### Custom Inspector

```csharp
using Strada.Core.Editor.Inspectors;
using UnityEditor;

[CustomEditor(typeof(CD_Player))]
public class CD_PlayerInspector : StradaConfigDataInspector<CD_Player>
{
    protected override bool IsConfigValid(out string errorMessage)
    {
        var config = (target as CD_Player).Config;

        if (config.MaxHealth <= 0)
        {
            errorMessage = "MaxHealth must be positive";
            return false;
        }

        errorMessage = "";
        return true;
    }

    protected override void DrawConfigProperties()
    {
        var config = (target as CD_Player).Config;

        StradaEditorGUI.DrawReadOnlyProperty(
            "Time to Death (100 DPS)",
            (config.MaxHealth / 100f).ToString("F1") + "s"
        );
    }

    protected override void OnResetClicked()
    {
        var config = (target as CD_Player).Config;
        config.MaxHealth = 100f;
        config.Speed = 5f;
    }
}
```

---

## Common Patterns

### Singleton Pattern (via DI)

```csharp
// ✅ Use DI instead of manual singleton
builder.Register<IGameManager, GameManager>()
       .WithLifetime(Lifetime.Singleton);
```

### Factory Pattern

```csharp
public interface IEnemyFactory
{
    Enemy Create(EnemyType type);
}

public class EnemyFactory : IEnemyFactory
{
    private readonly IContainer _container;

    public EnemyFactory(IContainer container)
    {
        _container = container;
    }

    public Enemy Create(EnemyType type)
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
    void Delete(int id);
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

    public void Delete(int id)
    {
        _dataService.Delete<Player>(id);
    }
}
```

---

## Performance Tips

### DI Resolution

```csharp
// ✅ Good: Resolve once in constructor
private readonly IService _service;
public MyClass(IService service)
{
    _service = service;
}

// ❌ Bad: Resolve every frame
public void Update()
{
    var service = container.Resolve<IService>(); // Slow!
}
```

### Event Subscriptions

```csharp
// ✅ Good: Subscribe in constructor
public MyController(IEventBus eventBus)
{
    eventBus.Subscribe<MyEvent>(OnMyEvent);
}

// ❌ Bad: Subscribe/unsubscribe every frame
public void Update()
{
    eventBus.Subscribe<MyEvent>(OnMyEvent);
    eventBus.Unsubscribe<MyEvent>(OnMyEvent);
}
```

### ECS Systems

```csharp
// ✅ Good: Use ScheduleParallel
Entities.ForEach((ref VelocityComponent vel) =>
{
    vel.Value += gravity * deltaTime;
}).ScheduleParallel();

// ❌ Bad: Run on main thread
Entities.ForEach((ref VelocityComponent vel) =>
{
    vel.Value += gravity * deltaTime;
}).Run();
```

---

## Troubleshooting

### "Type not registered"

```csharp
// Add registration
builder.Register<IMissingService, MissingServiceImpl>();
```

### "Circular dependency"

```csharp
// Break cycle with events
public class ServiceA
{
    public ServiceA(IEventBus eventBus) { }  // Not ServiceB
}
```

### "Multiple constructors"

```csharp
// Use only one public constructor
public class MyService
{
    public MyService(IDependency dep) { }  // Public for DI
    private MyService() { }                 // Private for tests
}
```

---

## Menu Reference

```
Window > Strada >
├── Module Graph
├── DI Container Inspector
├── ECS World Inspector
├── Command & Event Monitor
└── Runtime Health Check

Tools > Strada >
├── Validate All Assets
├── Validate Modules
└── Build Validation > [Settings]

Assets > Create > Strada >
├── New Module
├── Generate Tests
└── New ScriptableObject Config
```

---

**See Also**:
- [Full API Reference](04-APIReference/)
- [Tutorials](05-Tutorials/)
- [Best Practices](06-BestPractices/)
