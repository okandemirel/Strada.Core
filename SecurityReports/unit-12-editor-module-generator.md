# Security Review: Unit 12 -- Editor Module Generator

**Review Date:** 2026-03-07
**Reviewer:** Claude (Automated Security Analysis)
**Module:** `Editor/ModuleGenerator/` (all subdirectories)
**Risk Rating:** MEDIUM (aggregate)

---

## Executive Summary

The Editor Module Generator is a Unity Editor tool that creates module scaffolding (folders, C# source files, assembly definitions) based on user-provided names, namespaces, and paths. Because this runs exclusively within the Unity Editor (not at runtime or in builds), the attack surface is narrower than runtime code. Nonetheless, several issues exist around unvalidated path construction, lack of path traversal protection on file write/delete operations, and namespace injection into generated C# code. The most significant risks involve an editor user (or a malicious project contributor who modifies serialized settings) being able to write files outside the intended `Assets/` directory or inject arbitrary C# through namespace/module-name values that bypass the UI sanitization layer.

---

## Detailed Findings

### Finding 1: Path Traversal in File Generation -- No Canonicalization

**Severity:** MEDIUM
**Files:** `Pipeline/Steps/FileGenerationStep.cs` (lines 130-139), `Pipeline/Steps/AssemblyDefStep.cs` (lines 59-60, 87-88, 120-121), `Pipeline/Steps/FolderCreationStep.cs` (lines 27, 31, 39)
**CWE:** CWE-22 (Improper Limitation of a Pathname to a Restricted Directory)

**Description:**
`FileGenerationStep.CreateFile` constructs file paths by concatenating `context.Definition.FullPath` with hardcoded subdirectories and the module name:

```csharp
CreateFile($"{basePath}/Scripts/{name}ModuleConfig.cs", ...);
```

The `CreateFile` method then calls `Directory.CreateDirectory(dir)` and `File.WriteAllText(path, content)` without canonicalizing or validating that the resulting path remains under the expected project directory. The same pattern appears in `AssemblyDefStep` (lines 60, 88, 121) and `FolderCreationStep` (lines 27, 31, 39).

`TargetPath` is set from UI text fields (which accept arbitrary strings) or from `EditorPrefs` (which can be modified externally). While validation in `StradaModuleGenerator.Validation.cs` checks `path.StartsWith("Assets")`, this check is only performed in `ValidateTargetPath()` and is trivially bypassable with values like `Assets/../../etc/` since no canonicalization (e.g., `Path.GetFullPath`) is applied. Additionally, the `SetTargetPath` public method (line 88 of `StradaModuleGenerator.cs`) sets the path without any validation.

**Impact:** An attacker who can modify `EditorPrefs` or invoke `SetTargetPath` programmatically could write generated C# files to arbitrary filesystem locations outside the Unity project.

**Recommendation:** Canonicalize all constructed paths using `Path.GetFullPath()` and verify they remain under `Application.dataPath` before any `Directory.CreateDirectory`, `File.WriteAllText`, or `File.Delete` call. Apply this check centrally in `CreateFile` and in `AssemblyDefStep.Execute`.

---

### Finding 2: Rollback File Deletion Without Path Validation

**Severity:** MEDIUM
**Files:** `Pipeline/Steps/FileGenerationStep.cs` (lines 142-153), `Pipeline/Steps/AssemblyDefStep.cs` (lines 130-141), `Pipeline/Steps/FolderCreationStep.cs` (lines 60-78)
**CWE:** CWE-22 (Improper Limitation of a Pathname to a Restricted Directory)

**Description:**
The rollback methods iterate over `context.CreatedFiles` and `context.CreatedFolders` and call `File.Delete` or `Directory.Delete` on each entry. These lists are populated during the `Execute` phase using the same unvalidated paths. If the paths contain traversal sequences, rollback would delete files outside the project.

`FileGenerationStep.Rollback` applies a `.cs` extension check (line 146) and `AssemblyDefStep.Rollback` applies an `.asmdef` check (line 134), but these only filter by extension -- not by location. `FolderCreationStep.Rollback` deletes any empty directory in the list without any path validation.

**Impact:** If path traversal is exploited during generation, rollback amplifies the damage by also deleting arbitrary files matching the extension filter.

**Recommendation:** Before deletion, verify each path is under the expected project root. Consider also verifying that files were actually created by this tool (e.g., via a hash or creation timestamp stored in context).

---

### Finding 3: Namespace Injection into Generated C# Code

**Severity:** MEDIUM
**Files:** `Pipeline/Steps/FileGenerationStep.cs` (lines 155-502), `Utilities/TemplateProcessor.cs` (all preview methods)
**CWE:** CWE-94 (Improper Control of Generation of Code)

**Description:**
The namespace value (`ns` parameter) is interpolated directly into generated C# code via string interpolation:

```csharp
sb.AppendLine($"namespace {ns}");
```

While `ModuleName` is sanitized by `SanitizeModuleName` (only allows letters and digits), the `Namespace` field in `ModuleDefinition` is validated only for empty segments and that each segment starts with a letter (`ValidateNamespace`, lines 93-123 in `Validation.cs`). This validation does not prevent injection of characters like `{`, `}`, `;`, or `//` that could break out of the namespace declaration and inject arbitrary C# code.

For example, a namespace value of `Game.Modules { } public class Malicious { public static void Pwn() { System.Diagnostics.Process.Start("cmd"); } } namespace Fake` would compile and execute arbitrary code when Unity recompiles scripts.

**Impact:** A user who controls the Namespace field (via the UI, EditorPrefs, or programmatic access) can inject arbitrary C# code that executes upon Unity script recompilation.

**Recommendation:** Apply strict validation to the Namespace field -- each segment should match `^[A-Za-z_][A-Za-z0-9_]*$` and the overall namespace should match `^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$`. Reject any namespace containing characters outside this pattern.

---

### Finding 4: Unsafe `allowUnsafeCode` Enabled by Default in Assembly Definitions

**Severity:** LOW
**Files:** `Pipeline/Steps/AssemblyDefStep.cs` (lines 42-57, 66-85, 94-118)
**CWE:** CWE-250 (Execution with Unnecessary Privileges)

**Description:**
All generated assembly definition files (main, editor, and test) set `"allowUnsafeCode": true` by default. This enables `unsafe` blocks (raw pointer arithmetic, stackalloc, etc.) in all generated module code without requiring the developer to opt in.

**Impact:** Developers working in generated modules may inadvertently introduce memory-safety bugs. While this is a design choice rather than a vulnerability in the generator itself, it increases the attack surface of all generated modules.

**Recommendation:** Default `allowUnsafeCode` to `false` and provide a toggle in the generator UI or settings for modules that specifically need unsafe code.

---

### Finding 5: `TargetPath` Validation Bypass via `SetTargetPath`

**Severity:** MEDIUM
**Files:** `StradaModuleGenerator.cs` (lines 88-92)
**CWE:** CWE-20 (Improper Input Validation)

**Description:**
The `SetTargetPath` method is `public` and sets `_moduleDefinition.TargetPath` directly without any validation:

```csharp
public void SetTargetPath(string path)
{
    if (_moduleDefinition != null)
        _moduleDefinition.TargetPath = path;
}
```

This is called from `CreateModuleAtSelection` with the result of `GetSelectedFolderPath()`, which does validate that the path comes from `AssetDatabase`. However, any other editor script or extension can call `SetTargetPath` with an arbitrary path, bypassing the `StartsWith("Assets")` check in `ValidateTargetPath`.

**Impact:** Another editor script or a compromised editor extension could set an arbitrary target path, causing the generator to write files outside the project.

**Recommendation:** Add validation inside `SetTargetPath` or make it `internal`/`private`. Alternatively, validate the path at generation time (in `StartGeneration`) rather than relying solely on UI-time validation.

---

### Finding 6: EditorPrefs Used for Persisting Sensitive Configuration

**Severity:** LOW
**Files:** `StradaModuleGenerator.cs` (lines 154-173), `StradaModuleGenerator.Generation.cs` (lines 98-124)
**CWE:** CWE-922 (Insecure Storage of Sensitive Information)

**Description:**
Module configuration (namespace, target path, module type) and pending post-generation operations (module registration, config asset creation) are stored in `EditorPrefs`. On Windows, `EditorPrefs` values are stored in the Windows Registry and are accessible to any process running as the same user. On macOS, they are stored in `~/Library/Preferences/`.

The `PendingModuleData` stored in `EditorPrefs` (keys `Strada_PendingModuleRegistration` and `Strada_PendingModuleConfigAsset`) includes `ModulePath` and `Namespace`, which are consumed after script recompilation without re-validation.

**Impact:** A malicious process running as the same user could modify `EditorPrefs` to inject a crafted `PendingModuleData` JSON payload, causing the post-processor to create assets at attacker-controlled paths or with attacker-controlled type names.

**Recommendation:** Re-validate all data read from `EditorPrefs` before use, particularly in `ProcessPendingModuleConfigAsset` and `ProcessPendingBootstrapperRegistration`. Consider using a project-local file instead of `EditorPrefs`.

---

### Finding 7: Missing Path Validation in `DirectoryStructureConfig` Folder Entries

**Severity:** LOW
**Files:** `Config/DirectoryStructureConfig.cs` (lines 17-37, 75-94)
**CWE:** CWE-22 (Improper Limitation of a Pathname to a Restricted Directory)

**Description:**
`DirectoryStructureConfig` is a `ScriptableObject` with serialized `FolderEntry` objects containing a `Path` field. These paths are concatenated with `basePath` in `FolderCreationStep`:

```csharp
var path = $"{basePath}/{folder}";
```

Since `DirectoryStructureConfig` is a serializable asset, a malicious contributor could modify it to include folder paths with traversal sequences (e.g., `../../malicious`). The `GetFoldersForModule` method returns these paths without validation.

**Impact:** If the config asset is tampered with, directory creation could escape the project boundary.

**Recommendation:** Validate that folder entries do not contain `..`, absolute paths, or other traversal patterns before using them.

---

### Finding 8: Swallowed Exceptions in ModuleDiscovery

**Severity:** INFO
**Files:** `Utilities/ModuleDiscovery.cs` (lines 139-159, 201-215)
**CWE:** CWE-390 (Detection of Error Condition Without Action)

**Description:**
Both `EnrichFromInstallers` and `FindAssemblyForModule` use empty `catch` blocks that silently swallow all exceptions when iterating assemblies:

```csharp
catch { }
```

While this is a common pattern for handling `ReflectionTypeLoadException` in Unity, it masks potential errors that could indicate assembly tampering or loading issues.

**Impact:** Minimal direct security impact, but reduces observability and could mask indicators of compromised assemblies.

**Recommendation:** At minimum, log a debug-level message when exceptions occur. Consider catching only `ReflectionTypeLoadException` rather than all exceptions.

---

## Summary Table

| # | Finding | Severity | CWE | File(s) |
|---|---------|----------|-----|---------|
| 1 | Path traversal -- no canonicalization on file write | MEDIUM | CWE-22 | FileGenerationStep.cs, AssemblyDefStep.cs, FolderCreationStep.cs |
| 2 | Rollback deletes files without path validation | MEDIUM | CWE-22 | FileGenerationStep.cs, AssemblyDefStep.cs, FolderCreationStep.cs |
| 3 | Namespace injection into generated C# code | MEDIUM | CWE-94 | FileGenerationStep.cs, TemplateProcessor.cs |
| 4 | `allowUnsafeCode` enabled by default in asmdef | LOW | CWE-250 | AssemblyDefStep.cs |
| 5 | `SetTargetPath` bypasses validation | MEDIUM | CWE-20 | StradaModuleGenerator.cs |
| 6 | EditorPrefs stores sensitive config without re-validation | LOW | CWE-922 | StradaModuleGenerator.cs, StradaModuleGenerator.Generation.cs |
| 7 | DirectoryStructureConfig folder paths not validated | LOW | CWE-22 | DirectoryStructureConfig.cs |
| 8 | Swallowed exceptions in assembly reflection | INFO | CWE-390 | ModuleDiscovery.cs |
