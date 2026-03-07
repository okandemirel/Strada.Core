# Unit 8 — Module System Security Review

## Files Analyzed

- `Runtime/Modules/SystemRunner.cs`
- `Runtime/Modules/ModuleRegistry.cs`
- `Runtime/Modules/ServiceLocator.cs`
- `Runtime/Modules/ModuleConfig.cs`
- `Runtime/Modules/ServiceEntry.cs`
- `Runtime/Modules/SerializableType.cs`
- `Runtime/Modules/IModuleInstaller.cs`
- `Runtime/Modules/SystemAttributes.cs`
- `Runtime/Modules/ModuleBootstrapper.cs`
- `Runtime/Modules/RuntimeSystemDiscovery.cs`
- `Runtime/Modules/ModuleEntry.cs`
- `Runtime/Modules/SystemEntry.cs`
- `Runtime/Modules/IModuleBuilder.cs`
- `Runtime/Modules/ModuleBuilder.cs`
- `Runtime/Modules/IServiceLocator.cs`

## Executive Summary

The Module System provides runtime module discovery, type instantiation, and service registration for a Unity ECS framework. The primary security concerns center on unconstrained dynamic type instantiation via `Activator.CreateInstance` in `SystemRunner` and `ModuleRegistry`, type resolution from serialized strings in `SerializableType`, and broad assembly scanning without allowlisting. These issues carry limited practical risk in a typical Unity game context where all loaded assemblies are developer-controlled, but they represent defense-in-depth gaps that could be exploited if the trust boundary shifts (e.g., mod support, user-generated content, or runtime asset bundle loading from untrusted sources).

## Detailed Findings

### MOD-01: Unconstrained `Activator.CreateInstance` in SystemRunner

**Severity:** MEDIUM
**Location:** `Runtime/Modules/SystemRunner.cs`, line 265
**Category:** Dynamic type instantiation without validation

The `CreateSystem` method resolves a `Type` from a `SystemEntry` (which itself deserializes from a string via `SerializableType`) and calls `Activator.CreateInstance(systemType)` with no validation that the resolved type implements `ISystem` before instantiation. The `as ISystem` cast on the result means a non-ISystem type is silently discarded after its constructor has already executed.

```csharp
return Activator.CreateInstance(systemType) as ISystem;
```

Any type with a parameterless constructor can be instantiated. If a serialized `ModuleConfig` asset is tampered with or loaded from an untrusted asset bundle, an attacker could trigger constructor side effects of arbitrary types. The soft cast (`as`) only discards the result; it does not prevent instantiation.

**Recommendation:** Validate that `systemType` is assignable to `ISystem` before calling `Activator.CreateInstance`. Add an explicit check:
```csharp
if (!typeof(ISystem).IsAssignableFrom(systemType))
{
    Debug.LogWarning($"[SystemRunner] Type {systemType.Name} does not implement ISystem.");
    return null;
}
```

---

### MOD-02: Unconstrained `Activator.CreateInstance` in ModuleRegistry

**Severity:** MEDIUM
**Location:** `Runtime/Modules/ModuleRegistry.cs`, line 132
**Category:** Dynamic type instantiation without validation

The `DiscoverModulesInAssembly` method scans assemblies for types implementing `IModuleInstaller` and instantiates them via `Activator.CreateInstance(type)`. While the LINQ filter at lines 122-126 checks `installerType.IsAssignableFrom(t)`, the cast on line 132 assumes this is sufficient. This is structurally safer than MOD-01 because the type check precedes instantiation. However, the method scans all loaded assemblies by default (when no `assemblyFilter` is provided), which means any assembly that happens to be loaded -- including those from plugins, asset bundles, or third-party packages -- will have its `IModuleInstaller` types discovered and instantiated automatically.

```csharp
var installer = (IModuleInstaller)Activator.CreateInstance(type);
```

**Recommendation:** Apply a default assembly filter that restricts scanning to project assemblies. The `RuntimeSystemDiscovery.ShouldSkipAssembly` method already implements such a filter; consider sharing it.

---

### MOD-03: Type Resolution from Serialized Strings via `Type.GetType`

**Severity:** MEDIUM
**Location:** `Runtime/Modules/SerializableType.cs`, line 26
**Category:** Type resolution from untrusted strings

`SerializableType.Type` resolves types from `_assemblyQualifiedName` using `Type.GetType()`:

```csharp
_cachedType = Type.GetType(_assemblyQualifiedName);
```

The `_assemblyQualifiedName` field is serialized by Unity and stored in asset files (ScriptableObjects). If these assets are loaded from untrusted sources (downloaded asset bundles, user-modifiable config files), an attacker can specify arbitrary assembly-qualified type names. While `Type.GetType` itself does not execute code, the resolved type is subsequently used in `Activator.CreateInstance` calls (see MOD-01), completing a type-confusion or arbitrary-instantiation attack chain.

There is no validation, allowlist, or namespace restriction on what types can be resolved.

**Recommendation:** Add a validation step that checks the resolved type against an expected base type or interface before caching it. Consider restricting the allowed namespaces.

---

### MOD-04: Assembly Scanning Without Allowlisting in ModuleRegistry

**Severity:** LOW
**Location:** `Runtime/Modules/ModuleRegistry.cs`, lines 28-47
**Category:** Assembly scanning security

`DiscoverModules()` scans `AppDomain.CurrentDomain.GetAssemblies()` with an optional filter that defaults to null (scan everything). Unlike `RuntimeSystemDiscovery`, which skips system/Unity assemblies via `ShouldSkipAssembly`, `ModuleRegistry` has no built-in filtering. This means module installers from any loaded assembly are discovered and instantiated, including third-party libraries or dynamically loaded assemblies.

**Recommendation:** Apply a default assembly filter consistent with `RuntimeSystemDiscovery.ShouldSkipAssembly`. Alternatively, require an explicit allowlist of assembly prefixes.

---

### MOD-05: Assembly Scanning Without Allowlisting in RuntimeSystemDiscovery

**Severity:** LOW
**Location:** `Runtime/Modules/RuntimeSystemDiscovery.cs`, lines 107-131
**Category:** Assembly scanning security

`RuntimeSystemDiscovery` scans all assemblies in the current AppDomain. While it does filter out known system assemblies via `ShouldSkipAssembly`, this is a denylist approach. Any assembly not matching the denylist prefixes will be scanned and its `ISystem` types discovered. A malicious or unexpected assembly loaded at runtime would have its systems included.

**Recommendation:** Consider switching to an allowlist approach or providing a configurable assembly filter for production builds.

---

### MOD-06: Information Disclosure in Log Messages

**Severity:** LOW
**Location:** `Runtime/Modules/SystemRunner.cs`, line 256; `Runtime/Modules/ModuleRegistry.cs`, lines 43, 66, 138; `Runtime/Modules/ModuleBootstrapper.cs`, line 76
**Category:** Information disclosure in logs

Several log messages expose internal type names, assembly names, and exception messages:

- `SystemRunner.cs:256` — logs `entry.DisplayName` (type name) when a system type is null.
- `ModuleRegistry.cs:43` — logs assembly name and exception message on scan failure.
- `ModuleRegistry.cs:138` — logs type name and exception message on instantiation failure.
- `ModuleBootstrapper.cs:76` — logs exception message and full stack trace on initialization failure.

In a Unity game client, these logs go to the Unity console and potentially to log files. If log files are accessible or if the game has an in-game console, internal implementation details are exposed.

**Recommendation:** Use conditional compilation (`#if UNITY_EDITOR || DEBUG`) for verbose error logs, or strip stack traces and type details from release builds.

---

### MOD-07: Silent Exception Swallowing in ServiceLocator.TryGet

**Severity:** LOW
**Location:** `Runtime/Modules/ServiceLocator.cs`, lines 53-62
**Category:** Error handling

The `TryGet(Type, out object)` method catches all exceptions with a bare `catch` block:

```csharp
catch
{
    service = null;
    return false;
}
```

This swallows all exceptions, including `OutOfMemoryException`, `StackOverflowException`, and `ThreadAbortException`. It also silently hides resolution failures that may indicate configuration bugs.

**Recommendation:** Catch only expected exception types (e.g., `InvalidOperationException`). At minimum, log a debug-level message when resolution fails.

---

### MOD-08: Global Mutable Static State in RuntimeSystemDiscovery

**Severity:** LOW
**Location:** `Runtime/Modules/RuntimeSystemDiscovery.cs`, lines 18-19
**Category:** Global state exposure

`RuntimeSystemDiscovery` uses static mutable fields (`_cachedSystems`, `_cacheInitialized`) with public methods to read and mutate the cache (`ClearCache`, `Refresh`). This global state is not thread-safe and can be modified from any code path. In a multi-threaded scenario (e.g., async loading), concurrent access could lead to race conditions or inconsistent state.

**Recommendation:** Add thread-safety if concurrent access is possible, or document that all access must occur on the main thread.

---

### MOD-09: ModuleInfo Uses Mutable Public Setters

**Severity:** INFO
**Location:** `Runtime/Modules/ModuleRegistry.cs`, lines 281-307
**Category:** Global state exposure

The `ModuleInfo` class exposes all properties with public setters (`Installer`, `Type`, `Name`, `Priority`, `Dependencies`). Any code with a reference to the `Modules` list can mutate module metadata after registration, potentially altering initialization order or dependencies.

**Recommendation:** Make setters `internal` or use `init` accessors to prevent external mutation after construction.

---

### MOD-10: No Disposal Guard on SystemRunner Operations After Dispose

**Severity:** INFO
**Location:** `Runtime/Modules/SystemRunner.cs`
**Category:** Resource management

After `Dispose()` is called, the `_disposed` flag is set but no other methods check it. Calling `Update`, `LateUpdate`, `FixedUpdate`, or `AddSystem` after disposal will either iterate empty lists silently or re-add systems that cannot be properly cleaned up.

**Recommendation:** Add `ObjectDisposedException` checks in public methods, or at minimum in `AddSystem` and `Initialize`.

## Summary Table

| ID | Finding | Severity | Location | Category |
|---|---|---|---|---|
| MOD-01 | Unconstrained `Activator.CreateInstance` in SystemRunner | MEDIUM | SystemRunner.cs:265 | Dynamic type instantiation |
| MOD-02 | Unconstrained `Activator.CreateInstance` in ModuleRegistry | MEDIUM | ModuleRegistry.cs:132 | Dynamic type instantiation |
| MOD-03 | Type resolution from serialized strings via `Type.GetType` | MEDIUM | SerializableType.cs:26 | Type resolution from untrusted strings |
| MOD-04 | Assembly scanning without allowlisting in ModuleRegistry | LOW | ModuleRegistry.cs:28-47 | Assembly scanning security |
| MOD-05 | Assembly scanning denylist approach in RuntimeSystemDiscovery | LOW | RuntimeSystemDiscovery.cs:107-131 | Assembly scanning security |
| MOD-06 | Internal type/assembly names in log messages | LOW | Multiple files | Information disclosure |
| MOD-07 | Silent exception swallowing in ServiceLocator.TryGet | LOW | ServiceLocator.cs:53-62 | Error handling |
| MOD-08 | Global mutable static state in RuntimeSystemDiscovery | LOW | RuntimeSystemDiscovery.cs:18-19 | Global state exposure |
| MOD-09 | ModuleInfo mutable public setters | INFO | ModuleRegistry.cs:281-307 | Global state exposure |
| MOD-10 | No disposal guard on SystemRunner post-Dispose operations | INFO | SystemRunner.cs | Resource management |
