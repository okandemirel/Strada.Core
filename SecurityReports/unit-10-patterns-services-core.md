# Security Review: Unit 10 — Patterns, Services & Core Utilities

**Review Date:** 2026-03-07
**Reviewer:** Claude (automated security analysis)
**Scope:** `Runtime/Patterns/`, `Runtime/Services/`, `Runtime/Core/`

---

## Executive Summary

The Patterns, Services, and Core modules form the architectural backbone of the Strata.Core framework, implementing an MVCS (Model-View-Controller-Service) pattern with lifecycle management and a custom player loop integration. The code is generally well-structured with good disposal patterns and defensive null checks. However, several medium and low severity issues were identified around access control, unhandled exceptions during tick loops, missing input validation, and potential integer overflow in the timer service.

---

## Detailed Findings

### Finding 1: Unhandled Exceptions in Tick Loops Can Halt All Subsequent Updates

**Severity:** MEDIUM
**Location:** `Runtime/Patterns/PatternManager.cs` (lines 119-137), `Runtime/Core/PlayerLoop.cs` (lines 145-167)
**Description:** The `OnUpdate`, `OnFixedUpdate`, and `OnLateUpdate` methods iterate over registered tickables and invoke their `Tick`/`FixedTick`/`LateTick` methods without any exception handling. If any single tickable throws an exception, all subsequent tickables in the list will be skipped for that frame. In `PlayerLoop.cs`, the `RunUpdate`, `RunLateUpdate`, and `RunFixedUpdate` methods have the same issue — a single failing callback prevents all subsequent callbacks from executing.
**Impact:** A single faulty controller or service can break the entire game loop for all other components.
**Recommendation:** Wrap individual callback invocations in try-catch blocks and log errors rather than allowing them to propagate.

---

### Finding 2: `Construct` Method Is Public and Marked `[Inject]` — Allows External Re-injection

**Severity:** MEDIUM
**Location:** `Runtime/Patterns/Base.cs` (line 33)
**Description:** The `Construct(IContainer container)` method is `public` (required by the `[Inject]` attribute pattern) but this means any caller with a reference to a `Base`-derived instance can call `Construct` again with a different container. This would silently replace the `Container`, `World`, `EntityManager`, and `EventBus` references mid-lifecycle without re-initializing subscriptions or disposables, leading to inconsistent state.
**Impact:** If called after initialization, existing subscriptions point to the old `EventBus` while new operations use the new one, causing missed events or orphaned subscriptions.
**Recommendation:** Add a guard check (e.g., `if (Container != null) return;` or throw) to prevent re-injection after initial construction.

---

### Finding 3: `Controller<TModel>.InjectModel` Is Public and Can Be Called Multiple Times

**Severity:** MEDIUM
**Location:** `Runtime/Patterns/Controller.cs` (line 21)
**Description:** Similar to Finding 2, the `InjectModel` method is `public` with no guard against re-injection. Calling it multiple times silently replaces the Model reference without cleanup of any bindings to the previous model.
**Impact:** Stale references and inconsistent state between controller logic and its model.
**Recommendation:** Add a guard to prevent model replacement after initial injection, or handle the transition explicitly.

---

### Finding 4: Timer ID Integer Overflow

**Severity:** LOW
**Location:** `Runtime/Services/TimerService.cs` (line 39)
**Description:** The `_nextId` field is an `int` that increments on every `Schedule` call without overflow protection. In a long-running application with frequent timer creation, this will eventually overflow to negative values and then to zero. The `TimerHandle.IsValid` check requires `_id > 0`, meaning handles created after overflow past `int.MaxValue` will appear invalid (when `_id` wraps to negative) or could collide with previously used IDs.
**Impact:** In extremely long-running sessions, timer handles may incorrectly report as invalid or match stale entries.
**Recommendation:** Use a `long` for the ID counter, or add overflow detection and wrap-around logic.

---

### Finding 5: `PatternManager.RegisterController/RegisterService` Lack Duplicate Registration Guards

**Severity:** LOW
**Location:** `Runtime/Patterns/PatternManager.cs` (lines 37-69)
**Description:** Neither `RegisterController` nor `RegisterService` check whether the same instance has already been registered. A controller or service registered twice will be ticked twice per frame and disposed twice, leading to double-execution of logic and double-dispose errors.
**Impact:** Double-ticking causes logic to execute at double speed; double-dispose can throw `ObjectDisposedException` or corrupt state.
**Recommendation:** Add a `Contains` check or use a `HashSet` for deduplication before adding to the lists.

---

### Finding 6: `PatternManager.GetService/GetController` Uses LINQ Linear Scan

**Severity:** LOW
**Location:** `Runtime/Patterns/PatternManager.cs` (lines 143-153)
**Description:** `GetService<T>()` and `GetController<T>()` use `_services.OfType<T>().FirstOrDefault()`, which performs a linear scan. While not a direct security issue, if called frequently (e.g., every frame), this becomes a performance concern and could contribute to frame-time spikes that degrade user experience.
**Impact:** Performance degradation under high usage; potential denial-of-service vector in frame-sensitive contexts.
**Recommendation:** Cache lookups in a dictionary keyed by type, or document that these methods should not be called in hot paths.

---

### Finding 7: `View.UpdateView` Accepts Null Model Without Validation

**Severity:** LOW
**Location:** `Runtime/Patterns/View.cs` (lines 82-86)
**Description:** `View<TModel>.SetModel` correctly throws `ArgumentNullException` for null, but `UpdateView` assigns the model directly without null checking. This inconsistency means a null model can be injected through `UpdateView`, bypassing the safety of `SetModel`.
**Impact:** `NullReferenceException` in `OnViewUpdate` implementations that assume `Model` is non-null.
**Recommendation:** Add a null check in `UpdateView` consistent with `SetModel`, or document the difference in contract.

---

### Finding 8: `ReactiveModel.Property` Unsafe Cast Without Type Validation

**Severity:** MEDIUM
**Location:** `Runtime/Patterns/Model.cs` (lines 98-106)
**Description:** `ReactiveModel.Property<T>` retrieves properties by string name from a `Dictionary<string, object>` and casts with `(ReactiveProperty<T>)existing`. If the same property name is accessed with different type parameters (e.g., `Property<int>("health")` then `Property<float>("health")`), this will throw an `InvalidCastException` at runtime. Similarly, `GetProperty<T>` performs an unsafe cast.
**Impact:** Runtime crash from type mismatch. In a framework consumed by many developers, string-keyed property access with implicit typing is error-prone.
**Recommendation:** Validate the stored type matches `T` before casting, and throw a descriptive error on mismatch. Consider a compile-time-safe alternative.

---

### Finding 9: `PlayerLoop` Static State Not Thread-Safe

**Severity:** LOW
**Location:** `Runtime/Core/PlayerLoop.cs` (lines 16-21)
**Description:** All callback lists and state flags in `PlayerLoop` are static and accessed without synchronization. While Unity's main thread model generally prevents concurrent access, `RegisterUpdate`/`UnregisterUpdate` could be called from background threads (e.g., async callbacks), leading to list corruption.
**Impact:** Potential list corruption if callbacks are registered or unregistered from non-main threads.
**Recommendation:** Either enforce main-thread-only access with a thread check, or add synchronization to the registration methods.

---

### Finding 10: `Base.Dispose` Calls `OnDispose` Before Unsubscribing Events

**Severity:** LOW
**Location:** `Runtime/Patterns/Base.cs` (lines 124-140)
**Description:** In `Dispose()`, `OnDispose()` is called first (line 129), then event unsubscriptions happen (lines 131-133). If `OnDispose` in a subclass publishes events or triggers logic that causes re-entrant event handling, handlers are still subscribed during that window. This ordering can lead to unexpected behavior during teardown.
**Impact:** Re-entrant event handling during disposal can cause use-after-dispose scenarios.
**Recommendation:** Unsubscribe from events before calling `OnDispose`, or document the disposal ordering contract clearly.

---

### Finding 11: `PatternManager` Fixed Update Double-Iteration

**Severity:** INFO
**Location:** `Runtime/Patterns/PatternManager.cs` (lines 125-131)
**Description:** `OnFixedUpdate` iterates both `_fixedControllers` (IFixedTickController) and `_fixedTickables` (IFixedTickable). A controller implementing both `IFixedTickController` and `IFixedTickable` would be ticked twice per fixed update. The `RegisterController` method adds to both lists independently (lines 41-48).
**Impact:** Unintended double-execution of fixed update logic for controllers that implement both interfaces.
**Recommendation:** Ensure registration logic prevents adding the same instance to both `_fixedControllers` and `_fixedTickables`, or document the expected behavior.

---

## Summary Table

| # | Finding | Severity | Location | Category |
|---|---------|----------|----------|----------|
| 1 | Unhandled exceptions in tick loops halt subsequent updates | MEDIUM | PatternManager.cs, PlayerLoop.cs | Resource Management |
| 2 | Public `Construct` allows re-injection of container | MEDIUM | Base.cs | Access Control |
| 3 | Public `InjectModel` allows model replacement | MEDIUM | Controller.cs | Access Control |
| 4 | Timer ID integer overflow | LOW | TimerService.cs | Input Validation |
| 5 | No duplicate registration guard in PatternManager | LOW | PatternManager.cs | Input Validation |
| 6 | Linear scan in GetService/GetController | LOW | PatternManager.cs | Resource Management |
| 7 | `UpdateView` accepts null model | LOW | View.cs | Input Validation |
| 8 | Unsafe cast in ReactiveModel.Property | MEDIUM | Model.cs | Input Validation |
| 9 | Static PlayerLoop state not thread-safe | LOW | PlayerLoop.cs | Access Control |
| 10 | Dispose calls OnDispose before unsubscribing | LOW | Base.cs | Resource Management |
| 11 | Fixed update double-iteration for dual-interface controllers | INFO | PatternManager.cs | Interface Design |
