# The Unified Architecture (MECS)

**MECS** stands for **Model-Entity-Controller-Service**. It is the evolution of the classic MVCS pattern, designed specifically for modern game development where performance matters.

## The Problem
In classic MVCS (StrangeIoC, RobotLegs), everything is a `View` or a `Controller`. This works for UI but falls apart for gameplay. 10,000 enemies each having a `MonoBehaviour` View and a `Controller` instance will destroy your framerate.

## The Strada Solution
Strada splits the responsibility:

1.  **Macro Logic (MVCS):** UI, Input, Network, Game State, Inventory.
    *   *Managed memory, easy to write, event-driven.*
2.  **Micro Logic (ECS):** Physics, Movement, Collision, AI Swarms.
    *   *Data-oriented, cache-friendly, extremely fast.*

## The Bridge 🌉

The magic of Strada is the **Bridge**. You don't need to manually sync these two worlds.

### EntityView
An `EntityView` is a MonoBehaviour that automatically binds to an ECS Entity.

```csharp
public class EnemyView : EntityView
{
    // Automatically called when the 'Health' component on the linked Entity changes
    [BindComponent]
    public void OnHealthChanged(ref Health health)
    {
        _healthBar.fillAmount = health.Current / health.Max;
    }
}
```

### EntityMediator
Mediators sit between Views and the rest of the application. In Strada, they can also listen to ECS Groups.

```csharp
public class EnemyMediator : Mediator<EnemyView>
{
    [Inject] public IScoreModel ScoreModel { get; set; }

    public override void OnRegister()
    {
        // Listen to ECS events via the Bridge
        Bind<EnemyDeathSignal>(OnEnemyDied);
    }

    private void OnEnemyDied(EnemyDeathSignal signal)
    {
        ScoreModel.AddPoints(100);
    }
}
```

## Architectural Diagram

```mermaid
graph TD
    subgraph MVCS [Macro Logic / UI]
        View[Unity View (MonoBehaviour)]
        Mediator[Mediator]
        Model[Data Model]
        Service[External Service]
        Command[Command]
    end

    subgraph ECS [Micro Logic / Simulation]
        Entity[Entity]
        Component[Component Data]
        System[System (Logic)]
        Group[Group / Archetype]
    end

    %% The Bridge
    View <-->|Data Binding| Component
    Mediator -->|Dispatch| Command
    System -->|Signals| Mediator
    Command -->|Modify| Component

    classDef mvcs fill:#2d3e50,stroke:#fff,stroke-width:2px;
    classDef ecs fill:#1a5c38,stroke:#5ce68a,stroke-width:2px;
    
    class View,Mediator,Model,Service,Command mvcs;
    class Entity,Component,System,Group ecs;
```

## Key Concepts

### 1. Models are for Global State
Use `StradaModel` for things that exist once: User Profile, Settings, Active Level.

### 2. Components are for Simulation State
Use `IComponent` for things that exist in thousands: Position, Velocity, Health, Ammo.

### 3. Groups are States
Inspired by Svelto.ECS, Strada uses **Groups** to define entity states. An entity isn't just "Alive" or "Dead" via a boolean flag; it moves from the `AliveGroup` to the `DeadGroup`. This makes queries lightning fast because you never iterate over dead entities when processing movement.

### 4. Systems are Logic
`ISystem` classes iterate over Groups of Components. They are stateless logic processors.
