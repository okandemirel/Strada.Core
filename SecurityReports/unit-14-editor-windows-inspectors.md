# Security Review: Unit 14 — Editor Windows & Inspectors

## Review Metadata
- **Unit:** 14
- **Reviewer:** Claude (Automated Security Analysis)
- **Date:** 2026-03-07
- **Scope:** `Editor/Windows/`, `Editor/Inspectors/`, `Editor/PropertyDrawers/`
- **Files Reviewed:** 14 files

## Executive Summary

This unit covers Unity Editor tooling: custom editor windows, inspector drawers, and property drawers for the Strada ECS framework. Because all code in this unit runs exclusively within the Unity Editor (not in player builds), the overall risk profile is lower than runtime code. However, several patterns were identified that could lead to editor instability, information disclosure during development, or unexpected behavior if the editor environment is compromised.

The most notable findings are: unconstrained `Activator.CreateInstance` usage, multiple `Type.GetType` calls from serialized strings without validation, broad exception swallowing that hides failures, reflection-based access to private fields across multiple windows, and potential regex denial-of-service in the Bus Debugger filter.

## Detailed Findings

### Finding 1: Dynamic Type Instantiation Without Constraint Verification

- **Severity:** MEDIUM
- **Location:** `Editor/Windows/StradaEntityInspectorWindow.cs`, line 855
- **Code:**
  ```csharp
  var component = Activator.CreateInstance(componentType);
  ```
- **Description:** `Activator.CreateInstance` is called on `componentType` which originates from the `_availableComponentTypes` list. While the list is populated via `CacheComponentTypes()` which filters for types implementing `IComponent` that are value types and pass `IsUnmanagedType()` checks, the type list is cached once and never re-validated before instantiation. If the cached list becomes stale or is manipulated (e.g., via reflection from another editor extension), arbitrary types could be instantiated.
- **Mitigating Factors:** The `IsValidComponentType` method (line 815) checks for `IComponent` interface, non-abstract, value type, and unmanaged constraints. The code runs only in the editor during play mode.
- **Recommendation:** Re-validate `componentType` against `IsValidComponentType` immediately before `Activator.CreateInstance` at line 855 as a defense-in-depth measure.

### Finding 2: Type.GetType from Serialized Strings — SerializableTypeDrawer

- **Severity:** LOW
- **Location:** `Editor/PropertyDrawers/SerializableTypeDrawer.cs`, line 27
- **Code:**
  ```csharp
  var currentType = string.IsNullOrEmpty(currentTypeName) ? null : Type.GetType(currentTypeName);
  ```
- **Description:** `Type.GetType` is called with a string value read directly from a serialized property (`_assemblyQualifiedName`). An attacker with write access to Unity asset files could craft a malicious assembly-qualified name that resolves to an unexpected type. In the editor context, this is used only for display purposes (showing the type name in a dropdown), so the immediate risk is limited to UI confusion or errors.
- **Mitigating Factors:** The result is used only for display; no instantiation occurs at this call site. Serialized properties are under developer control. Editor-only code.
- **Recommendation:** Add a null-check and type constraint validation after resolution to ensure the resolved type matches expected base type constraints.

### Finding 3: Type.GetType from Serialized Strings — SystemEntryDrawer

- **Severity:** LOW
- **Location:** `Editor/PropertyDrawers/SystemEntryDrawer.cs`, lines 50 and 208
- **Code:**
  ```csharp
  var type = System.Type.GetType(assemblyQualifiedNameProp.stringValue);
  typeName = type?.Name ?? "(Invalid Type)";
  ```
- **Description:** Same pattern as Finding 2, appearing in two places within `SystemEntryDrawer` (for `SystemEntry` at line 50 and for `ServiceEntry` at line 208). Types are resolved from serialized strings for display purposes only.
- **Mitigating Factors:** Used only for rendering type names in the inspector UI. No instantiation or invocation occurs.
- **Recommendation:** Validate that resolved types conform to expected interfaces (`ISystem` for systems, appropriate interfaces for services).

### Finding 4: Broad Exception Swallowing Across Multiple Files

- **Severity:** LOW
- **Location:** Multiple locations:
  - `StradaEntityInspectorWindow.cs`, lines 650-653 (DrawField catch block)
  - `StradaEntityInspectorWindow.cs`, lines 807-809 (CacheComponentTypes catch block)
  - `StradaEntityInspectorWindow.cs`, line 777 (GetEntityVersion catch block)
  - `StradaEntityInspectorWindow.cs`, line 991 (DrawComponentMemoryRow catch block)
  - `StradaConfigDataManagerWindow.cs`, line 111 (CacheConfigTypes catch block)
  - `BusDebuggerWindow.cs`, line 375 (RefreshGraphData catch block)
- **Code:**
  ```csharp
  catch { }
  ```
- **Description:** Empty catch blocks silently swallow all exceptions, including security-relevant ones (e.g., `SecurityException`, `TypeLoadException`). This could mask issues during development, such as assembly loading failures caused by tampered DLLs, or type resolution errors that indicate corrupted assets.
- **Recommendation:** At minimum, log caught exceptions at debug level. For security-critical operations (type loading, reflection), log at warning level to help developers notice anomalies.

### Finding 5: Extensive Reflection Access to Private Fields

- **Severity:** LOW
- **Location:** Multiple locations:
  - `StradaEntityInspectorWindow.cs`, lines 768-769, 885-886, 965-979 (accessing `_entityVersions`, `_storages`, `_sparseSet`, `_dense`)
  - `SystemProfilerWindow.cs`, lines 501-502 (accessing `_systemsByPhase`)
  - `ReactivePropertyDrawer.cs`, lines 157-160 (accessing properties via reflection)
  - `EntityMediatorInspector.cs`, lines 79, 100-101 (Type.GetType for registry, field scanning)
- **Description:** Multiple editor tools use reflection to access private/internal fields of runtime types. This creates tight coupling between editor tools and runtime implementation details. If runtime field names change, editor tools fail silently (due to empty catch blocks). More importantly, reflection bypasses access controls, and the pattern of scanning all fields by name substring (e.g., `field.FieldType.Name.Contains("Mediator")` at EntityMediatorInspector line 103) could match unintended fields.
- **Mitigating Factors:** Editor-only code. Reflection in Unity Editor tooling is a common and accepted pattern.
- **Recommendation:** Consider adding explicit editor-facing APIs to runtime types (e.g., `[EditorAccessible]` properties or a dedicated `IEditorInspectable` interface) rather than relying on reflection of private members. For field scanning by name, use more specific matching criteria.

### Finding 6: Potential ReDoS in Bus Debugger Type Filter

- **Severity:** LOW
- **Location:** `Editor/Windows/BusDebuggerWindow.cs`, lines 810-821
- **Code:**
  ```csharp
  var regexPattern = "^" + Regex.Escape(pattern)
      .Replace("\\*", ".*")
      .Replace("\\?", ".") + "$";
  return Regex.IsMatch(typeName, regexPattern, RegexOptions.IgnoreCase);
  ```
- **Description:** User-provided filter patterns are converted to regex. While `Regex.Escape` is applied first (mitigating direct regex injection), the `\\*` to `.*` replacement creates potentially unbounded quantifiers. A pattern like `*****` would generate `.*.*.*.*.*` which can cause catastrophic backtracking on certain inputs. The catch block falls back to `IndexOf`, which mitigates the impact, but the regex engine may still consume significant CPU before throwing.
- **Mitigating Factors:** The fallback in the catch block prevents complete failure. This is editor-only UI filtering with user-typed input. The `Regex.Escape` call prevents most injection.
- **Recommendation:** Add a timeout to the regex evaluation using `Regex.IsMatch` with a `TimeSpan` parameter, or limit the number of wildcard characters allowed in the pattern.

### Finding 7: Information Disclosure in Editor Windows

- **Severity:** INFO
- **Location:** Multiple windows
  - `StradaEntityInspectorWindow.cs` — Exposes entity IDs, versions, component data, internal field values, memory layout
  - `SystemProfilerWindow.cs` — Exposes full type names (`FullName`) of all systems, internal execution timing
  - `BusDebuggerWindow.cs` — Exposes message payloads, subscriber details (target types and method names)
  - `StradaDashboardWindow.cs` — Aggregates DI container registrations, ECS world state, module configurations
  - `TimeMachineWindow.cs` — Records and stores complete world state snapshots in memory
- **Description:** Editor windows expose detailed internal architecture information including type names, method names, field values, memory usage patterns, and execution timings. While this is the intended purpose of debug tooling, the information could be valuable to an attacker who gains access to a developer's machine or screen-shares.
- **Mitigating Factors:** All code is editor-only and stripped from player builds. This is standard and expected behavior for debug/profiling tooling.
- **Recommendation:** No action required for typical use. For sensitive projects, consider adding an optional access control mechanism (e.g., requiring a developer to explicitly enable detailed inspection).

### Finding 8: World State Manipulation via Time Machine

- **Severity:** INFO
- **Location:** `Editor/Windows/TimeMachineWindow.cs`, lines 279-283 (RestoreSnapshot), lines 264-277 (RecordSnapshot)
- **Description:** The Time Machine captures and restores complete ECS world states, including all entity data. While this is the intended functionality, it means the editor window has the ability to silently replace all runtime game state with previously recorded data. The snapshots are stored in-memory without any integrity verification. A compromised editor extension could theoretically manipulate snapshot data before restoration.
- **Mitigating Factors:** Editor-only, play-mode-only functionality. Snapshots are stored as in-memory objects with no persistence to disk (no serialization attack surface). Standard Unity editor workflow.
- **Recommendation:** No action required for typical use.

### Finding 9: Duplicate Code and UI Elements

- **Severity:** INFO
- **Location:** `Editor/Windows/StradaEntityInspectorWindow.cs`
  - Lines 171-172: `DrawToolbar()` called twice
  - Lines 254-261: Duplicate "Refresh" button
- **Description:** `DrawToolbar()` is called twice in `OnGUI()` and there are two identical "Refresh" buttons in the toolbar. While not a security vulnerability, this indicates potential copy-paste errors that could mask other issues or cause confusion.
- **Recommendation:** Remove duplicate calls and buttons.

## Summary Table

| # | Finding | Severity | Location | Category |
|---|---------|----------|----------|----------|
| 1 | Activator.CreateInstance without re-validation | MEDIUM | StradaEntityInspectorWindow.cs:855 | Dynamic Type Instantiation |
| 2 | Type.GetType from serialized string | LOW | SerializableTypeDrawer.cs:27 | Type Resolution |
| 3 | Type.GetType from serialized string (2 sites) | LOW | SystemEntryDrawer.cs:50,208 | Type Resolution |
| 4 | Empty catch blocks swallow exceptions | LOW | Multiple files (6+ locations) | Error Handling |
| 5 | Reflection access to private fields | LOW | Multiple files (4+ locations) | Access Control |
| 6 | Potential ReDoS in type filter | LOW | BusDebuggerWindow.cs:810-821 | Input Validation |
| 7 | Internal architecture info exposure | INFO | Multiple windows | Information Disclosure |
| 8 | World state capture/restore without integrity checks | INFO | TimeMachineWindow.cs:279-283 | State Integrity |
| 9 | Duplicate toolbar/button calls | INFO | StradaEntityInspectorWindow.cs:171-172,254-261 | Code Quality |
