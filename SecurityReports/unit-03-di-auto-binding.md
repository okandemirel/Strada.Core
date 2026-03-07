# Security Review: Unit 3 — DI Auto-Binding Scanner

**Files reviewed:**
- `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs`
- `Runtime/DI/Attributes/AutoRegisterAttribute.cs`
- `Runtime/DI/ContainerBuilderExtensions.cs`

**Reviewer:** Claude (automated security analysis)
**Date:** 2026-03-07

---

## Executive Summary

The `RuntimeAutoBindingScanner` performs assembly scanning at runtime to discover types annotated with auto-registration attributes and binds them into the DI container. The scanner uses `AppDomain.CurrentDomain.GetAssemblies()` filtered by string-based include/exclude patterns, reflection-based method invocation via `MakeGenericMethod`, and a lock-guarded static cache.

The primary risks center on: (1) a race condition in the double-checked locking pattern that can cause cache entries built with one set of patterns to be silently returned for requests with different patterns, (2) pattern matching that uses case-sensitive prefix/suffix checks inconsistently (exact match is case-insensitive, wildcard matches are case-sensitive), (3) swallowed `ReflectionTypeLoadException` that hides assembly load failures, and (4) the potential for a malicious or compromised assembly to inject arbitrary service implementations into the DI container if it matches the default include patterns.

No critical vulnerabilities were found. The most significant issue is the cache-poisoning race condition (HIGH), which can produce incorrect DI bindings in multi-threaded initialization scenarios.

---

## Detailed Findings

### Finding 1: Cache Ignores Filter Parameters — Stale/Incorrect Bindings

**Severity:** HIGH
**Location:** `RuntimeAutoBindingScanner.cs`, lines 40-44, 70-73
**CWE:** CWE-362 (Race Condition), CWE-1188 (Insecure Default Initialization)

The static `_cachedEntries` field stores results from the first call to `ScanAssemblies` regardless of what `includePatterns` and `excludePatterns` were provided. All subsequent calls return the cached result even if different patterns are supplied.

```csharp
lock (_lock)
{
    if (_cachedEntries != null)
        return _cachedEntries;  // Returns previous result regardless of new patterns
}
```

**Impact:** If `ScanAssemblies` is called first with a narrow pattern (e.g., `"Game.*"`) and later with a broad pattern (e.g., `"Strada.*", "Game.*"`), the second call silently returns the narrow result. This can cause services to be missing from the container, leading to runtime `ResolutionFailedException` errors. In a security context, an attacker who controls the order of initialization could ensure certain security-critical services (e.g., authorization handlers) are excluded from registration.

**Recommendation:** Key the cache on the actual pattern sets, or remove caching and let callers cache explicitly. At minimum, log a warning when cached entries are returned for a call with different patterns.

---

### Finding 2: Assembly Injection via Permissive Default Include Patterns

**Severity:** MEDIUM
**Location:** `RuntimeAutoBindingScanner.cs`, line 46
**CWE:** CWE-829 (Inclusion of Functionality from Untrusted Control Sphere)

The default include patterns are:

```csharp
includePatterns ??= new[] { "Strada.*", "Game.*", "Assembly-CSharp" };
```

The `"Game.*"` pattern uses a case-sensitive `StartsWith` check (line 211), so any assembly whose name starts with `Game.` will be scanned. In Unity, third-party plugins, asset store packages, or user-generated mods could ship assemblies matching this prefix (e.g., `Game.Malicious`). Any type in such an assembly annotated with `[AutoRegisterSingleton(As = typeof(ICriticalService))]` would silently replace the legitimate implementation in the DI container.

**Impact:** A malicious assembly matching the include pattern could register hostile implementations for security-sensitive interfaces (logging, authentication, data persistence), enabling data exfiltration or privilege escalation within the game.

**Recommendation:** Narrow default include patterns to framework-owned namespaces only (e.g., `"Strada.*"` and `"Assembly-CSharp"`). Require explicit opt-in for additional patterns. Consider adding an allowlist of expected assembly strong names or public key tokens.

---

### Finding 3: Inconsistent Case Sensitivity in Pattern Matching

**Severity:** MEDIUM
**Location:** `RuntimeAutoBindingScanner.cs`, lines 205-214
**CWE:** CWE-178 (Improper Handling of Case Sensitivity)

The `MatchesPattern` method uses case-insensitive comparison only for exact matches (line 213: `StringComparison.OrdinalIgnoreCase`), while wildcard prefix/suffix checks use the default case-sensitive `string.StartsWith`, `string.EndsWith`, and `string.Contains`:

```csharp
if (pattern.EndsWith("*"))
    return name.StartsWith(pattern.TrimEnd('*'));   // Case-sensitive
return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);  // Case-insensitive
```

**Impact:** An assembly named `game.Exploit` would bypass the `"Game.*"` include pattern (capital G), but `assembly-csharp` would match the exact pattern `"Assembly-CSharp"` due to case-insensitive exact match. This inconsistency makes it difficult to reason about which assemblies are included or excluded, and could lead to either over-inclusion or under-exclusion of assemblies depending on platform-specific assembly naming.

**Recommendation:** Standardize on case-insensitive matching for all pattern types (prefix, suffix, contains, exact) by passing `StringComparison.OrdinalIgnoreCase` to all string comparison methods.

---

### Finding 4: Swallowed ReflectionTypeLoadException Hides Failures

**Severity:** MEDIUM
**Location:** `RuntimeAutoBindingScanner.cs`, lines 65-67
**CWE:** CWE-390 (Detection of Error Condition Without Action)

The `ReflectionTypeLoadException` is caught and silently discarded:

```csharp
catch (ReflectionTypeLoadException)
{
}
```

**Impact:** If an assembly fails to load some of its types (e.g., due to missing dependencies or tampering), the scanner silently skips it. This means services that should be registered may be missing, and there is no diagnostic output to indicate why. An attacker could exploit this by corrupting a dependency to prevent a security-critical service from loading, causing the application to fall back to a less secure default or throw at resolution time.

**Recommendation:** Log the exception details (including `LoaderExceptions`) at warning level. Consider scanning `ReflectionTypeLoadException.Types` (filtering out nulls) to recover partial type information from assemblies with partial load failures.

---

### Finding 5: Unguarded MakeGenericMethod Invocation

**Severity:** MEDIUM
**Location:** `RuntimeAutoBindingScanner.cs`, lines 167-172, 176-181, 186-191
**CWE:** CWE-470 (Use of Externally-Controlled Input to Select Classes or Code)

The `RegisterEntry` method uses `MakeGenericMethod` with types sourced from assembly scanning without constraint validation:

```csharp
var generic = method.MakeGenericMethod(entry.ServiceType, entry.ImplementationType);
generic.Invoke(builder, new object[] { entry.Lifetime });
```

If a scanned type specifies `As = typeof(SomeUnrelatedType)` where the implementation does not actually implement the service interface, this will produce a runtime error at resolution time rather than at registration time. More importantly, `MakeGenericMethod` with unconstrained type arguments can trigger JIT compilation for arbitrary type combinations.

**Impact:** A malicious `[AutoRegister]` attribute could specify `As` as any type, including internal framework types, potentially allowing the attacker's implementation to be resolved where the framework type is expected. The `First()` LINQ call on lines 168, 177, 187 will throw `InvalidOperationException` if no matching method is found, producing an unhandled crash.

**Recommendation:** Validate that `entry.ImplementationType` is assignable to `entry.ServiceType` before calling `MakeGenericMethod`. Replace `First()` with `FirstOrDefault()` and handle the null case with a descriptive error message.

---

### Finding 6: Denial of Service via Assembly Scanning

**Severity:** LOW
**Location:** `RuntimeAutoBindingScanner.cs`, line 51
**CWE:** CWE-400 (Uncontrolled Resource Consumption)

`AppDomain.CurrentDomain.GetAssemblies()` returns all loaded assemblies. While the include/exclude patterns filter most out, the initial enumeration and pattern matching iterates over every loaded assembly. In large Unity projects with many plugins, this could include hundreds of assemblies.

**Impact:** In pathological cases (very large projects, very many loaded assemblies), the initial scan could cause a noticeable frame spike during initialization. The performance tests suggest this is expected to complete within 50ms, which is acceptable for initialization but would be problematic if triggered during gameplay.

**Recommendation:** The existing caching mitigates repeated costs. Ensure `ScanAssemblies` is only called during initialization (e.g., loading screens) and never during frame-sensitive code paths. Consider adding a timeout or assembly count limit as a safety valve.

---

### Finding 7: Source-Generated Registry Loaded via Unchecked Reflection

**Severity:** LOW
**Location:** `ContainerBuilderExtensions.cs`, lines 40-43
**CWE:** CWE-470 (Use of Externally-Controlled Input to Select Classes or Code)

The `TryUseSourceGenerated` method loads a class by fully qualified name from well-known assemblies:

```csharp
var registryType =
    Type.GetType("Strada.Generated.StradaGeneratedRegistry, Assembly-CSharp") ??
    Type.GetType("Strada.Generated.StradaGeneratedRegistry, Assembly-CSharp-firstpass") ??
    Type.GetType("Strada.Generated.StradaGeneratedRegistry");
```

If an attacker can place a type with this exact name in any loaded assembly, they can hijack the entire DI registration process. The bare `catch` on line 59 swallows all exceptions from this reflection path.

**Impact:** Low in practice because placing a type in `Assembly-CSharp` requires modifying the game's source code or build pipeline. However, the catch-all exception handler means any error in the generated registry is silently ignored, falling back to runtime scanning without any diagnostic.

**Recommendation:** Add assembly identity validation (strong name or public key token check) when loading the generated registry. Replace the bare `catch` with targeted exception handling and logging.

---

### Finding 8: Mutable Cache Reference Returned Without Defensive Copy

**Severity:** LOW
**Location:** `RuntimeAutoBindingScanner.cs`, lines 42-43, 72, 75
**CWE:** CWE-374 (Passing Mutable Objects to an Untrusted Method)

The `ScanAssemblies` method returns a direct reference to the internal `_cachedEntries` list. Any caller can modify this list (add, remove, or replace entries), affecting all subsequent callers who receive the same cached reference.

```csharp
lock (_lock)
{
    _cachedEntries = entries;
}
return entries;  // Mutable reference shared with all callers
```

**Impact:** A malicious or buggy consumer could add rogue entries to or remove legitimate entries from the cached list, affecting all future DI registrations in the application.

**Recommendation:** Return `_cachedEntries.AsReadOnly()` or a new `List<AutoBindingEntry>(_cachedEntries)` to prevent external mutation of the cache.

---

## Summary Table

| # | Finding | Severity | CWE | Location |
|---|---------|----------|-----|----------|
| 1 | Cache ignores filter parameters | HIGH | CWE-362, CWE-1188 | Lines 40-44, 70-73 |
| 2 | Permissive default include patterns allow assembly injection | MEDIUM | CWE-829 | Line 46 |
| 3 | Inconsistent case sensitivity in pattern matching | MEDIUM | CWE-178 | Lines 205-214 |
| 4 | Swallowed ReflectionTypeLoadException hides failures | MEDIUM | CWE-390 | Lines 65-67 |
| 5 | Unguarded MakeGenericMethod with no type validation | MEDIUM | CWE-470 | Lines 167-191 |
| 6 | Potential frame spike from assembly enumeration | LOW | CWE-400 | Line 51 |
| 7 | Source-generated registry loaded via unchecked reflection | LOW | CWE-470 | ContainerBuilderExtensions.cs:40-43 |
| 8 | Mutable cache reference returned to callers | LOW | CWE-374 | Lines 42-43, 72, 75 |
