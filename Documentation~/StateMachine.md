# State Machine

Strada provides a type-safe finite state machine with conditional transitions.

## Table of Contents

- [Quick Start](#quick-start)
- [States](#states)
- [Transitions](#transitions)
- [Context State Machines](#context-state-machines)
- [Integration Patterns](#integration-patterns)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.StateMachine;

// 1. Define states
public class IdleState : StateBase
{
    public override void OnEnter() => Debug.Log("Entering Idle");
    public override void OnUpdate(float dt) { }
    public override void OnExit() => Debug.Log("Exiting Idle");
}

public class WalkState : StateBase { /* ... */ }
public class AttackState : StateBase { /* ... */ }

// 2. Create state machine
var fsm = new StateMachine<IState>();

// 3. Add states
fsm.AddState(new IdleState());
fsm.AddState(new WalkState());
fsm.AddState(new AttackState());

// 4. Define transitions
fsm.AddTransition<IdleState, WalkState>(() => input.IsMoving);
fsm.AddTransition<WalkState, IdleState>(() => !input.IsMoving);
fsm.AddAnyTransition<AttackState>(() => input.IsAttacking);

// 5. Start and update
fsm.Start<IdleState>();

void Update()
{
    fsm.Update(Time.deltaTime);
}
```

---

## States

States define behavior for each phase of the state machine.

### IState Interface

```csharp
public interface IState
{
    void OnEnter();
    void OnUpdate(float deltaTime);
    void OnExit();
}
```

### StateBase

Convenience base class:

```csharp
public abstract class StateBase : IState
{
    public virtual void OnEnter() { }
    public abstract void OnUpdate(float deltaTime);
    public virtual void OnExit() { }
}
```

### Example States

```csharp
public class PatrolState : StateBase
{
    private int _waypointIndex;
    private Transform[] _waypoints;

    public PatrolState(Transform[] waypoints)
    {
        _waypoints = waypoints;
    }

    public override void OnEnter()
    {
        Debug.Log("Starting patrol");
    }

    public override void OnUpdate(float deltaTime)
    {
        // Move toward current waypoint
        var target = _waypoints[_waypointIndex].position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * deltaTime
        );

        // Check if reached waypoint
        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            _waypointIndex = (_waypointIndex + 1) % _waypoints.Length;
        }
    }

    public override void OnExit()
    {
        Debug.Log("Stopping patrol");
    }
}

public class ChaseState : StateBase
{
    private Transform _target;

    public void SetTarget(Transform target) => _target = target;

    public override void OnUpdate(float deltaTime)
    {
        if (_target == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            _target.position,
            chaseSpeed * deltaTime
        );
    }
}

public class DeadState : StateBase
{
    public override void OnEnter()
    {
        // Play death animation
        animator.SetTrigger("Die");
    }

    public override void OnUpdate(float deltaTime)
    {
        // Dead entities don't update
    }
}
```

---

## Transitions

Transitions define when to switch between states.

### State-Specific Transitions

From one specific state to another:

```csharp
// Only from Idle to Walk
fsm.AddTransition<IdleState, WalkState>(() => input.magnitude > 0.1f);

// Only from Walk to Run
fsm.AddTransition<WalkState, RunState>(() => input.IsSprinting);

// Only from Walk back to Idle
fsm.AddTransition<WalkState, IdleState>(() => input.magnitude < 0.1f);
```

### Any-State Transitions

From any state (highest priority):

```csharp
// Can attack from any state
fsm.AddAnyTransition<AttackState>(() => input.AttackPressed);

// Die from any state
fsm.AddAnyTransition<DeadState>(() => health <= 0);

// Pause from any state
fsm.AddAnyTransition<PausedState>(() => isPaused);
```

### Transition Priority

1. Any-state transitions checked first
2. Then state-specific transitions
3. First matching transition wins

```csharp
// Death always wins (checked first as any-transition)
fsm.AddAnyTransition<DeadState>(() => health <= 0);

// Then attack
fsm.AddAnyTransition<AttackState>(() => input.Attack);

// Then state-specific
fsm.AddTransition<IdleState, WalkState>(() => input.IsMoving);
```

### Manual State Changes

```csharp
// Force state change (ignores conditions)
fsm.SetState<DeadState>();

// Check current state
if (fsm.CurrentStateType == typeof(IdleState))
{
    // Currently idle
}

// Get current state instance
IState current = fsm.CurrentState;
```

### State Change Events

```csharp
fsm.OnStateChanged += (previous, next) =>
{
    Debug.Log($"State changed: {previous?.GetType().Name} -> {next.GetType().Name}");
};
```

---

## Context State Machines

State machines with shared context.

### IState with Context

```csharp
public interface IState<TContext> : IState
{
    void SetContext(TContext context);
}

public abstract class StateBase<TContext> : IState<TContext>
{
    protected TContext Context { get; private set; }

    public void SetContext(TContext context) => Context = context;

    public virtual void OnEnter() { }
    public abstract void OnUpdate(float deltaTime);
    public virtual void OnExit() { }
}
```

### Context Example

```csharp
// Shared context
public class EnemyContext
{
    public Transform Transform;
    public Transform Target;
    public float Health;
    public float Speed;
    public Animator Animator;
}

// States use context
public class ChaseState : StateBase<EnemyContext>
{
    public override void OnUpdate(float deltaTime)
    {
        Context.Transform.position = Vector3.MoveTowards(
            Context.Transform.position,
            Context.Target.position,
            Context.Speed * deltaTime
        );
    }
}

public class AttackState : StateBase<EnemyContext>
{
    public override void OnEnter()
    {
        Context.Animator.SetTrigger("Attack");
    }

    public override void OnUpdate(float deltaTime)
    {
        // Face target
        Context.Transform.LookAt(Context.Target);
    }
}

// Setup
var context = new EnemyContext
{
    Transform = transform,
    Target = player,
    Health = 100,
    Speed = 5f,
    Animator = GetComponent<Animator>()
};

var fsm = new StateMachine<IState<EnemyContext>, EnemyContext>(context);
fsm.AddState(new IdleState());
fsm.AddState(new ChaseState());
fsm.AddState(new AttackState());

// States automatically receive context
```

---

## Integration Patterns

### MonoBehaviour Integration

```csharp
public class EnemyAI : MonoBehaviour
{
    private StateMachine<IState> _fsm;

    void Awake()
    {
        _fsm = new StateMachine<IState>();

        _fsm.AddState(new PatrolState(waypoints));
        _fsm.AddState(new ChaseState());
        _fsm.AddState(new AttackState());
        _fsm.AddState(new DeadState());

        // Transitions
        _fsm.AddTransition<PatrolState, ChaseState>(() => CanSeePlayer());
        _fsm.AddTransition<ChaseState, AttackState>(() => InAttackRange());
        _fsm.AddTransition<ChaseState, PatrolState>(() => !CanSeePlayer());
        _fsm.AddTransition<AttackState, ChaseState>(() => !InAttackRange());
        _fsm.AddAnyTransition<DeadState>(() => health <= 0);
    }

    void Start()
    {
        _fsm.Start<PatrolState>();
    }

    void Update()
    {
        _fsm.Update(Time.deltaTime);
    }
}
```

### DI Integration

```csharp
public class PlayerStateMachine : SystemBase
{
    private StateMachine<IState> _fsm;
    private InputService _input;

    [Inject]
    public void Inject(InputService input)
    {
        _input = input;
        SetupFSM();
    }

    private void SetupFSM()
    {
        _fsm = new StateMachine<IState>();

        _fsm.AddState(new IdleState());
        _fsm.AddState(new MoveState());
        _fsm.AddState(new JumpState());

        _fsm.AddTransition<IdleState, MoveState>(() => _input.Movement.magnitude > 0);
        _fsm.AddTransition<MoveState, IdleState>(() => _input.Movement.magnitude == 0);
        _fsm.AddAnyTransition<JumpState>(() => _input.JumpPressed && IsGrounded());
    }

    protected override void OnUpdate(float deltaTime)
    {
        _fsm.Update(deltaTime);
    }
}
```

### Hierarchical States

```csharp
// Parent state with sub-machine
public class CombatState : StateBase
{
    private StateMachine<IState> _subFsm;

    public CombatState()
    {
        _subFsm = new StateMachine<IState>();
        _subFsm.AddState(new AttackState());
        _subFsm.AddState(new BlockState());
        _subFsm.AddState(new DodgeState());

        _subFsm.AddTransition<AttackState, BlockState>(() => input.Block);
        _subFsm.AddTransition<BlockState, AttackState>(() => input.Attack);
        _subFsm.AddAnyTransition<DodgeState>(() => input.Dodge);
    }

    public override void OnEnter()
    {
        _subFsm.Start<AttackState>();
    }

    public override void OnUpdate(float deltaTime)
    {
        _subFsm.Update(deltaTime);
    }

    public override void OnExit()
    {
        _subFsm.Stop();
    }
}
```

---

## Best Practices

### 1. Keep States Focused

```csharp
// Good - single responsibility
public class WalkState : StateBase
{
    public override void OnUpdate(float dt)
    {
        Move(dt);
    }
}

// Avoid - too much responsibility
public class WalkState : StateBase
{
    public override void OnUpdate(float dt)
    {
        Move(dt);
        PlayFootstepSounds();
        UpdateAnimator();
        CheckForEnemies();
        CollectItems();
    }
}
```

### 2. Use Any-Transitions for Global Events

```csharp
// Death can happen from any state
fsm.AddAnyTransition<DeadState>(() => health <= 0);

// Stun interrupts everything
fsm.AddAnyTransition<StunnedState>(() => isStunned);

// Pause affects all states
fsm.AddAnyTransition<PausedState>(() => GameManager.IsPaused);
```

### 3. Clean Up in OnExit

```csharp
public class ChargingState : StateBase
{
    private ParticleSystem _chargeEffect;

    public override void OnEnter()
    {
        _chargeEffect.Play();
    }

    public override void OnExit()
    {
        // Always clean up resources
        _chargeEffect.Stop();
    }
}
```

### 4. Use Context for Shared Data

```csharp
// Good - shared context
var fsm = new StateMachine<IState<PlayerContext>, PlayerContext>(context);

// Avoid - passing data through constructors
var idle = new IdleState(transform, animator, health, input, audio);
var walk = new WalkState(transform, animator, health, input, audio);
// Duplicated dependencies
```

### 5. Handle Edge Cases

```csharp
// Check for null target
public override void OnUpdate(float dt)
{
    if (Context.Target == null)
    {
        // Target lost - transition will handle this
        return;
    }
    // Normal update
}
```

---

## API Reference

### StateMachine<TState>

```csharp
StateMachine()

TState CurrentState { get; }
Type CurrentStateType { get; }
bool IsRunning { get; }

event Action<TState, TState> OnStateChanged;

void AddState<T>(T state) where T : TState;
void AddTransition<TFrom, TTo>(Func<bool> condition) where TFrom : TState where TTo : TState;
void AddAnyTransition<TTo>(Func<bool> condition) where TTo : TState;

void Start<T>() where T : TState;
void Update(float deltaTime);
void SetState<T>() where T : TState;
void Stop();
```

### StateMachine<TState, TContext>

```csharp
StateMachine(TContext context)

TState CurrentState { get; }
Type CurrentStateType { get; }
TContext Context { get; }
bool IsRunning { get; }

event Action<TState, TState> OnStateChanged;

void AddState<T>(T state) where T : TState;
void AddTransition<TFrom, TTo>(Func<bool> condition);
void AddAnyTransition<TTo>(Func<bool> condition);

void Start<T>() where T : TState;
void Update(float deltaTime);
void SetState<T>() where T : TState;
void Stop();
```

### IState

```csharp
void OnEnter();
void OnUpdate(float deltaTime);
void OnExit();
```

### IState<TContext>

```csharp
void SetContext(TContext context);
void OnEnter();
void OnUpdate(float deltaTime);
void OnExit();
```

---

## Related Documentation

- [ECS System](ECS.md) - Entity Component System
- [Messaging](Messaging.md) - Event system
- [Bridge](Bridge.md) - Reactive properties
