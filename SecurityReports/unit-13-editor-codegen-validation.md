# Unit 13 — Editor CodeGen & Validation: Security Review

## Executive Summary

The Editor CodeGen and Validation subsystems are editor-only tools used for code generation and architectural rule enforcement within the Unity Editor. The code generation modules (`SystemRegistryGenerator`, `ModuleInitializerGenerator`, `StradaCodeGenerator`) write auto-generated C# source files to a hardcoded output directory. The validation modules (`ModuleStructureValidator`, `ViewBusinessLogicRule`, and others) perform read-only reflection-based and file-system-based checks on project structure.

The overall risk profile is **low**. All code runs exclusively in the Unity Editor context (not in shipped builds), paths are hardcoded constants rather than user-supplied, and generated code is derived from compiler-verified type metadata rather than arbitrary strings. Several minor defensive-coding gaps exist but none represent exploitable vulnerabilities in the intended editor-tool threat model.

---

## Detailed Findings

### Finding 1: Generated Code Uses Type.FullName Without Sanitization

**Severity:** LOW
**Location:** `Editor/CodeGen/SystemRegistryGenerator.cs` lines 91-93, 102-103, 114-115; `Editor/CodeGen/ModuleInitializerGenerator.cs` lines 98-99, 109-110, 122-123
**Category:** Generated Code Injection

The `GetFullTypeName` method inserts `Type.FullName` values directly into generated C# source via string interpolation (e.g., `$"typeof({typeName}),"`). If a type's `FullName` contained C# metacharacters, the generated source could be malformed or inject unintended code.

**Mitigating factors:**
- `Type.FullName` is derived from the CLR type system, not from user input. A type name must have already passed the C# compiler to appear in `AppDomain.CurrentDomain.GetAssemblies()`.
- The only transformation applied (`Replace("+", ".")`) converts nested-type CLR notation to valid C# syntax.
- An attacker would need to introduce a malicious assembly into the Unity editor domain, which already implies full editor-level compromise.

**Recommendation:** No immediate action required. For defense-in-depth, consider adding a regex whitelist check (e.g., `^[a-zA-Z_][a-zA-Z0-9_.<>,\s]*$`) on the resolved type name before emitting it into generated code.

---

### Finding 2: Hardcoded Output Path With No Canonicalization

**Severity:** LOW
**Location:** `Editor/CodeGen/SystemRegistryGenerator.cs` lines 15-16, 30-34; `Editor/CodeGen/ModuleInitializerGenerator.cs` lines 15-16, 30-34; `Editor/CodeGen/StradaCodeGenerator.cs` lines 22-29
**Category:** Path Traversal in Code Generation

The output folder `"Assets/Strada.Generated"` is a hardcoded relative path constant. `Path.Combine(GeneratedFolder, GeneratedFile)` constructs the final write target. `Directory.CreateDirectory` and `File.WriteAllText` are called on this path. The `CleanGeneratedCode` method calls `Directory.Delete(folder, true)` which recursively deletes the folder and all contents.

**Mitigating factors:**
- Both the folder and filename are compile-time string constants, not user-controlled.
- No external input flows into path construction.
- Unity resolves relative paths from the project root, so traversal outside the project would require modifying the constant itself.
- The recursive delete in `StradaCodeGenerator.CleanGeneratedCode` targets only the hardcoded `Assets/Strada.Generated` path.

**Recommendation:** No action required. The paths are static constants. If these paths ever become configurable (e.g., via EditorPrefs or a settings file), add `Path.GetFullPath` canonicalization and validate the resolved path stays within `Application.dataPath`.

---

### Finding 3: Silent Exception Swallowing in Assembly Scanning

**Severity:** LOW
**Location:** `Editor/CodeGen/SystemRegistryGenerator.cs` line 64; `Editor/CodeGen/ModuleInitializerGenerator.cs` line 71; `Editor/CodeGen/ModuleNameValidator.cs` lines 199-201
**Category:** Validation Rule Bypass

Multiple assembly-scanning loops contain empty `catch { }` blocks. If a `ReflectionTypeLoadException` or other exception occurs for a specific assembly, all types in that assembly are silently skipped. This means systems or modules in those assemblies will not appear in generated registries, and module name conflicts will not be detected.

**Mitigating factors:**
- This is a common and accepted pattern for assembly scanning in .NET/Unity because some assemblies legitimately cannot be reflected upon.
- The `ArchitectureValidator.ValidateAssembly` method (line 87-89) handles `ReflectionTypeLoadException` correctly by extracting loadable types.

**Recommendation:** Consider catching `ReflectionTypeLoadException` specifically and processing `ex.Types.Where(t => t != null)` (as `ArchitectureValidator` already does) instead of silently discarding entire assemblies. Log a `Debug.LogWarning` for skipped assemblies to aid debugging.

---

### Finding 4: ModuleStructureValidator Directory Operations Are Read-Only

**Severity:** INFO
**Location:** `Editor/Validation/ModuleStructureValidator.cs` lines 40, 47, 115, 127-128, 138, 163, 214-215, 218
**Category:** File Operation Safety / Directory Traversal

The `ModuleStructureValidator` performs numerous `Directory.Exists`, `Directory.GetDirectories`, `Directory.GetFiles`, `File.Exists`, and `Path.Combine` calls. All operations are read-only checks and none modify the file system.

**Analysis:**
- The root path (`Application.dataPath + "/Modules"`) is derived from the Unity project path, not user input.
- `Directory.GetDirectories(modulesPath)` enumerates only immediate children.
- `Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories)` recursively scans for `.cs` files but only reads filenames for naming validation.
- No `File.Delete`, `File.Move`, or `File.WriteAllText` calls exist in this validator. The task description references `File.Delete` at certain lines, but the actual code at those lines contains only `Directory.Exists`, `Path.Combine`, and `File.Exists` checks.

**Recommendation:** None. The validator is safely read-only.

---

### Finding 5: ViewBusinessLogicRule Pattern Matching Is Heuristic-Based

**Severity:** INFO
**Location:** `Editor/Validation/ViewBusinessLogicRule.cs` lines 131-149, especially 142-145
**Category:** Validation Rule Bypass

The `HasBusinessLogicMethodName` method uses `string.Contains` with `StringComparison.OrdinalIgnoreCase` to detect business-logic method names (e.g., "Calculate", "Process"). The `AllowedMethodPrefixes` list uses `string.StartsWith` to whitelist UI-related methods.

**Bypass scenarios:**
- A method named `OnCalculateDiscount` would be whitelisted by the "On" prefix despite containing "Calculate".
- A method named `Recalculate` would not trigger the rule because "Calculate" does not appear at a word boundary (though `string.Contains` would actually match "Calculate" as a substring of "Recalculate", so it would be flagged).
- Renaming methods to avoid the pattern keywords trivially bypasses the rule.

**Mitigating factors:**
- This is an advisory/linting rule, not a security control. It produces warnings, not errors.
- The heuristic approach is appropriate for its purpose of catching common architectural violations.
- The `IsServiceType` check (lines 154-172) using name-suffix matching ("Service", "Repository", "Manager", "Handler") is similarly heuristic but adequate for its advisory role.

**Recommendation:** None required for security purposes. The prefix-takes-priority logic is a design choice, not a vulnerability.

---

### Finding 6: ModuleNameValidator Sanitize Method Does Not Guarantee Uniqueness

**Severity:** INFO
**Location:** `Editor/CodeGen/ModuleNameValidator.cs` lines 151-162
**Category:** Validation Rule Bypass

The `Sanitize` method strips non-alphanumeric characters and uppercases the first character. It does not check the result against reserved names or existing modules. If a caller uses `Sanitize` without subsequently calling `Validate`, a reserved or conflicting name could be used.

**Mitigating factors:**
- The `Validate` method performs all necessary checks (reserved names, existing modules, PascalCase, length).
- `Sanitize` is a helper for cleaning input before validation, not a standalone validation gate.

**Recommendation:** Add a doc comment to `Sanitize` clarifying that callers must still call `Validate` on the result, or have `Sanitize` return a `ModuleNameValidationResult`.

---

## Summary Table

| # | Finding | Severity | Category | Location |
|---|---------|----------|----------|----------|
| 1 | Type.FullName used in generated code without sanitization | LOW | Generated Code Injection | `SystemRegistryGenerator.cs`, `ModuleInitializerGenerator.cs` |
| 2 | Hardcoded output path with no canonicalization | LOW | Path Traversal | `SystemRegistryGenerator.cs`, `ModuleInitializerGenerator.cs`, `StradaCodeGenerator.cs` |
| 3 | Silent exception swallowing in assembly scanning | LOW | Validation Rule Bypass | `SystemRegistryGenerator.cs:64`, `ModuleInitializerGenerator.cs:71`, `ModuleNameValidator.cs:199` |
| 4 | ModuleStructureValidator directory operations are read-only | INFO | File Operation Safety | `ModuleStructureValidator.cs` |
| 5 | ViewBusinessLogicRule pattern matching is heuristic-based | INFO | Validation Rule Bypass | `ViewBusinessLogicRule.cs:131-149` |
| 6 | Sanitize method does not guarantee validation | INFO | Validation Rule Bypass | `ModuleNameValidator.cs:151-162` |

**Overall Assessment:** No CRITICAL or HIGH severity issues found. The editor tooling operates within expected security boundaries for Unity Editor-only code. The three LOW-severity findings represent defense-in-depth opportunities rather than exploitable vulnerabilities.
