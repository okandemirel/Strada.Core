# Security Review: Unit 01 — DI Container Core

**Date:** 2026-03-07
**Reviewer:** Claude (Automated Security Analysis)
**Scope:** `Runtime/DI/` top-level files — Container, ContainerBuilder, ContainerBuilderExtensions, Registration, Lifetime, ContainerScope, TypeRegistry, InjectionProcessor, LifecycleProcessor, AsyncContainerScope, AsyncScopeBuilder, DirectFactory (IStradaFactory)

---

## Executive Summary

The Strata.Core DI container is a custom dependency injection framework for Unity. The review identified **13 findings** across several categories: race conditions in singleton resolution and disposal, information disclosure through error messages and logging, denial-of-service vectors via unbounded type registration and assembly scanning, resource leaks from missing disposal tracking, and code injection surface area through reflection-based type resolution from string names. The most significant issues involve thread safety defects in the singleton caching pattern and the disposal logic, which can lead to double-instantiation, use-after-dispose, and resource leaks under concurrent access.

---

## Detailed Findings

### DI-01 — Race Condition in Singleton Resolution (Double Instantiation)

| Field | Value |
|-------|-------|
| **ID** | DI-01 |
| **Severity** | HIGH |
| **File** | `Runtime/DI/Container.cs:236-256` |

**Description:**
The singleton factory (lines 236-256) checks `_singletons[index]` outside the lock, then creates the instance and uses `Interlocked.CompareExchange` to install it. While `CompareExchange` ensures only one value wins the race, a losing thread will create and then immediately dispose a duplicate instance. If the constructor has side effects (e.g., opening a file handle, starting a network connection, registering a global callback), those side effects execute before the duplicate is discarded. The disposed duplicate's side effects are not rolled back.

Additionally, the null-check `if (instance != null) return instance` on line 239 is performed without a volatile read or memory barrier, which on weakly-ordered architectures could read a stale null and proceed to create a duplicate.

**Recommendation:**
Use `lock (_lock)` around the entire singleton resolution path (check + create + store) instead of the lock-free `CompareExchange` pattern, or use `Volatile.Read` for the initial check and ensure constructors with side effects are safe for double-instantiation. The current approach in `Resolve<T>` already acquires `_lock` for singletons (line 57-60), but the factory lambda itself performs the lock-free CAS internally, meaning the lock in `Resolve<T>` protects the factory call while the factory itself has an unsynchronized fast path — these two synchronization strategies conflict.

---

### DI-02 — Use-After-Dispose Race in Container.Dispose

| Field | Value |
|-------|-------|
| **ID** | DI-02 |
| **Severity** | HIGH |
| **File** | `Runtime/DI/Container.cs:128-153` |

**Description:**
`Container.Dispose()` sets `_disposed = true` (line 131) without a memory barrier before acquiring the lock on line 133. A concurrent thread calling `Resolve<T>` may pass the `_disposed` check (line 47) before the write is visible, then enter the singleton factory lambda and call `rawFactory(this)` while `Dispose` is simultaneously popping and disposing instances from `_disposalStack`. This can result in:
- Resolving a service whose dependencies have already been disposed.
- Adding to `_disposalStack` after it has been drained.
- A singleton being created but never tracked for disposal.

The `_disposed` field is not marked `volatile`, so the store on line 131 has no guaranteed visibility ordering on weakly-ordered memory models (relevant for ARM-based platforms where Unity runs, e.g., Android, iOS).

**Recommendation:**
Mark `_disposed` as `volatile` or use `Volatile.Write`/`Volatile.Read`. Additionally, acquire the lock before setting `_disposed = true` to ensure atomicity with the disposal process, or check `_disposed` again inside the lock in `Resolve`.

---

### DI-03 — ContainerScope Disposal Not Thread-Safe

| Field | Value |
|-------|-------|
| **ID** | DI-03 |
| **Severity** | HIGH |
| **File** | `Runtime/DI/ContainerScope.cs:145-158` |

**Description:**
`ContainerScope.Dispose()` sets `_disposed = true` (non-volatile) and then iterates `_scopedInstances`, disposing each one. Concurrent calls to `ResolveByIndex` (line 70-101) can race with disposal:
1. A thread reads `_disposed` as false (stale), resolves a scoped service, and stores it via `Interlocked.CompareExchange` into `_scopedInstances[index]`.
2. The disposing thread has already passed that index, so the newly-created instance is never disposed.
3. Alternatively, a thread may read a scoped instance that is being concurrently disposed, leading to use-after-dispose.

**Recommendation:**
Mark `_disposed` as `volatile`. Use a lock around the disposal loop and the scoped resolution path, or use a two-phase disposal pattern where the disposed flag is checked under the same synchronization as instance creation.

---

### DI-04 — Type Resolution from Untrusted String Names

| Field | Value |
|-------|-------|
| **ID** | DI-04 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/ContainerBuilderExtensions.cs:40-43` |

**Description:**
`TryUseSourceGenerated` calls `Type.GetType(string)` with hardcoded assembly-qualified type names (lines 41-43). While the names are hardcoded (not user-supplied), the pattern is dangerous because:
1. `Type.GetType` with an assembly name will attempt to load the specified assembly if not already loaded. If an attacker can place a malicious `Assembly-CSharp.dll` in the probing path, the type will be loaded and its static constructor will execute.
2. The method then calls `registerMethod.Invoke(null, new object[] { builder })`, passing the container builder to the resolved type's `RegisterAll` method. A malicious assembly could register arbitrary services, replacing legitimate implementations with trojaned ones.

In a Unity context, the assembly names are standard Unity assemblies, but in modding scenarios or if the game loads external assemblies, this becomes exploitable.

**Recommendation:**
Validate the loaded type's assembly origin (e.g., check `Assembly.Location` or use a trusted assembly list). Consider using a compile-time reference instead of string-based `Type.GetType` where possible.

---

### DI-05 — Silent Exception Swallowing in Auto-Binding

| Field | Value |
|-------|-------|
| **ID** | DI-05 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/ContainerBuilderExtensions.cs:58-61, 80` |

**Description:**
Both `TryUseSourceGenerated` (line 59: `catch { return false; }`) and `GetAutoBindingCount` (line 80: `catch { }`) have bare catch blocks that swallow all exceptions silently. This masks:
- `TypeLoadException` from malformed or tampered assemblies.
- `SecurityException` from code access security violations.
- `TargetInvocationException` wrapping critical errors in the registered method.

An attacker who partially compromises the generated registry could cause it to throw, and the system would silently fall back to runtime scanning, which may have different (potentially weaker) filtering.

**Recommendation:**
Log caught exceptions at a warning level. At minimum, catch only expected exception types (`TypeLoadException`, `ReflectionTypeLoadException`) and let unexpected exceptions propagate.

---

### DI-06 — Unbounded Type ID Allocation (Denial of Service)

| Field | Value |
|-------|-------|
| **ID** | DI-06 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/TypeRegistry.cs:37`, `Runtime/DI/Container.cs:199` |

**Description:**
`TypeRegistry.AllocateId()` uses `Interlocked.Increment` on a monotonically increasing counter with no upper bound. Each unique type resolved via `TypeRegistry.GetId(Type)` allocates a new ID. The container then allocates an array of size `maxTypeId + 1` (Container.cs line 199: `new int[maxId + 1]`).

If an attacker can trigger resolution of many distinct types (e.g., through open generics, runtime type generation, or repeated calls with different generic type arguments), the type ID counter grows unboundedly, and each new container allocates increasingly large arrays. This is a memory exhaustion vector.

In Unity, `Type.GetType` through the auto-binding scanner iterates all types in matching assemblies (RuntimeAutoBindingScanner.cs line 80), which will allocate IDs for every discovered type.

**Recommendation:**
Add a configurable maximum type ID limit with a reasonable default (e.g., 4096). Throw an `InvalidOperationException` if the limit is exceeded. Consider a hash-based lookup instead of a sparse array for the type-to-index mapping.

---

### DI-07 — Information Disclosure via Error Messages and Logging

| Field | Value |
|-------|-------|
| **ID** | DI-07 |
| **Severity** | LOW |
| **File** | `Runtime/DI/Container.cs:143, 72, 176, 304` |

**Description:**
Several error paths disclose internal type names and full exception details:
- Line 143: `Debug.LogError($"Error disposing service: {e}")` logs the full exception including stack trace, which may reveal internal class names, file paths, and framework internals.
- Line 72: `ThrowNotRegistered<T>` includes the type name in the exception message.
- Line 176: `ResolveByType` includes the type name.
- Line 304: `CompileFactory` includes both dependency and implementation type names.

In a shipped game, these messages may appear in player logs or crash reports, revealing the internal architecture.

**Recommendation:**
In release builds, use generic error messages or error codes instead of type names. Use conditional compilation (`#if DEBUG` / `#if UNITY_EDITOR`) to control verbosity of error messages.

---

### DI-08 — DirectFactory Static Mutable Field (Global State Tampering)

| Field | Value |
|-------|-------|
| **ID** | DI-08 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/IStradaFactory.cs:7` (DirectFactory), `Runtime/DI/Container.cs:361-362` |

**Description:**
`DirectFactory<T>.Delegate` is a public static mutable field. Any code with access to this type can replace the factory delegate for any service type:

```csharp
DirectFactory<IAuthService>.Delegate = (container) => new MaliciousAuthService();
```

This allows service implementation replacement without going through the container builder, bypassing any validation. The field is set to null in `ClearFactory` during disposal (line 362) via reflection, but there is no protection against external writes.

Additionally, `ClearFactory` uses `MakeGenericType` and `GetField` with reflection to clear the delegate, which is fragile and could fail silently if the field is renamed.

**Recommendation:**
Make `DirectFactory<T>.Delegate` internal with `[assembly: InternalsVisibleTo]` limited to the framework assembly only. Consider using a `Func` stored in a container-scoped dictionary rather than a global static field.

---

### DI-09 — Reflection-Based Method Injection Invokes Private Methods

| Field | Value |
|-------|-------|
| **ID** | DI-09 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/InjectionProcessor.cs:57, 59-70` |

**Description:**
`InjectionProcessor.BuildInjectionInfo` scans for methods marked with `[Inject]` using `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic` (line 57). This means the `[Inject]` attribute can trigger invocation of private and protected methods via reflection (line 100: `method.Method.Invoke(target, args)`).

If an attacker can control which types are instantiated and injected (e.g., through auto-binding), they could craft a type where a private method marked with `[Inject]` performs privileged operations that were not intended to be called externally.

Similarly, `BindingFlags.NonPublic` for fields (line 57) allows injection into private fields, potentially overwriting security-critical internal state.

**Recommendation:**
Consider restricting injection to public members only, or document the security implications of `[Inject]` on non-public members. Add an opt-in flag for non-public injection.

---

### DI-10 — LifecycleProcessor Cache Not Thread-Safe

| Field | Value |
|-------|-------|
| **ID** | DI-10 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/LifecycleProcessor.cs:10-11, 19-21, 31-33` |

**Description:**
`LifecycleProcessor` uses `Dictionary<Type, MethodInfo[]>` caches (`PostConstructCache`, `DeConstructCache`) without any synchronization. The `TryGetValue` on line 19 and the assignment on line 21 are not protected by a lock. `Dictionary` is not thread-safe for concurrent reads and writes; concurrent access can corrupt the internal hash table, leading to infinite loops in bucket traversal (a known .NET `Dictionary` issue that causes hangs).

`ClearCache()` (line 59-62) also clears both dictionaries without synchronization, which can corrupt state if called while other threads are reading.

**Recommendation:**
Use `ConcurrentDictionary<Type, MethodInfo[]>` or add lock synchronization around cache reads and writes, consistent with the pattern used in `InjectionProcessor` and `TypeRegistry`.

---

### DI-11 — Transient IDisposable Services Are Never Disposed

| Field | Value |
|-------|-------|
| **ID** | DI-11 |
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/Container.cs:263-266` |

**Description:**
Transient registrations (line 263-266) assign `rawFactory` directly with no disposal tracking. Each call to `Resolve<T>()` for a transient service creates a new instance. If the implementation type implements `IDisposable`, the container has no reference to track or dispose these instances.

This is a resource leak by design. While some DI containers document this as expected behavior, it can lead to:
- Native resource leaks (file handles, network connections) in a long-running Unity game.
- Memory leaks from instances holding references to large object graphs.

**Recommendation:**
Document that transient `IDisposable` implementations are the caller's responsibility. Consider adding a warning at registration time if a transient type implements `IDisposable`. Alternatively, track transient disposables per-scope and dispose them when the scope is disposed.

---

### DI-12 — No Circular Dependency Protection for Factory/Instance Registrations

| Field | Value |
|-------|-------|
| **ID** | DI-12 |
| **Severity** | LOW |
| **File** | `Runtime/DI/ContainerBuilder.cs:96-97` |

**Description:**
`DetectCircularDependencies` (line 87-103) skips registrations with `Factory != null` or `Instance != null` (line 96-97). A factory delegate could call `container.Resolve<T>()` for a type that depends back on the factory's service type, creating a runtime circular dependency that results in a `StackOverflowException`.

This is not detected at build time and will crash the application at runtime without a meaningful error message.

**Recommendation:**
Add runtime circular dependency detection using a thread-local or `AsyncLocal` resolution stack. When resolving, push the type being resolved; if it is already on the stack, throw a descriptive `InvalidOperationException` instead of allowing a stack overflow.

---

### DI-13 — Assembly Scanning Pattern Matching Is Overly Permissive

| Field | Value |
|-------|-------|
| **ID** | DI-13 |
| **Severity** | LOW |
| **File** | `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs:46-47` |

**Description:**
The default include patterns (`Strada.*`, `Game.*`, `Assembly-CSharp`) and exclude patterns are simple wildcard matches. The `Game.*` pattern is very broad and could match assemblies from third-party plugins or user-generated content (e.g., `GameModLoader`, `GameCheatEngine`). If a malicious assembly with a matching name is loaded (e.g., through Unity Asset Bundles or modding APIs), its types decorated with `[AutoRegister]` will be automatically registered into the container, potentially replacing legitimate service implementations.

**Recommendation:**
Tighten default include patterns or require explicit opt-in. Consider validating assembly signatures or checking assembly load context. Document that auto-binding should not be used when untrusted assemblies may be loaded.

---

## Summary Table

| ID | Severity | Title | File |
|----|----------|-------|------|
| DI-01 | HIGH | Race condition in singleton resolution (double instantiation) | Container.cs:236-256 |
| DI-02 | HIGH | Use-after-dispose race in Container.Dispose | Container.cs:128-153 |
| DI-03 | HIGH | ContainerScope disposal not thread-safe | ContainerScope.cs:145-158 |
| DI-04 | MEDIUM | Type resolution from untrusted string names | ContainerBuilderExtensions.cs:40-43 |
| DI-05 | MEDIUM | Silent exception swallowing in auto-binding | ContainerBuilderExtensions.cs:58-61, 80 |
| DI-06 | MEDIUM | Unbounded type ID allocation (denial of service) | TypeRegistry.cs:37, Container.cs:199 |
| DI-07 | LOW | Information disclosure via error messages and logging | Container.cs:143, 72, 176, 304 |
| DI-08 | MEDIUM | DirectFactory static mutable field (global state tampering) | IStradaFactory.cs:7, Container.cs:361-362 |
| DI-09 | MEDIUM | Reflection-based method injection invokes private methods | InjectionProcessor.cs:57, 59-70 |
| DI-10 | MEDIUM | LifecycleProcessor cache not thread-safe | LifecycleProcessor.cs:10-11, 19-21 |
| DI-11 | MEDIUM | Transient IDisposable services are never disposed | Container.cs:263-266 |
| DI-12 | LOW | No circular dependency protection for factory registrations | ContainerBuilder.cs:96-97 |
| DI-13 | LOW | Assembly scanning pattern matching is overly permissive | RuntimeAutoBindingScanner.cs:46-47 |

**Totals:** 3 HIGH, 6 MEDIUM, 3 LOW, 0 CRITICAL, 0 INFO
