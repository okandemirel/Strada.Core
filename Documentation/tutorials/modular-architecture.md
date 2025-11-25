# Modular Architecture & Bridge Examples

This guide demonstrates how to structure your project using **Modules** to ensure loose coupling, testability, and assembly separation. We will build two interacting modules: `InputModule` (a pure Service provider) and `PlayerModule` (a hybrid MVCS+ECS module using the Bridge).

## 📂 Directory Structure

We strictly avoid a monolithic `Scripts` folder. Every feature is a Module.

```
Assets/Modules/
├── InputModule/
│   ├── InputModule.asmdef
│   └── Scripts/
│       ├── InputModuleBootstrapper.cs
│       ├── Interfaces/
│       │   └── IInputService.cs
│       ├── Models/
│       │   └── InputModel.cs
│       └── Services/
│           └── UnityInputService.cs
│
└── PlayerModule/
    ├── PlayerModule.asmdef
    └── Scripts/
        ├── PlayerModuleBootstrapper.cs
        ├── Components/       # ECS Data
        │   ├── PlayerTag.cs
        │   └── MovementData.cs
        ├── Systems/          # ECS Logic
        │   └── PlayerMovementSystem.cs
        ├── Views/            # Unity Bridge (Views)
        │   └── PlayerView.cs
        └── Mediators/        # Logic Glue
            └── PlayerMediator.cs
```

---

## 🎮 Example 1: InputModule (Pure MVCS)

This module provides input data to the rest of the game. It exposes an interface `IInputService` but keeps the implementation hidden.

### 1. The Interface (`Scripts/Interfaces/IInputService.cs`)
*This is the only thing other modules should reference.*
```csharp
using UnityEngine;

public interface IInputService
{
    Vector2 Movement { get; }
    bool IsJumpPressed { get; }
}
```

### 2. The Implementation (`Scripts/Services/UnityInputService.cs`)
```csharp
using UnityEngine;
using Strada.Core.DI.Attributes;

[StradaService(ServiceLifetime.Singleton)]
public class UnityInputService : IInputService
{
    public Vector2 Movement => new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    public bool IsJumpPressed => Input.GetButtonDown("Jump");
}
```

### 3. The Bootstrapper (`Scripts/InputModuleBootstrapper.cs`)
```csharp
using Strada.Core.Modules;
using Strada.Core.DI;

public class InputModuleBootstrapper : IModuleBootstrapper
{
    public void Register(ContainerBuilder builder)
    {
        // Map the interface to the implementation
        builder.Register<IInputService, UnityInputService>(Lifetime.Singleton);
    }
}
```

---

## 🌉 Example 2: PlayerModule (The Bridge)

This module demonstrates the **Unified Bridge**. It uses ECS for movement logic but syncs with a Unity GameObject via `PlayerView`. It consumes `IInputService` via Dependency Injection.

### 1. ECS Components (`Scripts/Components/MovementData.cs`)
```csharp
using Strada.Core.ECS;
using Unity.Mathematics;

public struct MovementData : IComponent
{
    public float3 Velocity;
    public float Speed;
}

public struct PlayerTag : IComponent { }
```

### 2. The Bridge View (`Scripts/Views/PlayerView.cs`)
**The Bridge** allows a `MonoBehaviour` to bind directly to ECS data.

```csharp
using Strada.Core.Bridge;
using UnityEngine;

public class PlayerView : EntityView
{
    [SerializeField] private Animator _animator;

    // 🔗 AUTOMATIC BINDING
    // This method is called whenever 'MovementData' on the linked Entity changes.
    [BindComponent]
    public void OnMovementChanged(ref MovementData move)
    {
        // Sync ECS position to Unity Transform
        // (Note: ViewSyncSystem can handles position automatically, but this shows manual control)
        
        // Update Animation based on ECS velocity
        bool isMoving = math.lengthsq(move.Velocity) > 0.01f;
        _animator.SetBool("IsWalking", isMoving);
    }
}
```

### 3. The Mediator (`Scripts/Mediators/PlayerMediator.cs`)
The Mediator connects the pure View to the application. It can inject services from other modules.

```csharp
using Strada.Core.MVCS;

public class PlayerMediator : Mediator<PlayerView>
{
    // Injecting the interface from InputModule
    [Inject] public IInputService InputService { get; set; }

    // We can also inject the ECS EntityManager to spawn things
    [Inject] public EntityManager ECS { get; set; }

    public override void OnRegister()
    {
        // Setup initial state
        View.name = "Player [Linked]";
    }
    
    // Mediators can tick if they implement ITickable, usually for UI logic.
    // For gameplay, prefer ECS Systems.
}
```

### 4. The ECS System (`Scripts/Systems/PlayerMovementSystem.cs`)
This system runs the simulation. Notice it uses `IInputService` which is injected into the System!

```csharp
using Strada.Core.ECS;
using Unity.Mathematics;

public class PlayerMovementSystem : ISystem
{
    private readonly IInputService _input;

    // Dependencies are injected into Systems too!
    public PlayerMovementSystem(IInputService input)
    {
        _input = input;
    }

    public void Update(EntityManager em, float dt)
    {
        var inputDir = _input.Movement;
        var moveVec = new float3(inputDir.x, 0, inputDir.y);

        // Iterate all players
        em.ForEach<MovementData, PlayerTag>((int entity, ref MovementData data, ref PlayerTag tag) =>
        {
            // Update Velocity based on Input
            data.Velocity = moveVec * data.Speed;
            
            // (Position is typically updated in a separate TranslationSystem)
        });
    }
}
```

### 5. The Bootstrapper (`Scripts/PlayerModuleBootstrapper.cs`)
Configures the module.

```csharp
using Strada.Core.Modules;
using Strada.Core.DI;

public class PlayerModuleBootstrapper : IModuleBootstrapper
{
    public void Register(ContainerBuilder builder)
    {
        // Register Systems
        builder.Register<PlayerMovementSystem>(Lifetime.Singleton);
        
        // Register Mediators
        builder.RegisterMediator<PlayerView, PlayerMediator>();
    }
}
```

---

## 🔑 Key Takeaways

1.  **Zero Coupling:** `PlayerModule` knows about `IInputService` but **not** `UnityInputService`.
2.  **The Bridge:** `PlayerView` visualizes the state of the ECS `MovementData` component automatically.
3.  **Unified DI:** Services (`IInputService`) are injected into Mediators AND ECS Systems seamlessly.
4.  **Separation:** Logic lives in Systems, Data in Components, Visuals in Views.
