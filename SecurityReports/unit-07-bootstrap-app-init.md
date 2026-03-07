# Unit 7 — Bootstrap & Application Initialization: Security Review

## Files Reviewed

| File | Path |
|------|------|
| GameBootstrapper.cs | `Runtime/Bootstrap/GameBootstrapper.cs` |
| GameBootstrapperConfig.cs | `Runtime/Bootstrap/GameBootstrapperConfig.cs` |
| ModuleBootstrapper.cs | `Runtime/Modules/ModuleBootstrapper.cs` |
| ModuleEntry.cs | `Runtime/Modules/ModuleEntry.cs` |
| ServiceLocator.cs | `Runtime/Modules/ServiceLocator.cs` |
| PlayerLoop.cs | `Runtime/Core/PlayerLoop.cs` |
| GameBootstrapperConfigEditor.cs | `Editor/Inspectors/GameBootstrapperConfigEditor.cs` |

## Executive Summary

The bootstrap and initialization layer provides a phased startup sequence for the Strada framework. The primary security concerns center on mutable global static state that any code can overwrite, incomplete resource cleanup when initialization fails partway through, and stack trace disclosure in error logging. The initialization phasing itself is well-structured with validation, but several gaps in failure handling could leave the framework in an inconsistent state.

## Detailed Findings

### Finding 1: Mutable Global Static State — Container and Service Locator

**Severity:** HIGH
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 42, 47, 52, 57

```csharp
public static IContainer Container { get; private set; }
public static IServiceLocator Services { get; private set; }
public static ECS.World.World World { get; private set; }
public static SystemRunner Systems { get; private set; }
```

**Description:**
Four static properties with `private set` expose the DI container, service locator, ECS world, and system runner as globally accessible singletons. While `private set` restricts direct assignment from outside the class, the objects themselves are mutable. Any code with a reference to `GameBootstrapper.Container` can resolve arbitrary services, register new types (if `IContainer` exposes registration methods), or manipulate global framework state.

**Risks:**
- Any loaded module, third-party code, or injected system can access the full DI container and resolve services it should not have access to.
- No access control or scoping exists — there is no distinction between "framework internals" and "game code" access levels.
- If multiple `GameBootstrapper` instances exist (e.g., across scene loads), the last one to initialize silently overwrites the static references, causing previously-resolved references to become stale.

**Recommendation:**
Consider making the static accessors internal rather than public, and use `[assembly: InternalsVisibleTo]` to restrict access to framework assemblies. Add a guard against multiple bootstrapper instances (singleton enforcement). For module code, prefer injecting `IServiceLocator` through the initialization API rather than relying on the static accessor.

---

### Finding 2: No Singleton Enforcement — Multiple Bootstrapper Instances

**Severity:** HIGH
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 188–189, 246–249

**Description:**
`GameBootstrapper` is a `MonoBehaviour` that writes to static properties during `BuildContainer()` (lines 188–189) and again during `CompleteInitialization()` (lines 246–249). There is no check to prevent a second `GameBootstrapper` from being instantiated. If two exist:

1. Both call `Awake()` and begin `InitializeAsync()`.
2. Both build separate containers and worlds.
3. The second one overwrites `Container`, `Services`, `World`, and `Systems`.
4. Code referencing the first bootstrapper's container now silently resolves from a stale or disposed container.

The static state is also set twice — once in `BuildContainer()` and once in `CompleteInitialization()` — which means there is a window during phases 3–5 where the static references point to the partially-initialized container from the current instance, while the instance-level fields may not yet be fully set up.

**Recommendation:**
Add a static instance guard in `Awake()`:
```csharp
private static GameBootstrapper _instance;

private void Awake()
{
    if (_instance != null && _instance != this)
    {
        Debug.LogError("[GameBootstrapper] Duplicate instance detected. Destroying.");
        Destroy(gameObject);
        return;
    }
    _instance = this;
    // ... rest of Awake
}
```
Also, consolidate the static property assignment to occur only once, in `CompleteInitialization()`, and remove the redundant assignment in `BuildContainer()`.

---

### Finding 3: Stack Trace Disclosure in Error Logging

**Severity:** MEDIUM
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 266, 273; `Runtime/Modules/ModuleBootstrapper.cs`, line 76

```csharp
// GameBootstrapper.cs:266
Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}\n{ex.StackTrace}");

// GameBootstrapper.cs:273
Debug.LogError($"[GameBootstrapper] Initialization failed: {ex.Message}\n{ex.StackTrace}");

// ModuleBootstrapper.cs:76
Debug.LogError($"[{GetType().Name}] Module initialization failed: {ex.Message}\n{ex.StackTrace}");
```

**Description:**
Full stack traces including method names, file paths, and line numbers are logged to `Debug.LogError`. In Unity, `Debug.Log` output can appear in:
- The Unity console (development builds)
- Player log files on disk
- Crash reporting services
- Screen overlays in development builds

Stack traces can reveal internal architecture, class names, method signatures, file system paths (including developer usernames and project structure), and dependency versions.

**Risks:**
- Information disclosure of internal framework structure to end users or attackers who gain access to log files.
- In release builds, stack traces may contain paths from the build machine.

**Recommendation:**
Gate verbose error logging behind a debug/development check:
```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}\n{ex.StackTrace}");
#else
    Debug.LogError($"[GameBootstrapper] {phaseName} failed: {ex.Message}");
#endif
```

---

### Finding 4: Incomplete Resource Cleanup on Partial Initialization Failure

**Severity:** HIGH
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 109–168, 278–323

**Description:**
If initialization fails during phases 3–5, `HandleInitializationError` is called, which logs the error, fires the failure event, and sets `_isInitialized = false`. However, it does not clean up resources that were successfully created in earlier phases:

- **Phase 2 fails:** Container is built but never disposed if phases 3–5 fail. `_container`, `_serviceLocator`, `_sharedEventBus`, and `_sharedHandleRegistry` remain allocated.
- **Phase 3 fails:** The container from phase 2, plus a partially-constructed world and system runner, are never disposed.
- **Phase 4 fails:** Some modules have been initialized (added to `_initializedModuleConfigs`), but `Shutdown()` is never called because `_isInitialized` is `false`, which is the guard condition in `Shutdown()` (line 280).
- **Phase 5 fails:** Same issue — initialized modules are not shut down.

The `Shutdown()` method only executes if `_isInitialized` is `true`, so a failed initialization leaves all partially-created resources alive until the `GameObject` is destroyed — and even then, `OnDestroy` calls `Shutdown()` which will immediately return due to the `_isInitialized` check.

Additionally, the static references `Container` and `Services` are set during `BuildContainer()` (phase 2), so even after a failure, they point to live objects that may be in an inconsistent state.

**Recommendation:**
Add cleanup logic to `HandleInitializationError`:
```csharp
private void HandleInitializationError(Exception ex)
{
    Debug.LogError($"[GameBootstrapper] Initialization failed: {ex.Message}");
    OnInitializationFailed?.Invoke(ex);
    _isInitialized = false;

    // Clean up partially-initialized resources
    CleanupPartialInitialization();
}

private void CleanupPartialInitialization()
{
    // Shut down any initialized modules in reverse order
    for (int i = _initializedModuleConfigs.Count - 1; i >= 0; i--)
    {
        try { _initializedModuleConfigs[i].Shutdown(); }
        catch (Exception ex) { Debug.LogError($"Cleanup error: {ex.Message}"); }
    }
    _initializedModuleConfigs.Clear();

    _systemRunner?.Dispose();
    _world?.Dispose();
    if (_container is IDisposable d) d.Dispose();

    Container = null;
    Services = null;
    World = null;
    Systems = null;
}
```

---

### Finding 5: Race Condition in Async Module Initialization

**Severity:** MEDIUM
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 208–231

```csharp
private IEnumerator InitializeModulesAsync()
{
    foreach (var module in _gameConfig.GetEnabledModules())
    {
        // ... initialize module ...
        if (_gameConfig.AsyncInitialization)
        {
            yield return null; // Yields for one frame
        }
    }
}
```

**Description:**
When `AsyncInitialization` is enabled, the coroutine yields between module initializations (line 229). During this yield, other code can run — including `Update()`, `LateUpdate()`, and `FixedUpdate()` on this same `MonoBehaviour`. These lifecycle methods check `_isInitialized` which is still `false`, so the system runner is not ticked. However:

1. The static `Container` and `Services` properties are already set (from phase 2) and point to a live container.
2. Other scripts running during the yield frame can access `GameBootstrapper.Container` or `GameBootstrapper.Services` and resolve services from modules that have not yet been initialized.
3. If a module's `Initialize` call has side effects that depend on ordering (e.g., setting up event handlers), accessing the container between yields can produce incorrect behavior.

**Recommendation:**
Defer setting static properties until `CompleteInitialization()` and remove the early assignment in `BuildContainer()`. Consider adding a `IsInitializing` state that calling code can check.

---

### Finding 6: Unvalidated OnInitializationFailed Event Subscribers

**Severity:** LOW
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 72, 274

```csharp
public event Action<Exception> OnInitializationFailed;
// ...
OnInitializationFailed?.Invoke(ex);
```

**Description:**
The `OnInitializationFailed` event passes the full `Exception` object to all subscribers. Any subscriber receives the complete exception including message, stack trace, inner exceptions, and custom data. If a subscriber logs this to an analytics service or displays it in a UI, it becomes an information disclosure vector.

Additionally, if a subscriber itself throws an exception, it will propagate uncaught and could mask the original initialization error.

**Recommendation:**
Wrap the event invocation in a try-catch to prevent subscriber exceptions from masking the original error. Consider passing a more restricted error type rather than the full `Exception` object.

---

### Finding 7: Verbose Logging Enabled by Default

**Severity:** LOW
**Location:** `Runtime/Bootstrap/GameBootstrapperConfig.cs`, line 24

```csharp
[SerializeField] private bool _verboseLogging = true;
```

**Description:**
Verbose logging is enabled by default in the configuration ScriptableObject. This means that unless explicitly disabled, all bootstrap phases, module names, system counts, and other internal details are logged via `Debug.Log`. In production builds, this information persists in log files.

**Recommendation:**
Default `_verboseLogging` to `false`. Developers who need it can enable it explicitly. Alternatively, tie it to `DEVELOPMENT_BUILD` or `UNITY_EDITOR` preprocessor defines.

---

### Finding 8: PlayerLoop Static State Not Cleaned on Domain Reload

**Severity:** MEDIUM
**Location:** `Runtime/Core/PlayerLoop.cs`, lines 16–22

```csharp
private static readonly List<Action<float>> _updateCallbacks = new(16);
private static readonly List<Action<float>> _lateUpdateCallbacks = new(8);
private static readonly List<Action<float>> _fixedUpdateCallbacks = new(8);
private static readonly List<Action> _initCallbacks = new(8);
private static bool _initialized;
private static bool _disposed;
```

**Description:**
`PlayerLoop` maintains static mutable state including callback lists and initialization flags. In the Unity Editor with domain reload disabled (Enter Play Mode Settings), these statics persist across play sessions. If `Shutdown()` is not called (e.g., due to an initialization failure in `GameBootstrapper`), stale callbacks from a previous play session remain registered and will be invoked, potentially calling methods on destroyed or stale objects.

**Recommendation:**
Add a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` handler to reset all static state:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
{
    _updateCallbacks.Clear();
    _lateUpdateCallbacks.Clear();
    _fixedUpdateCallbacks.Clear();
    _initCallbacks.Clear();
    _initialized = false;
    _disposed = false;
}
```

---

### Finding 9: Missing Null Guard on GetDebugInfo

**Severity:** LOW
**Location:** `Runtime/Bootstrap/GameBootstrapper.cs`, lines 355–373

**Description:**
`GetDebugInfo()` is a public method that exposes internal state including initialization status, enabled module count, and system runner debug info. While useful for development, it is accessible at runtime and could be called by any code to gather information about the framework's internal state.

**Recommendation:**
Consider guarding with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` or making it internal.

---

### Finding 10: Swallowed Exceptions in ServiceLocator.TryGet

**Severity:** LOW
**Location:** `Runtime/Modules/ServiceLocator.cs`, lines 53–62

```csharp
public bool TryGet(Type serviceType, out object service)
{
    // ...
    try
    {
        service = _container.Resolve(serviceType);
        return service != null;
    }
    catch
    {
        service = null;
        return false;
    }
}
```

**Description:**
The non-generic `TryGet` overload catches all exceptions silently with a bare `catch` clause. This swallows not only resolution failures but also unexpected errors such as `OutOfMemoryException`, `StackOverflowException`, or bugs in factory methods. This makes diagnosing container configuration issues difficult.

**Recommendation:**
Catch only expected exception types (e.g., `InvalidOperationException` or a custom `ResolutionException`), and let unexpected exceptions propagate.

## Summary Table

| # | Finding | Severity | Location | Category |
|---|---------|----------|----------|----------|
| 1 | Mutable global static state for Container/Services/World/Systems | HIGH | GameBootstrapper.cs:42,47,52,57 | Global State Exposure |
| 2 | No singleton enforcement — duplicate bootstrapper overwrites statics | HIGH | GameBootstrapper.cs:188-189,246-249 | Initialization Safety |
| 3 | Stack trace disclosure in error logging | MEDIUM | GameBootstrapper.cs:266,273; ModuleBootstrapper.cs:76 | Information Disclosure |
| 4 | No resource cleanup on partial initialization failure | HIGH | GameBootstrapper.cs:109-168,278-323 | Resource Cleanup |
| 5 | Race condition during async module initialization with early static assignment | MEDIUM | GameBootstrapper.cs:188-189,208-231 | Race Condition |
| 6 | OnInitializationFailed passes full Exception to subscribers | LOW | GameBootstrapper.cs:72,274 | Information Disclosure |
| 7 | Verbose logging enabled by default | LOW | GameBootstrapperConfig.cs:24 | Information Disclosure |
| 8 | PlayerLoop static state not cleaned on domain reload | MEDIUM | PlayerLoop.cs:16-22 | Stale State |
| 9 | GetDebugInfo exposes internal state publicly | LOW | GameBootstrapper.cs:355-373 | Information Disclosure |
| 10 | Bare catch clause swallows all exceptions in ServiceLocator.TryGet | LOW | ServiceLocator.cs:53-62 | Exception Handling |
