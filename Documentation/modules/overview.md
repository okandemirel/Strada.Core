# Module System

Strada enforces a modular architecture. A **Module** is a self-contained unit of functionality (e.g., Input, Physics, Inventory, Meta-Game).

## Directory Structure

We recommend the "Photon Quantum" style folder structure for every module:

```
MyGame.Modules.Input/
├── Scripts/
│   ├── Data/              # Serialized Data (Configs)
│   ├── Components/        # ECS Components (Structs)
│   ├── Systems/           # ECS Logic
│   ├── Views/             # Unity MonoBehaviours
│   ├── InputModule.cs     # Entry Point
│   └── InputInstaller.cs  # DI Registration
├── Tests/
└── MyGame.Modules.Input.asmdef
```

## Defining a Module

A Module implements `IModule`.

```csharp
public class InputModule : IModule
{
    [Inject] public InputSystem InputSystem { get; set; }
    
    public void Initialize()
    {
        InputSystem.Enable();
    }

    public void Tick()
    {
        InputSystem.Update();
    }
    
    public void Dispose()
    {
        InputSystem.Disable();
    }
}
```

## Module Communication

Modules **should not** reference each other directly. This causes spaghetti code.
Instead, communicate via:

1.  **Signals:** Publish a `PlayerJumpedSignal`. The Audio module listens and plays a sound. The Animation module listens and plays an animation.
2.  **Shared Interfaces:** Define `IInputService` in a Core/Shared assembly. Implement it in `InputModule`. Inject `IInputService` in `PlayerModule`.

## Config Data (ScriptableObjects)

Strada treats `ScriptableObjects` as **Configuration Containers** only. Runtime logic should never "live" in a ScriptableObject.

Use the `CD_` prefix for Config Data assets.

```csharp
[CreateAssetMenu(menuName = "Config/Input")]
public class CD_InputConfig : ScriptableObject
{
    public float DeadZone = 0.1f;
    public float Sensitivity = 1.5f;
}
```

Inject this config into your System:

```csharp
builder.RegisterInstance(myConfigAsset);
```
