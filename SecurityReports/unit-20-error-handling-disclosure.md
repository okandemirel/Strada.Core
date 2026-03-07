# Unit 20 — Error Handling & Information Disclosure (Cross-Cutting)

## Executive Summary

This review examined all error handling patterns across the Strata.Core codebase for information disclosure risks, over-broad exception catching, empty catch blocks, and missing error handling on critical paths. The codebase contains approximately 45 catch blocks, 130+ Debug.Log calls, and 40+ throw statements. The most significant findings are stack trace exposure in runtime production logs, full exception object logging in the DI container, 13 empty catch blocks that silently swallow errors, and missing error handling in the ECS system update loop. No critical-severity issues were found; the risk profile is moderate overall, driven primarily by information disclosure through verbose error logging in runtime code.

## Inventory of Error Handling Patterns

### Try/Catch Blocks (45 total)

| Category | Count | Files |
|----------|-------|-------|
| Runtime — over-broad `catch (Exception)` | 12 | Container.cs, GameBootstrapper.cs, ComponentBinding.cs, EntityView.cs, ModuleRegistry.cs, ModuleBootstrapper.cs |
| Editor — over-broad `catch (Exception)` | 22 | HotReloadManager.cs, EntityStatePreserver.cs, SystemProfilerWindow.cs, BenchmarkPersistence.cs, StradaTemplates.cs, ServiceNode.cs, and others |
| Specific exception catches | 4 | RuntimeAutoBindingScanner.cs, RuntimeSystemDiscovery.cs, ArchitectureValidator.cs (`ReflectionTypeLoadException`); SignalSequenceTests.cs (`OperationCanceledException`) |
| Empty catch blocks | 13 | ContainerBuilderExtensions.cs, ModuleInitializerGenerator.cs, SystemRegistryGenerator.cs, ModuleDiscovery.cs (x2), BusDebuggerWindow.cs, StradaEntityInspectorWindow.cs (x2), StradaConfigDataManagerWindow.cs, WorldDataProvider.cs (x3), BusDataProvider.cs |
| Tests | 4 | StressTestRunner.cs (x2), ManagedComponentTests.cs, AsyncEventBusTests.cs |

### Logging Calls (130+ total)

| Level | Approximate Count | Key Locations |
|-------|-------------------|---------------|
| `Debug.Log` | ~70 | Across editor tooling, tests, bootstrap |
| `Debug.LogWarning` | ~35 | Error recovery paths, validation |
| `Debug.LogError` | ~25 | Critical failures, disposal errors |

### Throw Statements (40+ total)

| Pattern | Count | Notes |
|---------|-------|-------|
| `ArgumentNullException` | 14 | Parameter validation |
| `InvalidOperationException` | 12 | State/registration errors |
| `ObjectDisposedException` | 4 | Container/scope disposal |
| `KeyNotFoundException` | 1 | AssetDatabase |
| `throw;` (re-throw) | 3 | StressTestRunner, ModuleBootstrapper |

## Detailed Findings

### Finding 1: Stack Trace Exposure in Runtime Production Logs

**Severity:** MEDIUM
**Files:**
- `Runtime/Bootstrap/GameBootstrapper.cs` (lines 266, 273)
- `Runtime/Modules/ModuleBootstrapper.cs` (line 76)

**Description:**
Three locations in runtime code explicitly log `ex.StackTrace` via `Debug.LogError`. These are runtime classes, not editor-only code, meaning stack traces will appear in production player logs. Stack traces disclose internal class names, method signatures, file paths, and line numbers to anyone with access to log output.

**Code:**
```csharp
// GameBootstrapper.cs:266
Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}\n{ex.StackTrace}");

// GameBootstrapper.cs:273
Debug.LogError($"[GameBootstrapper] Initialization failed: {ex.Message}\n{ex.StackTrace}");

// ModuleBootstrapper.cs:76
Debug.LogError($"[{GetType().Name}] Module initialization failed: {ex.Message}\n{ex.StackTrace}");
```

**Risk:** An attacker with access to device logs (e.g., via `adb logcat` on Android, or crash reporting services) can learn the full internal architecture, namespace structure, and method names of the framework.

**Recommendation:** Use conditional compilation (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`) to gate stack trace logging, or use a centralized logging abstraction with configurable verbosity levels.

---

### Finding 2: Full Exception Object Logged in DI Container Disposal

**Severity:** MEDIUM
**File:** `Runtime/DI/Container.cs` (line 143)

**Description:**
During container disposal, the full exception object is interpolated into the log message using `{e}` (equivalent to `e.ToString()`), which includes the exception type, message, and full stack trace.

**Code:**
```csharp
catch (Exception e)
{
    UnityEngine.Debug.LogError($"Error disposing service: {e}");
}
```

**Risk:** This is runtime code. The full exception dump may reveal service type names, constructor signatures, dependency chains, and internal state during disposal — all available in production logs.

**Recommendation:** Log only `e.Message` in production, or wrap behind a development-build guard.

---

### Finding 3: Full Exception Object Logged in StressTestRunner

**Severity:** LOW
**File:** `Tests/Stress/StressTestRunner.cs` (lines 27, 49)

**Description:**
The stress test runner logs full exception objects with `{ex}`. While this is test code, it is compiled into the `Tests` assembly which may ship in development builds.

**Code:**
```csharp
UnityEngine.Debug.LogError($"[StressTest] Failed: {testName} after {sw.ElapsedMilliseconds}ms. Error: {ex}");
```

**Risk:** Low, since this is test infrastructure, but the pattern could be copied to production code.

---

### Finding 4: Empty Catch Blocks Silently Swallowing Exceptions (13 instances)

**Severity:** MEDIUM
**Files:**
- `Runtime/DI/ContainerBuilderExtensions.cs` (line 80) — **Runtime code**
- `Editor/CodeGen/ModuleInitializerGenerator.cs` (line 71)
- `Editor/CodeGen/SystemRegistryGenerator.cs` (line 64)
- `Editor/ModuleGenerator/Utilities/ModuleDiscovery.cs` (lines 158, 212)
- `Editor/Windows/BusDebuggerWindow.cs` (line 375)
- `Editor/Windows/StradaEntityInspectorWindow.cs` (lines 777, 991)
- `Editor/Windows/StradaConfigDataManagerWindow.cs` (line 110)
- `Editor/DataProviders/WorldDataProvider.cs` (lines 188, 209, 243)
- `Editor/DataProviders/BusDataProvider.cs` (line 195)

**Description:**
Thirteen empty `catch { }` blocks across the codebase silently discard exceptions with no logging or error handling. The most concerning is in `ContainerBuilderExtensions.cs` (runtime code), where a failure to discover the generated service registry is silently ignored. This could mask a misconfigured DI container or a corrupted generated registry — potentially leading to services failing to resolve at runtime with no diagnostic information.

The editor-side empty catches are in reflection-heavy code (inspecting private fields, reading entity versions, extracting component data). While individually low risk, the pattern masks errors in tooling that developers rely on for debugging.

**Recommendation:** At minimum, add `Debug.LogWarning` calls. For the runtime `ContainerBuilderExtensions.cs` case, consider logging a warning that the generated registry was not found, as this affects service resolution correctness.

---

### Finding 5: Over-Broad Exception Catching in Runtime Code

**Severity:** LOW
**Files:**
- `Runtime/DI/Container.cs` (line 141)
- `Runtime/Bootstrap/GameBootstrapper.cs` (lines 220, 263, 295)
- `Runtime/Sync/ComponentBinding.cs` (lines 114, 214)
- `Runtime/Sync/EntityView.cs` (line 286)
- `Runtime/Modules/ModuleRegistry.cs` (lines 41, 136)
- `Runtime/Modules/ModuleBootstrapper.cs` (line 74)

**Description:**
All 12 catch blocks in runtime code catch the base `System.Exception` type rather than specific exception types. This means security-relevant exceptions (e.g., `UnauthorizedAccessException`, `SecurityException`, `OutOfMemoryException`) are caught and handled identically to benign errors.

In `ComponentBinding.cs` and `EntityView.cs`, exceptions during ECS sync operations are caught and stored in `_lastError` as `ex.Message`. If an `OutOfMemoryException` is caught here, it would be silently downgraded to a binding error string rather than causing the application to fail fast as it should.

**Recommendation:** Consider catching specific expected exceptions (e.g., `InvalidOperationException`, `ArgumentException`) and allowing fatal exceptions to propagate. Alternatively, add an explicit filter to re-throw critical exceptions like `OutOfMemoryException`, `StackOverflowException`, and `ThreadAbortException`.

---

### Finding 6: Type and Assembly Name Disclosure in Exception Messages

**Severity:** LOW
**Files:**
- `Runtime/DI/Container.cs` (lines 72, 176, 304, 314)
- `Runtime/DI/ContainerBuilder.cs` (lines 84, 112)
- `Runtime/DI/ContainerScope.cs` (lines 60, 64)
- `Runtime/Communication/EventBus.cs` (line 317)
- `Runtime/ECS/Core/EntityManager.cs` (lines 207, 211)
- `Runtime/Sync/ViewPool.cs` (lines 69, 146)
- `Runtime/Modules/ModuleRegistry.cs` (lines 43, 66, 138)

**Description:**
Exception messages and log output throughout the runtime include `type.Name`, `typeof(T).Name`, `assembly.GetName().Name`, and similar type information. While using `.Name` (not `.FullName` or `.AssemblyQualifiedName`) limits the detail, it still discloses internal type naming conventions to anyone who can trigger these error paths.

For example:
```csharp
throw new InvalidOperationException($"Dependency '{pType.Name}' not registered for '{implType.Name}'");
throw new InvalidOperationException($"Circular dependency detected involving type '{serviceType.Name}'");
```

These messages could reveal the DI graph structure if an attacker can trigger resolution failures.

**Risk:** Low in the context of a game framework — this information is moderately useful for reverse engineering but not directly exploitable.

---

### Finding 7: Missing Error Handling in SystemBase Update Loop

**Severity:** MEDIUM
**File:** `Runtime/ECS/Systems/SystemBase.cs` (lines 34-38)

**Description:**
The `SystemBase.Update()` method calls `OnUpdate(deltaTime)` without any error handling. If a user-implemented system throws an exception during its update, it will propagate unhandled through the system runner, potentially crashing the entire game loop.

**Code:**
```csharp
public void Update(float deltaTime)
{
    if (!_initialized || _disposed) return;
    OnUpdate(deltaTime);  // No try/catch
}
```

Similarly, `Initialize()` (line 30) and `Dispose()` (lines 43-44) have no error handling around user-overridable virtual methods.

**Risk:** A single faulty system can take down all systems. This is not an information disclosure issue per se, but it is a reliability concern that could be exploited in a denial-of-service scenario if an attacker can influence system behavior.

**Recommendation:** Consider wrapping `OnUpdate` in a try/catch at the system runner level, with configurable behavior (disable faulting system vs. propagate).

---

### Finding 8: Exception Details Stored in Publicly Accessible Properties

**Severity:** LOW
**Files:**
- `Runtime/Sync/ComponentBinding.cs` (lines 117, 217) — `_lastError = ex.Message`
- `Runtime/Sync/EntityView.cs` (line 289) — `_lastError = ex.Message`
- `Editor/HotReload/HotReloadManager.cs` (lines 218, 249, 258) — `result.Errors.Add(ex.Message)`
- `Editor/Benchmarking/BenchmarkRunner.cs` (line 199) — `ErrorMessage = ex.Message`

**Description:**
Exception messages are stored in object properties (`LastError`, `Errors`, `ErrorMessage`) that may be read by UI code or serialized. In `ComponentBinding` and `EntityView`, the `LastError` property is publicly readable. While `ex.Message` is less revealing than `ex.ToString()`, it can still contain internal type names, file paths, or other implementation details depending on the exception source.

**Recommendation:** Sanitize error messages before storing them in publicly accessible properties, or map exceptions to predefined error codes/messages.

---

### Finding 9: HotReloadManager Aggregates and Exposes Error Details

**Severity:** LOW
**File:** `Editor/HotReload/HotReloadManager.cs` (lines 218, 231, 242, 249, 258, 261)

**Description:**
The HotReloadManager collects exception messages into a `result.Errors` list, joins them, stores them in `HotReloadState.ErrorMessage`, and logs them. The error aggregation includes service type names (`service.GetType().Name`) concatenated with exception messages. This is editor-only code, but the `HotReloadState` object could be serialized or displayed in editor UI.

**Risk:** Very low since this is editor-only code, but the pattern of aggregating raw exception messages should be noted.

---

### Finding 10: Reflection-Based Data Access with Silent Failure

**Severity:** LOW
**File:** `Editor/DataProviders/WorldDataProvider.cs` (lines 180-246)

**Description:**
The WorldDataProvider uses reflection to access private fields (`_entityVersions`, `_scheduler`, `_systems`) via `BindingFlags.NonPublic | BindingFlags.Instance`, with empty catch blocks around the reflection calls. If the internal structure of these classes changes, the data provider will silently return empty/default data without any diagnostic indication.

While this is editor-only code, it creates a maintenance hazard — internal refactoring could silently break editor tooling with no error messages to guide debugging.

## Summary Table

| # | Finding | Severity | Location | Category |
|---|---------|----------|----------|----------|
| 1 | Stack trace exposure in runtime logs | MEDIUM | GameBootstrapper.cs, ModuleBootstrapper.cs | Information Disclosure |
| 2 | Full exception object logged during DI disposal | MEDIUM | Container.cs | Information Disclosure |
| 3 | Full exception object logged in test runner | LOW | StressTestRunner.cs | Information Disclosure |
| 4 | Empty catch blocks (13 instances) | MEDIUM | ContainerBuilderExtensions.cs, WorldDataProvider.cs, and 8 others | Empty Catch Blocks |
| 5 | Over-broad `catch (Exception)` in runtime (12 instances) | LOW | Container.cs, GameBootstrapper.cs, ComponentBinding.cs, and others | Over-Broad Catching |
| 6 | Type/assembly names in exception messages | LOW | Container.cs, ContainerBuilder.cs, EventBus.cs, EntityManager.cs | Information Disclosure |
| 7 | Missing error handling in SystemBase update loop | MEDIUM | SystemBase.cs | Missing Error Handling |
| 8 | Exception details in public properties | LOW | ComponentBinding.cs, EntityView.cs, HotReloadManager.cs | Information Disclosure |
| 9 | Error detail aggregation in HotReloadManager | LOW | HotReloadManager.cs | Information Disclosure |
| 10 | Silent reflection failures in editor data providers | LOW | WorldDataProvider.cs | Empty Catch Blocks |

## Security Analysis Checklist

- [x] Information disclosure via error messages — Findings 1, 2, 3, 6, 8, 9
- [x] Stack trace exposure — Findings 1, 2
- [x] Over-broad exception catching — Finding 5
- [x] Empty catch blocks — Findings 4, 10
- [x] Missing error handling in critical paths — Finding 7
- [x] Exception type specificity — Finding 5
