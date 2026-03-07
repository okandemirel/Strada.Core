# Security Review: Unit 15 -- Editor Tools (Benchmarking, Profiling, HotReload, etc.)

**Reviewer:** Claude (automated security review)
**Date:** 2026-03-07
**Scope:** Editor/Benchmarking/, Editor/Profiling/, Editor/HotReload/, Editor/Analyzers/, Editor/Graph/, Editor/DataProviders/, Editor/StradaContextMenus.cs

---

## Executive Summary

This unit covers Unity Editor tooling for benchmarking, profiling, hot reload, dependency graph visualization, data providers, and context menus. Because all code runs exclusively within the Unity Editor (not in production builds), the overall risk profile is reduced. However, several issues were identified related to path traversal in file I/O, unsafe DateTime parsing, unrestricted file deletion, information disclosure through benchmark data, and heavy use of reflection to access private fields. Most findings are LOW or INFO severity given the editor-only context, but two MEDIUM findings warrant attention.

---

## Detailed Findings

### Finding 1: Path Traversal in BenchmarkPersistence -- User-Supplied Directory

**Severity:** MEDIUM
**File:** `Editor/Benchmarking/BenchmarkPersistence.cs`
**Lines:** 33-53 (SaveSession), 60-76 (LoadSession), 82-92 (GetSavedSessions), 126-142 (DeleteSession), 147-161 (ExportSession)

**Description:**
`SaveSession`, `LoadSession`, `DeleteSession`, and `ExportSession` all accept user-supplied path parameters without any validation or sanitization. The `directory` parameter in `SaveSession` and `GetSavedSessions` is passed directly to `Path.Combine` and file system operations. The `path` parameter in `LoadSession`, `DeleteSession`, and `ExportSession` is used directly with `File.ReadAllText`, `File.Delete`, and `File.WriteAllText`.

While the `SaveSession` filename is constructed from a timestamp and session ID (which limits injection via filename), the `directory` parameter itself could contain path traversal sequences (e.g., `../../`), and the `ExportSession` and `DeleteSession` methods accept a fully arbitrary path.

**Risk:** An attacker with access to the Unity Editor could potentially read, write, or delete files outside the intended benchmark directory.

**Recommendation:**
- Validate that resolved paths remain within the project directory using `Path.GetFullPath` and prefix checking.
- Consider restricting `DeleteSession` to only delete files within the default benchmark results directory.

```csharp
// Example validation
var resolvedPath = Path.GetFullPath(path);
var allowedRoot = Path.GetFullPath(GetDefaultDirectory());
if (!resolvedPath.StartsWith(allowedRoot))
    throw new ArgumentException("Path outside allowed directory");
```

---

### Finding 2: DateTime.Parse Without Invariant Culture or Exact Format

**Severity:** LOW
**File:** `Editor/Benchmarking/BenchmarkPersistence.cs`
**Lines:** 197, 253

**Description:**
`DateTime.Parse(timestamp)` is called without specifying `CultureInfo.InvariantCulture` or using `DateTime.ParseExact`. The timestamp is serialized using the round-trip format specifier `"o"` (ISO 8601), but parsed with the default culture-sensitive `DateTime.Parse`.

On machines with non-standard culture settings, this could cause parsing failures or subtle date misinterpretation. While not a direct security vulnerability, malformed timestamp strings in a crafted JSON file could cause exceptions or unexpected behavior.

**Recommendation:**
Use `DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)` or `DateTime.ParseExact(timestamp, "o", CultureInfo.InvariantCulture)`.

---

### Finding 3: Unrestricted File Deletion in DeleteSession

**Severity:** MEDIUM
**File:** `Editor/Benchmarking/BenchmarkPersistence.cs`
**Lines:** 126-142

**Description:**
`DeleteSession(string path)` accepts an arbitrary file path and deletes whatever file exists at that path. There is no validation that the path points to a benchmark session file, that it resides within the benchmark results directory, or that it has the expected `.json` extension.

Combined with Finding 1, this means any caller can delete arbitrary files accessible to the Unity process.

**Recommendation:**
- Validate that the path is within the benchmark results directory.
- Check that the filename matches the expected pattern (`benchmark_session_*.json`).

---

### Finding 4: Information Disclosure via Benchmark Results

**Severity:** LOW
**File:** `Editor/Benchmarking/BenchmarkPersistence.cs`, `Editor/Benchmarking/BenchmarkModels.cs`
**Lines:** Various

**Description:**
Benchmark results are saved as unencrypted JSON files in a project-relative directory (`BenchmarkResults/`). These files contain:
- Unity version and platform information
- Performance timing data (DI resolution times, ECS query times, message bus throughput)
- Memory allocation data

If the project directory is shared (e.g., via version control), this data could reveal internal architectural details and performance characteristics to unintended parties.

**Recommendation:**
- Add `BenchmarkResults/` to `.gitignore` by default.
- Consider documenting this behavior so developers are aware of what gets persisted.

---

### Finding 5: Extensive Reflection Access to Private Fields

**Severity:** LOW
**File:** `Editor/Profiling/SystemProfilerHook.cs` (lines 116-117), `Editor/DataProviders/ContainerDataProvider.cs` (lines 96-101), `Editor/DataProviders/BusDataProvider.cs` (lines 279-298, 355-371), `Editor/DataProviders/WorldDataProvider.cs` (lines 180-181, 220-226), `Editor/DataProviders/ModuleDataProvider.cs` (lines 146-147)

**Description:**
Multiple editor tools use reflection with `BindingFlags.NonPublic | BindingFlags.Instance` to access private fields of runtime classes (`Container`, `EventBus`, `EntityManager`, `SystemScheduler`, `GameBootstrapper`). Field names are hardcoded strings (e.g., `_registeredTypes`, `_lifetimes`, `_singletons`, `_commandHandlers`, `_eventChannels`, `_systemsByPhase`, `_entityVersions`, `_gameConfig`).

This creates a tight coupling to internal implementation details. If the runtime classes are refactored (field renamed or removed), the editor tools will silently fail or return incorrect data. While not a direct security vulnerability, this pattern can mask errors and produce misleading diagnostic information.

**Recommendation:**
- Consider adding explicit editor/debug APIs to runtime classes that expose the needed data through a stable interface rather than relying on reflection.
- Add runtime checks that log warnings when expected fields are not found (some methods already do this).

---

### Finding 6: JSON Deserialization Without Schema Validation

**Severity:** LOW
**File:** `Editor/Benchmarking/BenchmarkPersistence.cs` (line 68), `Editor/HotReload/EntityStatePreserver.cs` (line 140), `Editor/HotReload/HotReloadManager.cs` (line 303)

**Description:**
`JsonUtility.FromJson<T>()` and `JsonUtility.FromJsonOverwrite()` are used to deserialize JSON data without any schema validation. Unity's `JsonUtility` is relatively safe (it does not support polymorphic deserialization or arbitrary type instantiation like `Newtonsoft.Json` with `TypeNameHandling`), so this is not a critical deserialization vulnerability.

However, a crafted JSON file loaded via `LoadSession` could contain unexpected values (e.g., negative iterations, NaN timing values) that might cause downstream issues in the benchmark comparison UI.

**Recommendation:**
- Add basic validation of deserialized values (e.g., non-negative iterations, finite timing values).

---

### Finding 7: EntityStatePreserver Type Resolution via Assembly Scanning

**Severity:** LOW
**File:** `Editor/HotReload/EntityStatePreserver.cs`
**Lines:** 172-196

**Description:**
`FindComponentType` iterates over all loaded assemblies via `AppDomain.CurrentDomain.GetAssemblies()` and resolves types by full name. The result is cached in a static dictionary. While the method validates that the resolved type implements `IComponent`, the type name comes from the serialized snapshot which could be manipulated.

In the editor-only context, this is low risk. The method correctly constrains results to `IComponent` implementations.

**Recommendation:**
No immediate action required. The `IComponent` check provides adequate safety. Consider logging a warning if a type name is found but does not implement `IComponent`.

---

### Finding 8: StradaContextMenus Path Operations

**Severity:** INFO
**File:** `Editor/StradaContextMenus.cs`
**Lines:** 76-88, 130-152

**Description:**
`GenerateControllerForView` constructs file paths using `Path.GetDirectoryName` and string replacement (`Replace("Views", "Controllers")`). The paths originate from `AssetDatabase.GetAssetPath`, which returns Unity-managed asset paths and is not user-supplied in the traditional sense.

`ValidateModuleStructure` uses `Path.Combine` with folder paths from `GetSelectedFolderPath()`, which also returns Unity asset paths. The `File.Exists` and `Directory.Exists` checks are read-only operations.

**Risk:** Minimal. All paths are derived from Unity's asset database, not from direct user input.

---

### Finding 9: Regex Construction from User Pattern in BusDataProvider

**Severity:** LOW
**File:** `Editor/DataProviders/BusDataProvider.cs`
**Lines:** 154-175

**Description:**
`MatchesTypePattern` converts wildcard patterns (`*`, `?`) to regex. The method uses `Regex.Escape` before replacing wildcards, and wraps the regex construction in a try-catch. However, there is no timeout on the regex match, which could allow ReDoS with carefully crafted patterns in theory.

In practice, the input comes from editor UI filter fields, and the matched strings are short type names, making exploitation extremely unlikely.

**Recommendation:**
Consider adding `RegexOptions.None` with a `TimeSpan` timeout parameter to the `Regex.IsMatch` call for defense-in-depth.

---

### Finding 10: Silent Exception Swallowing in Data Providers

**Severity:** INFO
**File:** `Editor/DataProviders/WorldDataProvider.cs` (lines 44-47, 113-115, 188, 208-209, 242-243), `Editor/DataProviders/BusDataProvider.cs` (lines 193-197, 301-303), `Editor/DataProviders/ContainerDataProvider.cs` (lines 158-161)

**Description:**
Multiple data provider methods use empty `catch` blocks or `catch { }` that silently swallow exceptions. While this prevents editor crashes from propagating, it also hides potential issues and makes debugging difficult.

**Recommendation:**
At minimum, log a debug-level warning in catch blocks to aid troubleshooting.

---

## Summary Table

| # | Finding | Severity | File(s) | Category |
|---|---------|----------|---------|----------|
| 1 | Path traversal in BenchmarkPersistence (user-supplied directory) | MEDIUM | BenchmarkPersistence.cs | File I/O |
| 2 | DateTime.Parse without invariant culture | LOW | BenchmarkPersistence.cs | Input Validation |
| 3 | Unrestricted file deletion in DeleteSession | MEDIUM | BenchmarkPersistence.cs | File I/O |
| 4 | Information disclosure via benchmark results | LOW | BenchmarkPersistence.cs, BenchmarkModels.cs | Info Disclosure |
| 5 | Extensive reflection access to private fields | LOW | Multiple DataProviders, SystemProfilerHook.cs | Code Quality |
| 6 | JSON deserialization without schema validation | LOW | BenchmarkPersistence.cs, EntityStatePreserver.cs, HotReloadManager.cs | Deserialization |
| 7 | Type resolution via assembly scanning | LOW | EntityStatePreserver.cs | Type Safety |
| 8 | Path operations in StradaContextMenus | INFO | StradaContextMenus.cs | File I/O |
| 9 | Regex from user pattern without timeout | LOW | BusDataProvider.cs | Input Validation |
| 10 | Silent exception swallowing | INFO | Multiple DataProviders | Code Quality |

### Severity Distribution
- **CRITICAL:** 0
- **HIGH:** 0
- **MEDIUM:** 2
- **LOW:** 6
- **INFO:** 2
