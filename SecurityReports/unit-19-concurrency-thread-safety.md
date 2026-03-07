# Unit 19 — Concurrency & Thread Safety (Cross-Cutting)

## Executive Summary

The Strata.Core codebase uses a mix of concurrency primitives including `lock` statements, `Interlocked` operations, `Volatile` reads, and `SemaphoreSlim`. The DI container and EventBus are the most concurrency-aware subsystems. However, several areas have race conditions stemming from unprotected reads of shared `Dictionary` instances outside of locks, unsynchronized static mutable state in multiple classes, and a TOCTOU vulnerability in the `TypeRegistry.GetId(Type)` method. The ECS `ComponentPlayback` handler registry uses a static `Dictionary` without any synchronization. The `PlayerLoop` class exposes static mutable `List` fields with no thread safety despite being invoked from Unity's player loop callbacks.

**Overall Risk: MEDIUM** — The framework primarily targets Unity's single-threaded main loop, which mitigates many theoretical races. However, the explicit use of locks and async patterns in DI/EventBus suggests multi-threaded access is intended in those areas, and several gaps exist.

---

## Inventory of Concurrency Patterns

| Pattern | Files | Count |
|---------|-------|-------|
| `lock (` | 9 files | ~25 lock statements |
| `Interlocked.` | 5 runtime files | ~10 usages |
| `Volatile.Read` | ContainerScope.cs | 3 usages |
| `SemaphoreSlim` | AsyncContainerScope.cs | 1 instance |
| `IJobParallelFor` | ParallelComponentJob.cs | 4 struct implementations |
| `static` mutable `Dictionary`/`List` | 10+ files | Widespread |
| `ConcurrentDictionary` | None | 0 |
| `async`/`await` | 12 files | Various |

---

## Detailed Findings

### Finding 1: TOCTOU Race in TypeRegistry.GetId(Type)

**Severity: MEDIUM**
**File:** `Runtime/DI/TypeRegistry.cs` (lines 17-34)

The `GetId(Type)` method performs a `TryGetValue` on `_typeCache` outside the lock (line 19), then re-checks inside the lock (line 24). The outer read of the `Dictionary` is not thread-safe because `Dictionary<TKey,TValue>` does not support concurrent readers and writers. If one thread is inside the lock adding to `_typeCache` (which rehashes the internal buckets), another thread calling `TryGetValue` outside the lock could observe corrupted state, leading to incorrect return values or exceptions.

```csharp
// Line 19 — unsynchronized read while another thread may be writing at line 32
if (_typeCache.TryGetValue(type, out int id))
    return id;

lock (_cacheLock)
{
    if (_typeCache.TryGetValue(type, out id))
        return id;
    // ... writes to _typeCache at line 32
}
```

**Recommendation:** Use `ConcurrentDictionary<Type, int>` or always acquire the lock before reading.

---

### Finding 2: TOCTOU Race in InjectionProcessor.GetOrCreateInfo

**Severity: MEDIUM**
**File:** `Runtime/DI/InjectionProcessor.cs` (lines 35-49)

Same double-checked locking pattern over a non-concurrent `Dictionary`. The `TryGetValue` at line 37 executes without the lock while line 46 writes inside the lock. A concurrent write can corrupt the Dictionary's internal hash table while a reader is iterating buckets.

```csharp
if (_cache.TryGetValue(type, out var info))   // unsynchronized read
    return info;

lock (_lock)
{
    if (_cache.TryGetValue(type, out info))
        return info;
    info = BuildInjectionInfo(type);
    _cache[type] = info;                      // write under lock
}
```

**Recommendation:** Use `ConcurrentDictionary` or move all reads inside the lock.

---

### Finding 3: Unsynchronized Static Dictionary in ComponentPlayback

**Severity: HIGH**
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs` (lines 307-338)

`ComponentPlayback._handlers` is a `static Dictionary<ulong, IComponentPlaybackHandler>` with zero synchronization. `RegisterHandler`, `EnsureHandler`, `AddComponent`, `RemoveComponent`, and `SetComponent` all read/write without locks. Since `EntityCommandBuffer` is used alongside `IJobParallelFor` (parallel jobs), this dictionary could be accessed from job setup/teardown code on different threads.

```csharp
private static readonly Dictionary<ulong, IComponentPlaybackHandler> _handlers = new(64);

public static void RegisterHandler<T>(...) {
    _handlers[TypeHash<T>.Value] = handler;   // no lock
}

public static unsafe void AddComponent(...) {
    if (_handlers.TryGetValue(typeHash, out var handler))  // no lock
        handler.AddComponent(em, entity, data, size);
}
```

**Recommendation:** Add lock protection or use `ConcurrentDictionary`. If registration only happens at startup, document the thread-safety contract.

---

### Finding 4: Unsynchronized Static Dictionary in LifecycleProcessor

**Severity: MEDIUM**
**File:** `Runtime/DI/LifecycleProcessor.cs` (lines 10-11, 19-26, 30-37)

`PostConstructCache` and `DeConstructCache` are static `Dictionary` fields read and written without any synchronization. If `InvokePostConstruct` or `InvokeDeConstruct` is called concurrently (e.g., from async container resolution), concurrent dictionary mutation can corrupt internal state.

```csharp
private static readonly Dictionary<Type, MethodInfo[]> PostConstructCache = new();
private static readonly Dictionary<Type, MethodInfo[]> DeConstructCache = new();
// No locks on TryGetValue or indexer writes
```

**Recommendation:** Add lock-based synchronization or use `ConcurrentDictionary`.

---

### Finding 5: Race Condition in Container Singleton Factory

**Severity: MEDIUM**
**File:** `Runtime/DI/Container.cs` (lines 236-256)

The singleton factory uses `Interlocked.CompareExchange` for the singleton slot (good), but the initial null-check at line 239 reads `_singletons[index]` without a volatile read or memory barrier. On weakly-ordered architectures this can lead to reading a partially constructed object. The `Resolve<T>` method acquires `_lock` for singletons (lines 57-60), but the factory lambda itself does an unguarded read first.

Additionally, line 59 invokes `_factories[index](this)` while holding `_lock`. If the factory recursively resolves another singleton, it will re-enter the same lock. Since C# `lock` (Monitor) is re-entrant, this does not deadlock, but it does mean a circular dependency would cause a stack overflow rather than a clean error.

**Recommendation:** Use `Volatile.Read` for the initial null-check in the singleton factory. Consider adding circular dependency detection.

---

### Finding 6: EventBus — Torn Read on Handler Array References

**Severity: MEDIUM**
**File:** `Runtime/Communication/EventBus.cs` (lines 79-88, 116-124)

The `Send` and `Publish` methods read handler arrays without locks (`var handlers = _signalHandlers;`). While this is a reference assignment (atomic in .NET), the `EnsureCapacity` method (line 306-313) creates a new array and assigns it with `array = newArray` — but `_signalHandlers` is not marked `volatile`, so on weakly-ordered architectures a reading thread may see a stale reference. The copy-on-write pattern used in `EventChannel` (lines 351-379) is correctly implemented for `_handlers`, but the top-level arrays for signals/queries/async use `EnsureCapacity` which replaces the array reference under a lock while readers read without a lock and without volatile.

```csharp
// Send (no lock)
var handlers = _signalHandlers;  // may be stale
if (id < handlers.Length && handlers[id] != null) { ... }

// EnsureCapacity (under lock)
array = newArray;  // not volatile, caller's field assignment
```

**Recommendation:** Mark `_signalHandlers`, `_queryHandlers`, `_eventChannels`, `_asyncSignalHandlers`, `_asyncQueryHandlers` as `volatile`, or use `Volatile.Write`/`Volatile.Read`.

---

### Finding 7: PlayerLoop — Unsynchronized Static Mutable Lists

**Severity: LOW**
**File:** `Runtime/Core/PlayerLoop.cs` (lines 16-22)

Four static `List` fields (`_updateCallbacks`, `_lateUpdateCallbacks`, `_fixedUpdateCallbacks`, `_initCallbacks`) are mutated by public `Register`/`Unregister` methods and iterated by `Run*` methods, all without synchronization. In Unity, these are expected to run on the main thread, but no enforcement exists. If `RegisterUpdate` is called from a background thread while `RunUpdate` is iterating, `List` mutation during iteration will throw `InvalidOperationException` or produce undefined behavior.

Additionally, `_initialized` and `_disposed` flags (lines 21-22) are not volatile, creating potential visibility issues.

**Recommendation:** Add a thread-affinity check or use lock-based synchronization for the callback lists.

---

### Finding 8: RuntimeSystemDiscovery — Unsynchronized Static Cache

**Severity: LOW**
**File:** `Runtime/Modules/RuntimeSystemDiscovery.cs` (lines 18-19, 56-58, 98-105)

`_cachedSystems` (a `Dictionary`) and `_cacheInitialized` (a `bool`) are static mutable fields accessed without synchronization. `ClearCache()` modifies both without locks, while `EnsureCacheInitialized()` reads `_cacheInitialized` without a memory barrier. Concurrent calls could result in double-initialization or reads from a partially populated dictionary.

**Recommendation:** Add synchronization or make initialization thread-safe with a lock.

---

### Finding 9: MediatorPool — Correct Lock Usage (Positive Finding)

**Severity: INFO**
**File:** `Runtime/Sync/MediatorRegistry.cs` (lines 96-118)

`MediatorPool<TMediator, TView>.MediatorPoolInstance` correctly uses a lock for both `Rent` and `Return` operations on the internal `Stack`. However, the static `_instance` field (line 93) uses `??=` without synchronization, which on rare occasion could create two pool instances under concurrent access. This is benign (both would function correctly) but not strictly thread-safe.

**Recommendation:** Use `Lazy<T>` or a lock for `_instance` initialization if strict single-instance semantics are required.

---

### Finding 10: AsyncContainerScope — SemaphoreSlim for Async Init (Positive Finding)

**Severity: INFO**
**File:** `Runtime/DI/AsyncContainerScope.cs` (lines 14, 66-74)

Correctly uses `SemaphoreSlim` for serializing async initialization of `IAsyncInitializable` instances. However, this means all async initializations are serialized globally within a scope, which could become a bottleneck if many async services need initialization simultaneously.

**Recommendation:** Consider per-type initialization locks if parallelism is desired.

---

### Finding 11: Container.Dispose — Race with Concurrent Resolve

**Severity: MEDIUM**
**File:** `Runtime/DI/Container.cs` (lines 128-153)

`Dispose()` sets `_disposed = true` (line 131) without synchronization, then acquires `_lock` to drain the disposal stack. Meanwhile, `Resolve<T>()` checks `_disposed` (line 47) without a lock or volatile read. A thread could read `_disposed` as false, proceed to resolve, and access `_singletons` or `_factories` while `Dispose()` is nulling them out (lines 151-152). This is a classic TOCTOU issue.

**Recommendation:** Mark `_disposed` as `volatile` or check it within the lock in `Resolve`.

---

### Finding 12: EventBus.Dispose — Non-Atomic Flag Set

**Severity: LOW**
**File:** `Runtime/Communication/EventBus.cs` (lines 223-228)

`Dispose()` sets `_disposed = true` without synchronization or volatile semantics. `Send`/`Publish` do not check `_disposed` at all, meaning they can operate on a disposed bus. While `Clear()` is called under lock, the `_disposed` flag itself is not protected.

**Recommendation:** Mark `_disposed` as `volatile`. Consider checking `_disposed` in `Send`/`Publish` for fail-fast behavior.

---

### Finding 13: HotReloadManager — Unsynchronized Static Collections

**Severity: LOW**
**File:** `Editor/HotReload/HotReloadManager.cs` (lines 19-23)

Multiple static `Dictionary` and `Queue` fields are read and written without synchronization. This is Editor-only code that runs on Unity's main thread, so the practical risk is low. However, `QueueConfigChange` could theoretically be called from an asset postprocessor on a different thread.

**Recommendation:** Acceptable for editor-only code, but adding a main-thread assertion would be defensive.

---

### Finding 14: SignalSequence.ActionEntry — Synchronous Wait on Async

**Severity: MEDIUM**
**File:** `Runtime/Communication/SignalSequence.cs` (line 307)

`ActionEntry.Execute` calls `.AsTask().Wait()` on an async action, which can cause deadlocks in contexts with a synchronization context (such as Unity's main thread). If the async action awaits something that needs to post back to the main thread, it will deadlock.

```csharp
public void Execute(IEventBus defaultBus)
{
    _asyncAction?.Invoke(CancellationToken.None).AsTask().Wait();  // deadlock risk
}
```

**Recommendation:** Avoid synchronous `.Wait()` on async code. Consider throwing `NotSupportedException` for sync execution of async entries, or use a custom synchronous wait that pumps the message loop.

---

### Finding 15: ParallelComponentJob — Unsafe Pointer Usage

**Severity: INFO**
**File:** `Runtime/ECS/Jobs/ParallelComponentJob.cs`

The `ComponentJobParallel` structs use `[NativeDisableUnsafePtrRestriction]` to bypass Unity's safety checks for raw pointers in parallel jobs. This is a deliberate performance optimization but removes Unity's built-in race condition detection for parallel writes to the same memory. The user-defined `UserJob.Execute` receives `ref` to component data, and if the sparse set indices map multiple entities to the same component slot, concurrent writes would be a data race.

**Recommendation:** Document the safety contract. Ensure that the sparse set guarantees unique index mapping per entity.

---

## Summary Table

| # | Finding | Severity | File(s) | Category |
|---|---------|----------|---------|----------|
| 1 | TOCTOU in TypeRegistry double-checked lock | MEDIUM | TypeRegistry.cs | Race Condition / TOCTOU |
| 2 | TOCTOU in InjectionProcessor double-checked lock | MEDIUM | InjectionProcessor.cs | Race Condition / TOCTOU |
| 3 | Unsynchronized static Dictionary in ComponentPlayback | HIGH | EntityCommandBuffer.cs | Missing Synchronization |
| 4 | Unsynchronized static Dictionary in LifecycleProcessor | MEDIUM | LifecycleProcessor.cs | Missing Synchronization |
| 5 | Container singleton factory non-volatile read | MEDIUM | Container.cs | Race Condition |
| 6 | EventBus handler arrays not volatile | MEDIUM | EventBus.cs | Race Condition |
| 7 | PlayerLoop unsynchronized static Lists | LOW | PlayerLoop.cs | Missing Synchronization |
| 8 | RuntimeSystemDiscovery unsynchronized cache | LOW | RuntimeSystemDiscovery.cs | Missing Synchronization |
| 9 | MediatorPool instance lazy init race (benign) | INFO | MediatorRegistry.cs | Race Condition |
| 10 | AsyncContainerScope serialized init (perf note) | INFO | AsyncContainerScope.cs | Lock Contention |
| 11 | Container.Dispose race with Resolve | MEDIUM | Container.cs | TOCTOU |
| 12 | EventBus.Dispose non-atomic flag | LOW | EventBus.cs | Race Condition |
| 13 | HotReloadManager unsynchronized statics | LOW | HotReloadManager.cs | Missing Synchronization |
| 14 | SignalSequence sync Wait on async | MEDIUM | SignalSequence.cs | Deadlock |
| 15 | ParallelComponentJob unsafe ptr bypass | INFO | ParallelComponentJob.cs | Race Condition |

## Security Analysis Checklist

- [x] Deadlock scenarios — Finding 14 (sync `.Wait()` on async path)
- [x] Race conditions — Findings 1, 2, 5, 6, 9, 11, 12, 15
- [x] TOCTOU — Findings 1, 2, 11
- [x] Missing synchronization on shared state — Findings 3, 4, 7, 8, 13
- [x] Lock ordering violations — No nested lock issues detected across different lock objects
- [x] Thread-unsafe collection usage — Findings 1, 2, 3, 4, 7, 8
