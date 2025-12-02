# Timer Service

Strada's TimerService provides managed timers with pooling, pause/resume support, and automatic cleanup.

## Table of Contents

- [Quick Start](#quick-start)
- [Creating Timers](#creating-timers)
- [TimerHandle](#timerhandle)
- [Timer Control](#timer-control)
- [Integration](#integration)
- [Performance](#performance)
- [Best Practices](#best-practices)
- [API Reference](#api-reference)

---

## Quick Start

```csharp
using Strada.Core.Services;

// Get service from DI container
var timerService = container.Resolve<TimerService>();

// One-shot timer (fires once after delay)
timerService.After(2.0f, () => Debug.Log("2 seconds passed!"));

// Repeating timer (fires every interval)
timerService.Every(1.0f, () => Debug.Log("Tick!"));

// Update in your game loop
void Update()
{
    timerService.Update(Time.deltaTime);
}
```

---

## Creating Timers

### One-Shot Timer (After)

Execute callback once after a delay:

```csharp
// Simple delayed action
timerService.After(3.0f, () =>
{
    SpawnEnemy();
});

// Store handle for later control
TimerHandle handle = timerService.After(5.0f, OnTimerComplete);
```

### Repeating Timer (Every)

Execute callback repeatedly at fixed intervals:

```csharp
// Infinite repeating (repeatCount = -1)
timerService.Every(0.5f, () =>
{
    UpdateScore();
});

// Limited repeats
timerService.Every(1.0f, () =>
{
    SpawnWave();
}, repeatCount: 5); // Fires 5 times then stops
```

### Custom Schedule

Full control over timing behavior:

```csharp
// Schedule(delay, interval, repeatCount, callback)

// Fire after 2s, then every 0.5s, 10 times total
timerService.Schedule(2.0f, 0.5f, 10, OnTick);

// Fire after 1s, then every 2s, forever
timerService.Schedule(1.0f, 2.0f, -1, OnTick);

// Same as After() - fire once after 3s
timerService.Schedule(3.0f, 0f, 1, OnComplete);
```

---

## TimerHandle

All timer creation methods return a `TimerHandle` for controlling the timer.

### Handle Properties

```csharp
TimerHandle handle = timerService.After(5.0f, OnComplete);

// Check if handle points to a valid timer
bool valid = handle.IsValid;

// Check if timer is still running (not cancelled/completed)
bool running = handle.IsActive;
```

### Handle as Struct

`TimerHandle` is a lightweight struct (12 bytes) containing:
- Reference to TimerService
- Timer ID for validation
- Index for O(1) lookup

```csharp
// Safe to store, copy, pass around
private TimerHandle _respawnTimer;

void StartRespawn()
{
    _respawnTimer = timerService.After(3.0f, Respawn);
}

void CancelRespawn()
{
    _respawnTimer.Cancel();
}
```

---

## Timer Control

### Cancel

Stop a timer before it completes:

```csharp
TimerHandle handle = timerService.After(10.0f, OnTimeout);

// Cancel via handle
handle.Cancel();

// Or cancel via service
timerService.Cancel(handle);
```

### Pause and Resume

Temporarily stop timer progression:

```csharp
TimerHandle handle = timerService.Every(1.0f, OnTick);

// Pause - timer stops counting down
handle.Pause();

// Resume - timer continues from where it paused
handle.Resume();
```

### Check Status

```csharp
if (handle.IsActive)
{
    Debug.Log("Timer is still running");
}

if (!handle.IsActive)
{
    Debug.Log("Timer completed or was cancelled");
}
```

### Cancel All

Remove all active timers:

```csharp
timerService.CancelAll();
```

---

## Integration

### Registration with DI

```csharp
// In ModuleConfig
protected override void Configure(IModuleBuilder builder)
{
    builder.RegisterService<TimerService>();
}
```

### Updating Timers

TimerService must be updated each frame:

```csharp
// Option 1: In MonoBehaviour
public class GameManager : MonoBehaviour
{
    private TimerService _timerService;

    void Start()
    {
        _timerService = GameBootstrapper.Services.Get<TimerService>();
    }

    void Update()
    {
        _timerService.Update(Time.deltaTime);
    }
}

// Option 2: In ECS System
public class TimerSystem : SystemBase
{
    private TimerService _timerService;

    protected override void OnInitialize()
    {
        _timerService = GameBootstrapper.Services.Get<TimerService>();
    }

    protected override void OnUpdate(float deltaTime)
    {
        _timerService.Update(deltaTime);
    }
}
```

### With Pause Menu

```csharp
public class GameController
{
    private readonly TimerService _timerService;
    private readonly List<TimerHandle> _gameTimers = new();

    public void PauseGame()
    {
        foreach (var handle in _gameTimers)
            handle.Pause();
    }

    public void ResumeGame()
    {
        foreach (var handle in _gameTimers)
            handle.Resume();
    }
}
```

### Cleanup on Scene Unload

```csharp
void OnDestroy()
{
    // Cancel timers when object is destroyed
    _attackTimer.Cancel();
    _respawnTimer.Cancel();

    // Or cancel all if shutting down
    _timerService.CancelAll();
}
```

---

## Performance

### Benchmarks

| Operation | Time |
|-----------|------|
| Create Timer | ~50ns |
| Cancel Timer | ~20ns |
| Update (per timer) | ~15ns |
| Pause/Resume | ~10ns |

### Memory

| Metric | Value |
|--------|-------|
| TimerHandle size | 12 bytes |
| TimerEntry size | ~64 bytes |
| Pool overhead | Minimal (reuses entries) |

### Pooling

TimerService uses `ObjectPool<TimerEntry>` internally:
- Timer entries are pooled and reused
- No allocation after warmup
- Automatic return on cancel/complete

```csharp
// Pre-warm if you know you'll need many timers
// (handled automatically, but can be done explicitly)
for (int i = 0; i < 100; i++)
{
    var h = timerService.After(999f, () => {});
    h.Cancel();
}
```

---

## Best Practices

### 1. Store Handles for Cancellable Timers

```csharp
// Good - can cancel later
private TimerHandle _cooldownTimer;

void StartCooldown()
{
    _cooldownTimer = timerService.After(2.0f, EndCooldown);
}

void OnInterrupt()
{
    _cooldownTimer.Cancel();
}

// Avoid - no way to cancel
void StartCooldown()
{
    timerService.After(2.0f, EndCooldown); // Handle lost!
}
```

### 2. Check IsActive Before Acting on Timer

```csharp
void Update()
{
    if (_chargeTimer.IsActive)
    {
        UpdateChargeVisuals();
    }
}
```

### 3. Cancel Timers on Object Destruction

```csharp
void OnDestroy()
{
    _attackTimer.Cancel();
    _respawnTimer.Cancel();
    _buffTimer.Cancel();
}
```

### 4. Use Every() for Repeated Actions

```csharp
// Good - single timer
timerService.Every(0.1f, UpdateDamageOverTime, repeatCount: 10);

// Avoid - creating new timer each time
void ApplyDamage()
{
    DealDamage();
    timerService.After(0.1f, ApplyDamage); // Creates new timer each tick
}
```

### 5. Pause Instead of Cancel for Resumable Timers

```csharp
// Good - can resume
void OnPause()
{
    _waveTimer.Pause();
}

void OnResume()
{
    _waveTimer.Resume();
}

// Avoid - loses progress
void OnPause()
{
    _remainingTime = GetRemainingTime(); // Complex!
    _waveTimer.Cancel();
}
```

---

## API Reference

### TimerService

```csharp
public sealed class TimerService : IService, IDisposable
{
    // Create one-shot timer
    TimerHandle After(float delay, Action callback);

    // Create repeating timer (-1 = infinite)
    TimerHandle Every(float interval, Action callback, int repeatCount = -1);

    // Create custom scheduled timer
    TimerHandle Schedule(float delay, float interval, int repeatCount, Action callback);

    // Update all timers (call every frame)
    void Update(float deltaTime);

    // Cancel specific timer
    void Cancel(int id, int index);

    // Pause specific timer
    void Pause(int id, int index);

    // Resume specific timer
    void Resume(int id, int index);

    // Check if timer is active
    bool IsActive(int id, int index);

    // Cancel all timers
    void CancelAll();

    // Dispose service
    void Dispose();
}
```

### TimerHandle

```csharp
public readonly struct TimerHandle
{
    // Is this handle pointing to a valid timer?
    bool IsValid { get; }

    // Is the timer still running?
    bool IsActive { get; }

    // Cancel the timer
    void Cancel();

    // Pause the timer
    void Pause();

    // Resume the timer
    void Resume();
}
```

---

## Common Patterns

### Delayed Spawn

```csharp
timerService.After(spawnDelay, () =>
{
    var enemy = enemyPool.Spawn();
    enemy.Position = spawnPoint;
});
```

### Countdown

```csharp
private int _countdown = 10;

void StartCountdown()
{
    _countdown = 10;
    timerService.Every(1.0f, () =>
    {
        _countdown--;
        UpdateCountdownUI(_countdown);
    }, repeatCount: 10);
}
```

### Damage Over Time

```csharp
public TimerHandle ApplyDOT(int entityId, int damagePerTick, float interval, int ticks)
{
    return timerService.Every(interval, () =>
    {
        DealDamage(entityId, damagePerTick);
    }, repeatCount: ticks);
}
```

### Ability Cooldown

```csharp
private TimerHandle _cooldownHandle;
private bool _canUseAbility = true;

public void UseAbility()
{
    if (!_canUseAbility) return;

    PerformAbility();
    _canUseAbility = false;

    _cooldownHandle = timerService.After(cooldownDuration, () =>
    {
        _canUseAbility = true;
        OnCooldownComplete();
    });
}
```

### Wave Spawner

```csharp
private TimerHandle _waveTimer;

void StartWaves()
{
    _waveTimer = timerService.Every(waveInterval, SpawnWave, repeatCount: totalWaves);
}

void SpawnWave()
{
    for (int i = 0; i < enemiesPerWave; i++)
    {
        timerService.After(i * 0.2f, () => SpawnEnemy());
    }
}
```

---

## Related Documentation

- [Pooling](Pooling.md) - Object pooling used internally
- [DI Container](DI.md) - Service registration
- [Modules](Modules.md) - Module configuration
