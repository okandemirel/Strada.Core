# Quick Start Guide

This guide will get you a working **Strada** scene in under 5 minutes.

## Prerequisites
*   Unity 2021.3 or later (Unity 6+ Recommended)
*   Nuget for Unity (optional, if using external libs)

## Step 1: Installation

1.  Open Unity Package Manager.
2.  Click "+" -> "Add package from disk".
3.  Select the `package.json` inside `com.strada.core`.

## Step 2: The Setup

Create a new Scene.
Create a GameObject named `[StradaContext]`.
Add the `StradaContext` component (or `ContextView` if you renamed it).

*This component acts as the Root for the Dependency Injection container.*

## Step 3: Create a Component

```csharp
using Strada.Core.ECS;
using Unity.Mathematics;

public struct PositionComponent : IComponent
{
    public float3 Value;
}
```

## Step 4: Create a System

```csharp
using Strada.Core.ECS;

public class MovementSystem : ISystem
{
    public void Update(EntityManager em, float dt)
    {
        // Iterate all entities with PositionComponent
        em.ForEach<PositionComponent>((int entity, ref PositionComponent pos) => 
        {
            pos.Value.y += 1.0f * dt; // Move up
        });
    }
}
```

## Step 5: Register Dependencies

Create a class `GameInstaller` that implements `IInstaller` (or extends `MonoInstaller`).

```csharp
public class GameInstaller : MonoInstaller
{
    public override void InstallBindings(ContainerBuilder builder)
    {
        // Register the System
        builder.Register<MovementSystem>(Lifetime.Singleton);
        
        // Register ECS World
        builder.RegisterInstance(new EntityManager());
    }
}
```

## Step 6: The Main Loop

In your entry point (e.g., `GameController`):

```csharp
public class GameController : MonoBehaviour
{
    [Inject] public MovementSystem Movement { get; set; }
    [Inject] public EntityManager Entities { get; set; }

    void Start()
    {
        // Create a test entity
        var e = Entities.CreateEntity();
        Entities.AddComponent(e, new PositionComponent { Value = float3.zero });
    }

    void Update()
    {
        Movement.Update(Entities, Time.deltaTime);
    }
}
```

*Note: In a real module, the `Bootstrap` class handles the Update loop automatically.*
