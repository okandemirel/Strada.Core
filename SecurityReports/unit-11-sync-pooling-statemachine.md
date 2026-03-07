# Unit 11 Security Review: Sync, Pooling, StateMachine & Data

**Review Date:** 2026-03-07
**Reviewer:** Claude (Automated Security Analysis)
**Scope:** `Runtime/Sync/`, `Runtime/Pooling/`, `Runtime/StateMachine/`, `Runtime/Data/`

---

## Executive Summary

This review covers the reactive property system, entity-view synchronization layer, object pooling infrastructure, finite state machine, and data/config management. The code is generally well-structured with proper disposal patterns and guard checks. However, several findings were identified: a notable risk of resource leaks from reactive subscriptions that are not disposed through a `BindingScope`, thread safety gaps in the reactive property system, potential stale data exposure from pooled objects, and missing transition validation in the state machine. No critical vulnerabilities were found; the most impactful issues are at the MEDIUM level.

---

## Detailed Findings

### SYNC-01: ReactiveProperty Notify() Can Throw Mid-Iteration on Handler List Mutation

**Severity:** MEDIUM
**File:** `Runtime/Sync/ReactiveProperty.cs`, lines 83-90

`Notify()` iterates `_handlers` by index with a cached count. If a handler callback modifies the handler list (e.g., calls `Subscribe` or `Unsubscribe` on the same property), the iteration will either skip handlers, invoke stale delegates, or throw an `IndexOutOfRangeException`. The same pattern appears in `ReactiveCollection` notification methods (lines 155-170) and throughout `ReactiveExtensions.cs` (e.g., `MappedProperty`, `FilteredProperty`, `CombinedProperty`).

**Recommendation:** Snapshot the handler list before iterating, or use a copy-on-write strategy. Alternatively, defer subscribe/unsubscribe operations until after notification completes.

---

### SYNC-02: Reactive Subscriptions Without BindingScope Cause Memory Leaks

**Severity:** MEDIUM
**Files:** `Runtime/Sync/ReactiveProperty.cs`, `Runtime/Sync/ReactiveExtensions.cs`

`ReactiveProperty.Subscribe()` adds a handler to an internal list but provides no automatic lifecycle management. If callers subscribe without using a `BindingScope` or manually calling `Unsubscribe`, the handler (and any objects it captures in a closure) will remain referenced indefinitely. This is particularly dangerous for `MappedProperty`, `FilteredProperty`, `CombinedProperty`, `ThrottledProperty`, `DistinctProperty`, `PropertyBinding`, and `ConvertedBinding` -- all of which subscribe to a source in their constructor. If these derived properties are created via the extension methods (`Select`, `Where`, `CombineLatest`, etc.) outside a `BindingScope` and the caller forgets to call `Dispose()`, the subscription leaks permanently.

**Recommendation:** Document the ownership contract clearly. Consider adding a finalizer-based safety net that logs warnings for undisposed reactive properties that still have subscriptions.

---

### SYNC-03: ReactiveCollection Has No Unsubscribe Mechanism

**Severity:** MEDIUM
**File:** `Runtime/Sync/ReactiveProperty.cs`, lines 107-182

`ReactiveCollection<T>` provides `OnAdd`, `OnRemove`, and `OnClear` subscription methods but offers no way to unsubscribe individual handlers. The only way to remove handlers is via `Dispose()`, which clears everything. This makes it impossible to selectively remove a single listener without disposing the entire collection.

**Recommendation:** Add `OffAdd`, `OffRemove`, `OffClear` methods that mirror the subscription methods, or return a disposable subscription handle from the `On*` methods.

---

### SYNC-04: TwoWayBinding Reentrancy Guard Not Exception-Safe

**Severity:** LOW
**File:** `Runtime/Sync/BindingScope.cs`, lines 142-188

`TwoWayBinding<T>` uses a boolean `_updating` flag to prevent infinite recursion. However, if the target property's setter throws an exception, `_updating` is never reset to `false` (lines 167-169), permanently disabling the binding for future updates. The same issue exists in `TwoWayBinding<TSource, TTarget>` and `ValidatedBinding<T>`.

**Recommendation:** Wrap the update in a try/finally block to ensure `_updating` is reset even on exception.

---

### SYNC-05: EntityView.SyncBindings Skips Non-Dirty Bindings, Potentially Missing Updates

**Severity:** LOW
**File:** `Runtime/Sync/EntityView.cs`, lines 92-101

`SyncBindings()` only calls `Sync()` on bindings where `IsDirty` is true. However, the `ComponentBinding<T>` in `EntityView.cs` (line 207) only becomes dirty via explicit `MarkDirty()` calls -- there is no automatic dirty detection from ECS component changes. This means bindings may silently miss ECS updates unless `ForceSyncBindings()` is used or `MarkDirty()` is called externally. The `ViewSyncRunner` compensates by calling `ForceSyncAll()`, but if a view is synced through `ViewSyncSystem` instead, it calls `SyncBindings()` (dirty-only), creating inconsistent behavior depending on which sync path is active.

**Recommendation:** Unify the sync strategy. Either always force-sync (since ECS components are cheap to read) or implement proper dirty tracking triggered by ECS write events.

---

### SYNC-06: MediatorPool Has No Size Limit

**Severity:** LOW
**File:** `Runtime/Sync/MediatorRegistry.cs`, lines 89-119

`MediatorPool<TMediator, TView>` uses a static `Stack` with no maximum size. If mediators are created and released in a burst pattern (e.g., spawning and despawning many entities), the pool will grow to the peak count and never shrink, holding memory indefinitely. The pool is also `static`, meaning it persists across scene loads and is never cleared.

**Recommendation:** Add a configurable maximum pool size and/or a periodic trim mechanism. Consider clearing the static pool on domain reload or scene transitions.

---

### SYNC-07: MediatorRegistry.ReleaseAll Calls Dispose Instead of Unbind

**Severity:** LOW
**File:** `Runtime/Sync/MediatorRegistry.cs`, lines 67-74

`ReleaseAll()` calls `Dispose()` on each active mediator. However, `EntityMediator.Dispose()` sets `_disposed = true`, which means the mediator cannot be reused even though it was obtained from a pool. The `Release<TMediator, TView>` method correctly calls `Unbind()` instead. Mediators disposed via `ReleaseAll()` will be unusable if returned to the pool later.

**Recommendation:** `ReleaseAll()` should call the proper `Release` flow (unbind + return to pool) rather than `Dispose()`, or the pool should not accept disposed mediators.

---

### SYNC-08: EntityHandleRegistry._nextHandleId Integer Overflow

**Severity:** LOW
**File:** `Runtime/Sync/EntityHandleRegistry.cs`, lines 9, 17-21

`_nextHandleId` is an `int` that starts at 1 and increments on every `Register` call. In a very long-running session, this could overflow to negative values or zero, producing `EntityHandle` instances where `IsValid` returns `false` (since validity checks `_id > 0`). While unlikely in typical game sessions, server-authoritative or persistent-world scenarios could trigger this.

**Recommendation:** Use a `uint` or `long` for the handle ID, or add overflow detection with a wrap-around strategy.

---

### POOL-01: ObjectPool Does Not Reset State on Reuse

**Severity:** MEDIUM
**File:** `Runtime/Pooling/ObjectPool.cs`, lines 36-58

When `Spawn()` reuses a pooled object, it calls `_onSpawn` and `IPoolable.OnSpawn()`. However, there is no enforced or built-in state reset mechanism. If a pooled object's `OnSpawn()` implementation does not fully reinitialize all fields, stale data from the previous use will leak into the new use. This is a classic pool reuse bug that can cause hard-to-diagnose gameplay issues (e.g., enemies retaining health from a previous spawn, projectiles keeping old trajectory data).

The same concern applies to `ViewPool<TView>` in `Runtime/Sync/ViewPool.cs` -- when a view is respawned, it is re-bound to a new entity via `Bind()`, but any MonoBehaviour state on the GameObject that is not explicitly reset in `OnBind()` will persist.

**Recommendation:** Add a required `Reset()` method to `IPoolable` or invoke the reset callback before the spawn callback. Consider adding a debug-mode validation that checks for field modifications between despawn and the next spawn.

---

### POOL-02: ObjectPool.ActiveCount Calculation Is Inaccurate

**Severity:** LOW
**File:** `Runtime/Pooling/ObjectPool.cs`, line 19

`ActiveCount` is computed as `_totalCreated - _available.Count`. However, `Clear()` resets `_totalCreated` to 0 while active instances remain in use outside the pool. After `Clear()`, `ActiveCount` becomes negative, which is semantically incorrect and could cause issues for callers that rely on this value for capacity planning or diagnostics.

**Recommendation:** Track active count independently using an increment on `Spawn` and decrement on `Despawn`.

---

### POOL-03: Double-Despawn Not Prevented in ObjectPool

**Severity:** LOW
**File:** `Runtime/Pooling/ObjectPool.cs`, lines 61-73

`Despawn()` does not check whether the instance is already in the available pool. If a caller despawns the same object twice, it will be pushed onto the stack twice, leading to the same instance being spawned to two different consumers simultaneously. This causes shared mutable state corruption.

**Recommendation:** Add a `HashSet<T>` of pooled instances to detect and reject double-despawn, at least in debug builds.

---

### POOL-04: PooledObject.Dispose Calls ReturnToPool Creating Misleading Semantics

**Severity:** INFO
**File:** `Runtime/Pooling/PooledObject.cs`, lines 24-27

`PooledObject<T>.Dispose()` calls `ReturnToPool()`, which returns the object to the pool rather than truly disposing it. This violates the `IDisposable` contract: a disposed object should not be reused. Callers using `using` statements will return the object to the pool, which is useful but semantically surprising.

**Recommendation:** Document this behavior clearly, or rename the method to avoid confusion with standard `IDisposable` semantics.

---

### FSM-01: State Machine Allows Transitions to Unregistered States (Silent Failure)

**Severity:** MEDIUM
**File:** `Runtime/StateMachine/StateMachine.cs`, lines 75-92

`SetState(Type stateType)` silently returns without any error or warning if the target state type has not been registered via `AddState`. This means misconfigured transitions (e.g., due to a typo in the generic type parameter, or a forgotten `AddState` call) will silently fail, leaving the FSM in its current state with no indication of the error. This applies to both `StateMachine<TState>` and `StateMachine<TState, TContext>`.

**Recommendation:** Log a warning or throw an exception when a transition targets an unregistered state. At minimum, provide a debug-mode validation pass that verifies all transition targets have been registered.

---

### FSM-02: OnStateChanged Event Fires During Transition (Reentrancy Risk)

**Severity:** MEDIUM
**File:** `Runtime/StateMachine/StateMachine.cs`, lines 80-91

`SetState` sets `_isTransitioning = false` on line 89 before firing `OnStateChanged` on line 91. If an `OnStateChanged` handler calls `SetState` again, it will succeed (since `_isTransitioning` is already false), causing a recursive state transition. This can lead to stack overflows, unexpected state sequences, or the FSM ending in an unintended state.

**Recommendation:** Keep `_isTransitioning = true` until after the event fires, or queue state changes triggered during transitions to be applied on the next `Update` call.

---

### FSM-03: No Mechanism to Remove States or Transitions

**Severity:** LOW
**File:** `Runtime/StateMachine/StateMachine.cs`

Once states and transitions are added, there is no API to remove them. The `StateMachine` also does not implement `IDisposable`, so the `OnStateChanged` event subscribers and the captured `Func<bool>` condition closures in transitions are never cleaned up. In long-lived games that reconfigure state machines (e.g., AI behavior trees that change based on game phase), this could lead to accumulating stale references.

**Recommendation:** Add `RemoveState`, `RemoveTransition`, and `Dispose` methods. Clear the `OnStateChanged` event on disposal.

---

### FSM-04: Transition Condition Exceptions Are Unhandled

**Severity:** LOW
**File:** `Runtime/StateMachine/StateMachine.cs`, lines 94-116

`CheckTransitions()` evaluates `Func<bool>` conditions. If any condition delegate throws an exception, it will propagate up through `Update()`, leaving the state machine in a potentially inconsistent state (the current state's `OnUpdate` will be skipped for that frame). No try/catch or error handling exists.

**Recommendation:** Wrap condition evaluation in try/catch blocks and log errors rather than propagating exceptions.

---

### DATA-01: ConfigData GUID Generated at Runtime is Non-Deterministic

**Severity:** LOW
**File:** `Runtime/Data/ConfigData.cs`, lines 12-18

`ConfigData.Guid` lazily generates a new GUID via `System.Guid.NewGuid()` if none is set. This means the GUID for a given config asset can change between sessions if the serialized `_guid` field was never persisted (e.g., the asset was loaded but never saved in the editor). This could cause lookup failures in `ConfigDatabase` if GUIDs are used as stable identifiers across sessions.

The same pattern exists in `AssetContainer.AssetGuid` (`Runtime/Data/AssetContainer.cs`, lines 41-49) and `AssetRef<T>` (line 30).

**Recommendation:** Ensure GUIDs are always assigned during asset creation in the editor and treat runtime GUID generation as an error condition that logs a warning.

---

### DATA-02: ConfigData<T>.Data Setter Allows Null Assignment

**Severity:** LOW
**File:** `Runtime/Data/ConfigData.cs`, lines 46-47

`ConfigData<T>.Data` has a public setter that accepts null. However, the getter auto-creates a `new T()` if `_data` is null. This asymmetry means setting `Data = null` followed by reading `Data` produces a new default instance, potentially confusing callers who expect null to persist.

**Recommendation:** Either prevent null assignment in the setter or remove the auto-creation in the getter.

---

### DATA-03: RuntimeAssetDatabase.Get Throws on Missing Asset

**Severity:** INFO
**File:** `Runtime/Data/AssetDatabase.cs`, lines 22-28

`RuntimeAssetDatabase.Get<T>(string guid)` throws `KeyNotFoundException` when the GUID is not found, while `TryGet` provides a safe alternative. The exception-throwing behavior is acceptable but inconsistent with other lookup patterns in the codebase (e.g., `ConfigDatabase.Get<T>()` returns null on miss). This inconsistency could confuse developers.

**Recommendation:** Standardize the lookup pattern across the codebase -- either always return null for misses or always throw. Consider adding a `Get` overload with a default value parameter.

---

### SYNC-09: No Thread Safety in ReactiveProperty

**Severity:** LOW
**File:** `Runtime/Sync/ReactiveProperty.cs`

`ReactiveProperty<T>` uses a plain `List<Action<T>>` for handlers with no synchronization. While Unity game code typically runs on the main thread, the framework provides no documentation or enforcement of this constraint. If a `ReactiveProperty` is accessed from a background thread (e.g., from an async loading operation or a job system callback), handler list corruption could occur.

The `MediatorPool` in `MediatorRegistry.cs` is the only component that uses locking, suggesting awareness of potential multi-threaded access in some scenarios but inconsistent application of thread safety.

**Recommendation:** Document the threading model explicitly. Either add thread-safety guards to `ReactiveProperty` or add debug-mode thread-affinity checks that assert main-thread access.

---

## Summary Table

| ID | Finding | Severity | Category | File(s) |
|---|---|---|---|---|
| SYNC-01 | Handler list mutation during notification causes iteration errors | MEDIUM | Correctness | ReactiveProperty.cs, ReactiveExtensions.cs |
| SYNC-02 | Subscriptions without BindingScope cause memory leaks | MEDIUM | Memory Leak | ReactiveProperty.cs, ReactiveExtensions.cs |
| SYNC-03 | ReactiveCollection has no individual unsubscribe mechanism | MEDIUM | Resource Leak | ReactiveProperty.cs |
| SYNC-04 | TwoWayBinding reentrancy guard not exception-safe | LOW | Correctness | BindingScope.cs |
| SYNC-05 | Inconsistent dirty-check vs force-sync strategies | LOW | Correctness | EntityView.cs, ViewSyncRunner.cs, ViewSyncSystem.cs |
| SYNC-06 | MediatorPool has no size limit and never shrinks | LOW | Memory | MediatorRegistry.cs |
| SYNC-07 | ReleaseAll disposes mediators instead of unbinding for reuse | LOW | Correctness | MediatorRegistry.cs |
| SYNC-08 | EntityHandle ID integer overflow in long sessions | LOW | Correctness | EntityHandleRegistry.cs |
| SYNC-09 | No thread safety in ReactiveProperty | LOW | Thread Safety | ReactiveProperty.cs |
| POOL-01 | No enforced state reset on pooled object reuse | MEDIUM | Stale Data | ObjectPool.cs, ViewPool.cs |
| POOL-02 | ActiveCount becomes negative after Clear() | LOW | Correctness | ObjectPool.cs |
| POOL-03 | Double-despawn not prevented in ObjectPool | LOW | Correctness | ObjectPool.cs |
| POOL-04 | PooledObject.Dispose returns to pool instead of disposing | INFO | Semantics | PooledObject.cs |
| FSM-01 | Transitions to unregistered states fail silently | MEDIUM | Validation | StateMachine.cs |
| FSM-02 | OnStateChanged fires after _isTransitioning reset (reentrancy) | MEDIUM | Correctness | StateMachine.cs |
| FSM-03 | No mechanism to remove states/transitions or dispose FSM | LOW | Resource Leak | StateMachine.cs |
| FSM-04 | Transition condition exceptions are unhandled | LOW | Error Handling | StateMachine.cs |
| DATA-01 | Config/Asset GUIDs generated non-deterministically at runtime | LOW | Data Integrity | ConfigData.cs, AssetContainer.cs |
| DATA-02 | ConfigData setter allows null but getter auto-creates | LOW | Consistency | ConfigData.cs |
| DATA-03 | Inconsistent missing-key behavior across databases | INFO | Consistency | AssetDatabase.cs, ConfigDatabase.cs |
