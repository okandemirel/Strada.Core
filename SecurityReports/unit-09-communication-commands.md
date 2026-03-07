# Unit 9 — Communication & Commands: Security Review

## Executive Summary

The Communication and Commands subsystem provides an `EventBus` (signal/query/event dispatch), `SignalSequence` (composable signal chains), and command interfaces. The architecture uses struct-based messages with static type IDs for fast dispatch, copy-on-write arrays for event channels, and lock-based synchronization for registration.

The review identified **1 HIGH**, **3 MEDIUM**, and **3 LOW** severity findings. The most significant issues are: a race condition between dispatch and handler registration that can cause `InvalidCastException` or stale reads; missing exception isolation in event fan-out allowing one handler to abort delivery to subsequent subscribers; and a synchronous `.Wait()` call on an async path that risks deadlocks in single-threaded Unity contexts.

---

## Detailed Findings

### FIND-09-01: Race Condition Between Dispatch and Registration (Thread Safety)

| Field | Value |
|-------|-------|
| **Severity** | HIGH |
| **File** | `Runtime/Communication/EventBus.cs` |
| **Lines** | 79-88, 116-124, 230-240 |

**Description:**
`Send<TSignal>` reads `_signalHandlers` without any lock or memory barrier, while `RegisterSignalHandler` writes under `_lock`. This is a classic torn-read / stale-reference pattern. The `EnsureCapacity` method creates a new array and reassigns it, but the dispatching thread may hold a reference to the old array indefinitely.

On relaxed-memory architectures (ARM, used by mobile Unity targets), the lack of a `volatile` read or `Interlocked.CompareExchange` means the dispatch thread can observe a partially-initialized array or never see the new array at all.

The same pattern applies to `Publish`, `SendAsync`, `QueryAsync`, and `Query`.

**Impact:**
- Handler invoked from stale array misses newly registered handlers.
- If the array reference is torn, an `IndexOutOfRangeException` or `NullReferenceException` can crash the dispatch path.
- `InvalidCastException` if a handler slot is read mid-update.

**Recommendation:**
Mark the handler arrays as `volatile` or use `Volatile.Read` / `Volatile.Write` when accessing them from unlocked paths. Alternatively, use `Interlocked.Exchange` in `EnsureCapacity`.

---

### FIND-09-02: No Exception Isolation in Event Fan-Out

| Field | Value |
|-------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/Communication/EventBus.cs` |
| **Lines** | 343-349 |

**Description:**
`EventChannel<T>.Publish` iterates through all handlers and invokes them without a `try/catch`:

```csharp
for (int i = 0; i < handlers.Length; i++)
    handlers[i](message);
```

If any handler throws, all subsequent handlers in the array are skipped. This applies to both `Publish` and `Send` (though `Send` has a single handler, so it is less affected).

**Impact:**
A single misbehaving subscriber can prevent all other subscribers from receiving events, leading to inconsistent game state. In a game framework, this can silently break systems that depend on event delivery.

**Recommendation:**
Wrap each handler invocation in a `try/catch` block and log exceptions. Consider an `AggregateException` collection pattern or a configurable error-handling strategy.

---

### FIND-09-03: Synchronous `.Wait()` on Async Path Risks Deadlock

| Field | Value |
|-------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/Communication/SignalSequence.cs` |
| **Lines** | 305-308 |

**Description:**
`AsyncActionEntry.Execute` calls `.AsTask().Wait()` to synchronously block on an async action:

```csharp
public void Execute(IEventBus defaultBus)
{
    _asyncAction?.Invoke(CancellationToken.None).AsTask().Wait();
}
```

In Unity's single-threaded synchronization context, if the async action posts back to the main thread, this will deadlock permanently.

**Impact:**
Calling `SignalSequence.Execute()` on a sequence that contains async entries will deadlock on Unity's main thread. Since `Execute()` and `ExecuteAsync()` are both public, callers may not realize they must use the async variant when async entries exist.

**Recommendation:**
Either throw `InvalidOperationException` in the synchronous path if async entries are present, or use `ConfigureAwait(false)` and document the constraint. A compile-time or runtime guard would be the safest approach.

---

### FIND-09-04: Handler Memory Leak — No Lifecycle-Aware Unsubscribe

| Field | Value |
|-------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/Communication/EventBus.cs` |
| **Lines** | 132-146, 166-184 |

**Description:**
Signal handlers are stored as `Action<TSignal>` delegates, and event subscribers are stored in `EventChannel<T>`. There is no weak-reference mechanism, no token-based unsubscription (returning `IDisposable`), and no automatic cleanup when a Unity `MonoBehaviour` or `GameObject` is destroyed.

While `Unsubscribe` exists for events, signals and queries have no unregister API at all — the only way to remove them is `Clear()` or `Dispose()`, which removes everything.

**Impact:**
If a `MonoBehaviour` registers a signal handler and is then destroyed, the delegate keeps the `MonoBehaviour` alive in managed memory (preventing GC), and the handler will be invoked on a destroyed object, causing `MissingReferenceException` at runtime.

**Recommendation:**
- Return `IDisposable` subscription tokens from all registration methods.
- Consider adding `UnregisterSignalHandler<T>()` and `UnregisterQueryHandler<T>()` methods.
- Optionally support weak-reference handlers or integration with Unity's `OnDestroy` lifecycle.

---

### FIND-09-05: Static Type ID Counters Grow Monotonically Across Application Lifetime

| Field | Value |
|-------|-------|
| **Severity** | LOW |
| **File** | `Runtime/Communication/EventBus.cs` |
| **Lines** | 58-62, 294-331 |

**Description:**
The static type ID classes (`SignalTypeId<T>`, `QueryTypeId<T>`, etc.) use `Interlocked.Increment` on static counters. These IDs start at 1 (since `Increment` returns the incremented value) and grow forever. The handler arrays start at size 64 and double as needed.

If many distinct generic type arguments are used across the application lifetime (e.g., via code generation or reflection), the arrays will grow unboundedly. Since these are `static` fields, they survive `EventBus.Dispose()` and will cause the next `EventBus` instance to allocate larger arrays.

**Impact:**
Unlikely to be an issue in practice, but in long-running sessions with hot-reloading or many generated types, memory usage grows without bound.

**Recommendation:**
Document the expected upper bound of distinct message types. Consider a diagnostic warning if the ID exceeds a threshold (e.g., 256).

---

### FIND-09-06: `ExecuteAsync` on EventBus Bypasses Handler Registration

| Field | Value |
|-------|-------|
| **Severity** | LOW |
| **File** | `Runtime/Communication/EventBus.cs` |
| **Lines** | 289-292 |

**Description:**
`EventBus.ExecuteAsync(IAsyncAwaitCommand)` directly calls `command.ExecuteAsync()` without any validation, logging, or interception:

```csharp
public ValueTask ExecuteAsync(IAsyncAwaitCommand command, CancellationToken cancellationToken = default)
{
    return command.ExecuteAsync(cancellationToken);
}
```

This means any `IAsyncAwaitCommand` can be executed through the bus without registration, authorization, or auditing. The method is essentially a pass-through that adds no value and no security controls.

**Impact:**
Any code with a reference to the `IEventBus` can execute arbitrary async commands. In a modular game architecture, this bypasses any command registration or permission model that might be expected.

**Recommendation:**
Either remove this method (callers can invoke commands directly) or add a command registration/validation step. At minimum, add a null check for the `command` parameter.

---

### FIND-09-07: `SignalSequence.Include` Self-Reference Guard Is Incomplete

| Field | Value |
|-------|-------|
| **Severity** | LOW |
| **File** | `Runtime/Communication/SignalSequence.cs` |
| **Lines** | 60-67 |

**Description:**
`Include` guards against direct self-reference (`other != this`) but does not detect indirect cycles (A includes B, B includes A). Executing a cyclic sequence will cause a `StackOverflowException`.

**Impact:**
A `StackOverflowException` will crash the Unity process without a catchable exception. This is a denial-of-service risk if sequences are constructed from external data.

**Recommendation:**
Add cycle detection via a visited set during `Execute`/`ExecuteAsync`, or limit recursion depth.

---

## Summary Table

| ID | Finding | Severity | File | Recommendation |
|----|---------|----------|------|----------------|
| FIND-09-01 | Race condition in dispatch vs. registration | HIGH | EventBus.cs | Use `volatile` / `Volatile.Read` for handler arrays |
| FIND-09-02 | No exception isolation in event fan-out | MEDIUM | EventBus.cs | Wrap handler calls in try/catch |
| FIND-09-03 | `.Wait()` on async path risks deadlock | MEDIUM | SignalSequence.cs | Throw or guard against sync execution of async entries |
| FIND-09-04 | No lifecycle-aware unsubscribe for signals/queries | MEDIUM | EventBus.cs | Return IDisposable tokens; add unregister methods |
| FIND-09-05 | Static type IDs grow without bound | LOW | EventBus.cs | Document limits; add diagnostic threshold |
| FIND-09-06 | Command execution bypasses registration | LOW | EventBus.cs | Add validation or remove pass-through |
| FIND-09-07 | Incomplete cycle detection in SignalSequence | LOW | SignalSequence.cs | Add visited-set cycle detection |
