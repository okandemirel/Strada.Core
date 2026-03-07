# Security Review: Unit 6 — ECS Reactive & World

**Reviewer:** Claude (automated)
**Date:** 2026-03-07
**Scope:** `Runtime/ECS/Reactive/`, `Runtime/ECS/World/`
**Files Reviewed:**
- `Runtime/ECS/Reactive/ReactiveComponentStorage.cs`
- `Runtime/ECS/Reactive/ReactiveEntityManager.cs`
- `Runtime/ECS/World/ECSBuilder.cs`
- `Runtime/ECS/World/UpdatePhase.cs`
- `Runtime/ECS/World/SystemScheduler.cs`
- `Runtime/ECS/World/World.cs`

---

## Executive Summary

The ECS Reactive and World subsystems provide event-driven component change tracking and a world lifecycle manager. The code is generally well-structured with proper `IDisposable` implementations. However, several issues were identified: callback lists in the reactive storage can be modified during iteration causing exceptions, unhandled exceptions in callbacks can break notification chains, the global mutable `World.Current` static introduces race conditions, and missing thread-safety across all reactive operations could cause corruption in multi-threaded scenarios.

---

## Detailed Findings

### Finding 1: Callback Exception Breaks Notification Chain (MEDIUM)

**File:** `Runtime/ECS/Reactive/ReactiveComponentStorage.cs` (lines 86-102)
**Category:** Denial of Service / Reliability

The `NotifyAdd`, `NotifyRemove`, and `NotifyChange` methods iterate over callback lists and invoke each delegate without any exception handling. If any callback throws, subsequent callbacks in the list are never invoked.

```csharp
private void NotifyAdd(int entityIndex, T component)
{
    foreach (var callback in _onAddCallbacks)
        callback(entityIndex, component);
}
```

A single faulty callback prevents all later-registered callbacks from executing. In a game framework, this can silently break systems that depend on reactive notifications.

**Recommendation:** Wrap each callback invocation in a try-catch and log exceptions, or use an invocation pattern that guarantees all callbacks execute.

---

### Finding 2: Collection Modified During Enumeration (MEDIUM)

**File:** `Runtime/ECS/Reactive/ReactiveComponentStorage.cs` (lines 86-102)
**Category:** Denial of Service

If a callback subscribes or unsubscribes another callback during notification (e.g., an `OnAdd` handler calls `SubscribeOnAdd` or `UnsubscribeOnAdd`), a `System.InvalidOperationException` ("Collection was modified during enumeration") will be thrown, crashing the notification pipeline.

This is a realistic scenario: a system reacting to an entity addition may want to register or tear down further listeners.

**Recommendation:** Iterate over a snapshot of the callback list (e.g., copy to a temporary array), or use a deferred add/remove queue that is processed after iteration completes.

---

### Finding 3: Unbounded Recursive Event Chains (MEDIUM)

**File:** `Runtime/ECS/Reactive/ReactiveComponentStorage.cs` (lines 43-50, 64-75)
**Category:** Denial of Service

Callbacks triggered by `Add` or `Set` can themselves call `Add`, `Set`, or `Remove` on the same or other reactive storages, producing unbounded recursive notification chains. This can exhaust the call stack, causing a `StackOverflowException`.

For example, an `OnAdd` callback that conditionally adds another component to the same entity would trigger another `OnAdd` cycle.

**Recommendation:** Add a re-entrancy guard (e.g., a depth counter) and either throw or log when recursion exceeds a safe threshold.

---

### Finding 4: Global Mutable Static `World.Current` (MEDIUM)

**File:** `Runtime/ECS/World/World.cs` (lines 9, 21-25)
**Category:** Race Condition / Information Disclosure

`World.Current` is a publicly settable static field with no synchronization. In scenarios involving multiple worlds (e.g., editor tooling, play-mode tests, server/client split), any code can overwrite the global reference, causing other code to operate on the wrong world instance. This can lead to data corruption or information disclosure across world boundaries.

```csharp
public static World Current
{
    get => _current;
    set => _current = value;
}
```

**Recommendation:** Remove the public setter, or scope world access through explicit dependency injection. If a global accessor is required, use thread-local storage or make the field `volatile` at minimum.

---

### Finding 5: No Thread Safety on Reactive Operations (MEDIUM)

**File:** `Runtime/ECS/Reactive/ReactiveComponentStorage.cs` (entire file)
**Category:** Race Condition

All subscribe, unsubscribe, add, remove, and set operations modify shared `List<T>` instances without synchronization. If reactive storage is accessed from multiple threads (e.g., job system workers or async operations), list corruption or lost updates can occur. The `[MethodImpl(MethodImplOptions.AggressiveInlining)]` attributes suggest these are performance-sensitive hot paths, but do not provide thread safety.

**Recommendation:** Document single-threaded usage requirements clearly. If multi-threaded access is expected, use `ConcurrentBag` or explicit locking.

---

### Finding 6: `DestroyEntity` Does Not Clean Up Reactive Components (MEDIUM)

**File:** `Runtime/ECS/Reactive/ReactiveEntityManager.cs` (lines 40-41)
**Category:** Resource Leak

`DestroyEntity` delegates directly to the underlying `EntityManager.DestroyEntity` without removing the entity's components from any reactive storages. This means:
1. `OnRemove` callbacks are never fired for destroyed entities.
2. Component data remains in reactive storages, leaking memory and producing stale query results.

```csharp
public void DestroyEntity(Entity entity) => _entityManager.DestroyEntity(entity);
```

**Recommendation:** Iterate over all reactive storages and call `Remove` for the destroyed entity's index before delegating to the entity manager, ensuring `OnRemove` callbacks fire and storage is cleaned up.

---

### Finding 7: Memory Leak from Unsubscribed Handlers (LOW)

**File:** `Runtime/ECS/Reactive/ReactiveComponentStorage.cs` (lines 25-40)
**Category:** Memory Leak

Subscribe methods accept `Action<>` delegates but there is no mechanism to enforce or verify that subscribers eventually unsubscribe. If a subscribing object is destroyed or goes out of scope without calling the corresponding `Unsubscribe` method, the delegate reference keeps the subscriber alive, preventing garbage collection.

This is particularly relevant in Unity where MonoBehaviours may be destroyed without explicit cleanup.

**Recommendation:** Consider a weak-reference based subscription model, or provide a `RemoveAllCallbacks` method and document the cleanup contract. Alternatively, return an `IDisposable` subscription token that auto-unsubscribes.

---

### Finding 8: `ECSBuilder` Does Not Validate Duplicate System Registration (LOW)

**File:** `Runtime/ECS/World/ECSBuilder.cs` (lines 26-36)
**Category:** Denial of Service

`WithSystem<T>` does not check whether a system of the same type has already been registered. Accidentally registering the same system twice causes it to execute twice per frame, doubling CPU cost and potentially causing logic bugs from double-processing.

**Recommendation:** Track registered system types and throw or warn on duplicates.

---

### Finding 9: `_initialEntityCapacity` Is Set But Never Used (INFO)

**File:** `Runtime/ECS/World/ECSBuilder.cs` (lines 11, 14-18, 40)
**Category:** Dead Code

The `_initialEntityCapacity` field is set by `WithInitialEntityCapacity` but is never passed to the `EntityManager` constructor in `Build()`. This means the API promises configurable capacity but silently ignores it.

```csharp
var entities = new EntityManager(); // _initialEntityCapacity not used
```

**Recommendation:** Pass `_initialEntityCapacity` to the `EntityManager` constructor, or remove the unused API to avoid misleading callers.

---

### Finding 10: `SystemScheduler.AddSystem` Allows Addition After Initialization (LOW)

**File:** `Runtime/ECS/World/SystemScheduler.cs` (lines 22-26)
**Category:** Reliability

`AddSystem` does not check whether the scheduler has already been initialized. Systems added after `Initialize()` is called will never have their `Initialize()` method invoked, leading to undefined behavior or crashes when they run.

**Recommendation:** Either throw if `AddSystem` is called after initialization, or automatically initialize newly added systems.

---

## Summary Table

| # | Finding | Severity | Category | File |
|---|---------|----------|----------|------|
| 1 | Callback exception breaks notification chain | MEDIUM | Denial of Service | ReactiveComponentStorage.cs |
| 2 | Collection modified during enumeration | MEDIUM | Denial of Service | ReactiveComponentStorage.cs |
| 3 | Unbounded recursive event chains | MEDIUM | Denial of Service | ReactiveComponentStorage.cs |
| 4 | Global mutable static `World.Current` | MEDIUM | Race Condition | World.cs |
| 5 | No thread safety on reactive operations | MEDIUM | Race Condition | ReactiveComponentStorage.cs |
| 6 | `DestroyEntity` does not clean up reactive components | MEDIUM | Resource Leak | ReactiveEntityManager.cs |
| 7 | Memory leak from unsubscribed handlers | LOW | Memory Leak | ReactiveComponentStorage.cs |
| 8 | No duplicate system registration check | LOW | Denial of Service | ECSBuilder.cs |
| 9 | `_initialEntityCapacity` set but never used | INFO | Dead Code | ECSBuilder.cs |
| 10 | `AddSystem` allowed after initialization | LOW | Reliability | SystemScheduler.cs |
