# Modular Architecture

Strada's modular architecture provides a unified pattern for organizing game code into self-contained modules with Inspector-configurable systems and services.

## Table of Contents

- [Overview](#overview)
- [Module Generator](#module-generator)
- [Quick Start](#quick-start)
- [ModuleConfig](#moduleconfig)
- [GameBootstrapperConfig](#gamebootstrapperconfig)
- [System Configuration](#system-configuration)
- [Service Configuration](#service-configuration)
- [IModuleBuilder API](#imodulebuilder-api)
- [System Discovery](#system-discovery)
- [Migration from Legacy](#migration-from-legacy)

---

## Overview

The modular architecture combines:
- **VContainer-like fluent API** for DI registration
- **Quantum 3-style Inspector configuration** for ECS systems
- **ScriptableObject-based modules** for easy configuration

```
┌─────────────────────────────────────────────────────────────────┐
│                    GameBootstrapperConfig                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ CoreModule  │  │ GameModule  │  │  UIModule   │   ...        │
│  │  Config     │  │  Config     │  │  Config     │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│         │                │                │                      │
│         ▼                ▼                ▼                      │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                     IModuleBuilder                          ││
│  │  Register<IService, ServiceImpl>()                          ││
│  │  RegisterModel<IModel, ModelImpl>()                         ││
│  │  RegisterController<Controller>()                           ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                   │
│                              ▼                                   │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                      SystemRunner                           ││
│  │  Systems configured per-module via Inspector                ││
│  │  Ordered by UpdatePhase and priority                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

---

## Module Generator

The Strada Module Generator is a powerful editor tool that creates complete module structures with a single click.

### Accessing the Generator

- **Menu:** `Strada > Module Generator`
- **Context Menu:** Right-click in Project → `Create > Strada > New Module Here`
- **Shortcut:** `Ctrl+Enter` to generate (when window is focused)

### Module Types

| Type | Description | Use Case |
|------|-------------|----------|
| **Main** | Standalone module with full structure and assembly definition | Core features, major game systems |
| **Sub** | Child module that inherits parent's assembly definition | Feature extensions, sub-systems |
| **Screen** | UI-focused module with View and Mediator components | UI screens, dialogs, panels |
| **Test** | Test module for unit and integration testing | Module testing |

### Components to Generate

#### ECS Components
- **System** - Creates `{Name}System.cs` with SystemBase template
- **Component** - Creates `{Name}Component.cs` ECS component
- **Entity Mediator** - Creates `{Name}Mediator.cs` for entity-view binding

#### MVCS Pattern
- **Service Interface** - Creates `I{Name}Service.cs` interface
- **Service** - Creates `{Name}Service.cs` implementation
- **Controller** - Creates `{Name}Controller.cs` logic orchestrator
- **Model** - Creates `{Name}Model.cs` state container
- **View** - Creates `{Name}View.cs` MonoBehaviour view

#### Data & Events
- **ConfigData** - Creates `CD_{Name}.cs` ScriptableObject config
- **ValueObject** - Creates `{Name}Config.cs` POCO config
- **Events** - Creates `{Name}Events.cs` event definitions
- **Signals** - Creates `{Name}Signals.cs` signal definitions

#### Infrastructure
- **ModuleConfig** - Creates `{Name}ModuleConfig.cs` ScriptableObject (Main modules only)
- **Assembly Definition** - Creates `{Name}.asmdef` (Main modules only)
- **Runtime Tests** - Creates test structure with NUnit
- **Editor Tests** - Creates editor test structure
- **Editor Scripts** - Creates Editor folder with `.Editor.asmdef`

#### Optional Folders
- Resources, Prefabs, Scenes, Sprites, Art, Audio

### Hierarchical Module View

The generator displays existing modules in a tree structure:
- Expandable/collapsible with ▶/▼ arrows
- Type labels: **(Sub)**, **(Screen)**, **(Test)**
- Color-coded by type for visual identification
- **Select** button to quickly set a parent module

### Generated Structure (Main Module)

```
Assets/Modules/PlayerModule/
├── Scripts/
│   ├── Interfaces/
│   │   └── IPlayerService.cs
│   ├── Services/
│   │   └── PlayerService.cs
│   ├── Controllers/
│   │   └── PlayerController.cs
│   ├── Systems/
│   │   └── PlayerSystem.cs
│   ├── Components/
│   │   └── PlayerComponent.cs
│   └── Data/
│       ├── UnityObjects/
│       │   └── CD_Player.cs
│       └── ValueObjects/
│           └── PlayerConfig.cs
├── Editor/
│   └── Player.Editor.asmdef
├── Tests/
│   ├── Runtime/
│   │   └── PlayerTests.cs
│   ├── Editor/
│   │   └── PlayerEditorTests.cs
│   └── Player.Tests.asmdef
├── Resources/
├── Prefabs/
├── PlayerModuleConfig.cs
└── Player.asmdef
```

### Post-Generation Options

| Option | Description |
|--------|-------------|
| **Register in GameBootstrapper** | Auto-adds module to GameBootstrapperConfig |
| **Create ModuleConfig Asset** | Creates ScriptableObject asset after compilation |
| **Open Folder** | Opens the created folder in Project window |

### Example: Creating a Player Module

1. Open `Strada > Module Generator`
2. Enter "Player" as Module Name
3. Select **Main** as Module Type
4. Choose components:
   - ✓ Service Interface
   - ✓ Service
   - ✓ Controller
   - ✓ ConfigData
   - ✓ ModuleConfig
   - ✓ Assembly Definition
5. Click **Create Module** (or `Ctrl+Enter`)

### Example: Creating a Sub-Module

1. Open Module Generator
2. Enter "Inventory" as Module Name
3. Select **Sub** as Module Type
4. In the Parent Module hierarchy, click **Select** on "PlayerModule"
5. The target path auto-updates to the parent module path
6. Click **Create Module**

The sub-module will be created inside the parent module folder and use the parent's assembly definition.

---

## Quick Start

### 1. Create a Module Config

```csharp
using Strada.Core.Modules;
using UnityEngine;

[CreateAssetMenu(fileName = "GameModuleConfig", menuName = "Strada/Game Module Config")]
public class GameModuleConfig : ModuleConfig
{
    [Header("Game Settings")]
    [SerializeField] private float _gameSpeed = 1.0f;

    public float GameSpeed => _gameSpeed;

    protected override void Configure(IModuleBuilder builder)
    {
        builder
            .RegisterService<IGameService, GameService>()
            .RegisterModel<IPlayerModel, PlayerModel>()
            .RegisterController<GameController>();
    }

    public override void Initialize(IServiceLocator services)
    {
        var gameService = services.Resolve<IGameService>();
        gameService.SetSpeed(_gameSpeed);
    }
}
```

### 2. Configure Systems in Inspector

1. Create the ScriptableObject asset
2. In the Inspector, add systems to the **ECS Systems** list
3. Click **Discover** to auto-find systems marked with `[StradaSystem]`
4. Configure phase (Update/LateUpdate/FixedUpdate) and order

### 3. Create GameBootstrapperConfig

1. Create a `GameBootstrapperConfig` asset
2. Add your module configs to the Modules list
3. Modules are initialized in priority order (lower = first)

### 4. Setup Scene

```csharp
// GameBootstrapper MonoBehaviour
[SerializeField] private GameBootstrapperConfig _gameConfig;
```

---

## ModuleConfig

`ModuleConfig` is the base class for all module configurations. It replaces both `IModuleInstaller` and MonoBehaviour bootstrappers.

### Creating a Module

```csharp
using Strada.Core.Modules;
using UnityEngine;

[CreateAssetMenu(fileName = "MyModuleConfig", menuName = "Game/My Module Config")]
public class MyModuleConfig : ModuleConfig
{
    [Header("Module Settings")]
    [SerializeField] private int _maxEntities = 1000;

    protected override void Configure(IModuleBuilder builder)
    {
        builder
            .Register<IMyService, MyService>()
            .RegisterInstance(new MySettings { MaxEntities = _maxEntities });
    }

    public override void Initialize(IServiceLocator services)
    {
        Debug.Log($"Module {ModuleName} initialized");
    }

    public override void Shutdown()
    {
        Debug.Log($"Module {ModuleName} shutdown");
    }
}
```

### Module Properties

| Property | Description |
|----------|-------------|
| `ModuleName` | Display name (defaults to asset name) |
| `Priority` | Initialization order (lower = first) |
| `Enabled` | Toggle module on/off |
| `Systems` | List of ECS systems to register |
| `Services` | List of DI services to register |
| `Dependencies` | Other modules this depends on |

### Lifecycle Methods

```csharp
// Called during container building
protected virtual void Configure(IModuleBuilder builder) { }

// Called after container is built, services are available
public virtual void Initialize(IServiceLocator services) { }

// Called on application shutdown (reverse order)
public virtual void Shutdown() { }
```

---

## GameBootstrapperConfig

`GameBootstrapperConfig` is the central orchestrator that manages all modules.

### Creating the Config

```csharp
// Create via Assets > Create > Strada > Game Bootstrapper Config
```

### Settings

| Setting | Description |
|---------|-------------|
| `Modules` | List of ModuleConfig assets to load |
| `Verbose Logging` | Enable detailed initialization logs |
| `Validate On Start` | Check for configuration errors at startup |
| `Fail On Validation Error` | Throw exception on validation failure |
| `Async Initialization` | Spread initialization across frames |

### Inspector Features

- **Find All**: Discovers all ModuleConfig assets in project
- **Sort**: Orders modules by priority
- **Validation**: Shows configuration errors/warnings
- **Initialization Order**: Preview of module load order

---

## System Configuration

Systems can be configured entirely through the Inspector.

### SystemEntry Fields

| Field | Description |
|-------|-------------|
| `Enabled` | Toggle system on/off |
| `System Type` | The system class (dropdown) |
| `Phase` | Update, LateUpdate, FixedUpdate, or Initialization |
| `Order` | Execution order within phase |
| `Category` | Grouping for organization |
| `Description` | Documentation for the system |

### Marking Systems for Discovery

```csharp
using Strada.Core.Modules;

[StradaSystem(
    Module = "Combat",
    Category = "Damage",
    Description = "Applies damage to entities",
    Phase = UpdatePhase.Update,
    Order = 100
)]
public class DamageSystem : SystemBase
{
    protected override void OnUpdate(float deltaTime)
    {
        ForEach<Health, DamageReceiver>((int e, ref Health h, ref DamageReceiver d) =>
        {
            h.Current -= d.PendingDamage;
            d.PendingDamage = 0;
        });
    }
}
```

### StradaSystem Attribute Properties

| Property | Description |
|----------|-------------|
| `Module` | Module name for filtering |
| `Category` | Category for grouping in menus |
| `Description` | Tooltip/documentation |
| `Phase` | Default UpdatePhase |
| `Order` | Default execution order |

---

## Service Configuration

Services can be registered via Inspector or code.

### ServiceEntry Fields

| Field | Description |
|-------|-------------|
| `Enabled` | Toggle service on/off |
| `Interface Type` | The service interface |
| `Implementation Type` | The concrete implementation |
| `Lifetime` | Singleton, Transient, or Scoped |

### Code Registration (Recommended)

```csharp
protected override void Configure(IModuleBuilder builder)
{
    builder
        .RegisterService<IPlayerService, PlayerService>()
        .RegisterService<IEnemyService, EnemyService>()
        .RegisterFactory<IWeaponFactory, WeaponFactory>();
}
```

---

## IModuleBuilder API

The `IModuleBuilder` provides a VContainer-like fluent API for service registration.

### Registration Methods

```csharp
// Interface → Implementation with lifetime
IModuleBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)

// Concrete type
IModuleBuilder Register<T>(Lifetime lifetime = Lifetime.Singleton)

// Runtime types
IModuleBuilder Register(Type interfaceType, Type implementationType, Lifetime lifetime)

// Pre-created instance
IModuleBuilder RegisterInstance<T>(T instance)

// Factory function
IModuleBuilder RegisterFactory<T>(Func<IServiceLocator, T> factory, Lifetime lifetime)
```

### Convenience Methods

```csharp
// Singleton model (state container)
builder.RegisterModel<IPlayerModel, PlayerModel>();

// Singleton controller (logic orchestrator)
builder.RegisterController<GameController>();

// Singleton service (business logic)
builder.RegisterService<IGameService, GameService>();

// Singleton factory (object creation)
builder.RegisterFactory<IEnemyFactory, EnemyFactory>();
```

### Example

```csharp
protected override void Configure(IModuleBuilder builder)
{
    builder
        .RegisterModel<IGameState, GameState>()
        .RegisterService<ISpawnService, SpawnService>()
        .RegisterController<WaveController>()
        .RegisterFactory(services => new EnemyPool(
            services.Resolve<IEnemyFactory>(),
            initialSize: 50
        ));
}
```

---

## System Discovery

The `[StradaSystem]` attribute enables automatic system discovery in the Inspector.

### Marking Systems

```csharp
[StradaSystem(Module = "BoardDefence", Category = "Movement")]
public class MovementSystem : SystemBase<Position, Velocity>
{
    protected override void OnUpdateEntity(int entity, ref Position p, ref Velocity v, float dt)
    {
        p.X += v.X * dt;
        p.Y += v.Y * dt;
    }
}
```

### Discovery in Editor

1. Open a ModuleConfig in Inspector
2. Click **Discover** button in Systems section
3. Select systems to add from the dropdown
4. Systems are filtered by module name match

### RuntimeSystemDiscovery API

```csharp
// Discover all systems
var systems = RuntimeSystemDiscovery.DiscoverSystems();

// Discover systems for a specific module
var moduleSystems = RuntimeSystemDiscovery.DiscoverSystems("BoardDefence");

// Refresh cache after assembly changes
RuntimeSystemDiscovery.Refresh();
```

---

## Migration from Legacy

### From IModuleInstaller

**Before (Legacy):**
```csharp
public class GameModuleInstaller : IModuleInstaller
{
    public void Install(ContainerBuilder builder)
    {
        builder.Register<IGameService, GameService>();
    }

    public void Initialize(IContainer container)
    {
        var service = container.Resolve<IGameService>();
    }
}
```

**After (ModuleConfig):**
```csharp
[CreateAssetMenu(menuName = "Strada/Game Module Config")]
public class GameModuleConfig : ModuleConfig
{
    protected override void Configure(IModuleBuilder builder)
    {
        builder.RegisterService<IGameService, GameService>();
    }

    public override void Initialize(IServiceLocator services)
    {
        var service = services.Resolve<IGameService>();
    }
}
```

### From BootstrapConfig

**Before:** Use `BootstrapConfig` with assembly patterns and `IModuleInstaller` classes.

**After:** Use `GameBootstrapperConfig` with `ModuleConfig` ScriptableObjects.

### Key Differences

| Legacy | New |
|--------|-----|
| `IModuleInstaller` classes | `ModuleConfig` ScriptableObjects |
| `ContainerBuilder` directly | `IModuleBuilder` wrapper |
| `IContainer` for resolution | `IServiceLocator` (read-only) |
| Code-only system registration | Inspector + code systems |
| Assembly pattern scanning | Explicit module references |

---

## Best Practices

### 1. One Module Per Feature

```
Modules/
├── CoreModuleConfig.asset       # Shared services
├── PlayerModuleConfig.asset     # Player systems/services
├── EnemyModuleConfig.asset      # Enemy AI and spawning
├── UIModuleConfig.asset         # UI controllers
└── AudioModuleConfig.asset      # Sound management
```

### 2. Use Priority for Dependencies

```csharp
// CoreModuleConfig: Priority = 0 (loads first)
// GameModuleConfig: Priority = 100 (loads after core)
// UIModuleConfig: Priority = 200 (loads after game logic)
```

### 3. Configure Systems in Inspector

Prefer Inspector configuration for:
- Easy toggling during development
- Designer-friendly tuning
- No recompilation needed

### 4. Register Services in Code

Use `Configure()` for:
- Complex registration logic
- Factory registrations
- Conditional registrations

### 5. Use IServiceLocator in Initialize

```csharp
public override void Initialize(IServiceLocator services)
{
    // Read-only access - cannot register new services
    var game = services.Resolve<IGameService>();
}
```

---

## Related Documentation

- [DI Container](DI.md) - Dependency injection details
- [ECS System](ECS.md) - Entity Component System
- [Messaging](Messaging.md) - MessageBus communication
