# Unit 16 — Roslyn Source Generators: Security Review

## Files Reviewed

- `SourceGenerationDI~/StradaDISourceGenerator.cs`
- `SourceGenerationECS~/EntityQueryGenerator.cs`
- `SourceGenerationECS~/StradaFactoryGenerator.cs`

## Executive Summary

The three Roslyn source generators produce C# code at compile time for dependency injection containers, ECS entity query structs, and service factory classes. Overall risk is low because the generators operate on the Roslyn semantic model, which inherently constrains inputs to valid type symbols from the compilation. The most notable concern is the use of `unsafe` pointer arithmetic in the `EntityQueryGenerator` output, which relies on correctness of the underlying `SparseSet` API. There are minor gaps in type-name sanitization in `StradaDISourceGenerator` and a `#pragma warning disable` pattern in `StradaFactoryGenerator` that suppresses nullability warnings in generated code.

## Detailed Findings

### Finding 1 — Unsafe Pointer Arithmetic in Generated EntityQuery Code

**Severity: MEDIUM**

**Location:** `SourceGenerationECS~/EntityQueryGenerator.cs`, lines 94–124 (the `GenerateStruct` method)

**Description:**
The generator emits `unsafe` blocks that perform raw pointer dereferencing in the generated `ForEach` method:

```csharp
sb.AppendLine($"                    action(e");
for (int i = 1; i <= n; i++) sb.Append($", ref *(set{i}.GetDataPtr() + d{i})");
```

This produces code like `ref *(set1.GetDataPtr() + d1)` which dereferences a pointer offset without bounds checking in the generated output. Safety depends entirely on:
1. `GetDenseIndex(e)` returning a valid index (the `< 0` check filters negatives but does not verify upper bounds).
2. `GetDataPtr()` returning a valid, sufficiently large allocation.
3. The `min` variable correctly bounding iteration count.

While the generator itself only produces fixed patterns (T1..T16) and no external input can alter the generated code shape, the generated unsafe code is a runtime risk if the underlying storage APIs have bugs.

**Recommendation:** Consider adding an upper-bound check on `d{i}` against `set{i}.Count` in the generated code, or document why the current negative-only check is sufficient. Alternatively, use `Span<T>` with bounds checking instead of raw pointers where performance allows.

---

### Finding 2 — Incomplete Type Name Sanitization in StradaDISourceGenerator

**Severity: LOW**

**Location:** `SourceGenerationDI~/StradaDISourceGenerator.cs`, lines 196–199

**Description:**
The `GetSafeName` method sanitizes type names for use as method identifiers:

```csharp
private string GetSafeName(string typeName)
{
    return typeName.Replace(".", "_").Replace("<", "_").Replace(">", "_");
}
```

This is used to generate method names like `ResolveMyNamespace_IMyService()`. However, it does not handle commas (from generic type arguments with multiple parameters, e.g., `IDictionary<string, int>`), plus signs (from nested types), or other characters that could appear in `ToDisplayString()` output (e.g., `[]` for arrays, `?` for nullable reference types). If such types are registered, the generated method name would contain invalid C# identifier characters, causing a compile error — not a security vulnerability per se, but a correctness/robustness issue.

In contrast, `StradaFactoryGenerator` uses `symbol.Name` (the simple name without namespace or generic decorations) for its factory class names, avoiding this problem.

**Recommendation:** Expand the sanitization to cover commas, spaces, brackets, question marks, and plus signs, or use a more robust identifier-safe transformation (e.g., regex replacing all non-alphanumeric characters).

---

### Finding 3 — Type Names Used Directly in Generated Code Without Validation

**Severity: LOW**

**Location:** `SourceGenerationDI~/StradaDISourceGenerator.cs`, lines 136, 162, 187–193; `SourceGenerationECS~/StradaFactoryGenerator.cs`, lines 224, 259, 294

**Description:**
Both `StradaDISourceGenerator` and `StradaFactoryGenerator` interpolate type names from `ISymbol.ToDisplayString()` or `SymbolDisplayFormat.FullyQualifiedFormat` directly into generated C# source. For example:

```csharp
sb.AppendLine($"            _factories[typeof({service.InterfaceType})] = () => {factoryMethod};");
```

Because these strings originate from the Roslyn semantic model (i.e., they are resolved type symbols from the compilation being analyzed), they are inherently constrained to valid C# type expressions. An attacker cannot inject arbitrary code through type names because the type must first compile successfully in the source project. This is **not exploitable** in practice, but it is worth noting that no explicit validation or escaping is applied — the safety relies entirely on the Roslyn API contract.

**Recommendation:** No action required. The current approach is standard practice for Roslyn source generators. Document that the safety guarantee comes from the semantic model.

---

### Finding 4 — Nullable Warning Suppression in Generated Code

**Severity: LOW**

**Location:** `SourceGenerationECS~/StradaFactoryGenerator.cs`, lines 193–194

**Description:**
The generator emits `#pragma warning disable CS8603` and `CS8604` in the generated output, suppressing nullable reference type warnings globally in the generated file. This means any null-returning or null-passing patterns in the generated factory/resolution code will not produce compiler warnings, potentially masking null reference issues at runtime.

**Recommendation:** Narrow the pragma scope to specific lines where nullability is intentionally relaxed, or use the null-forgiving operator (`!`) on specific expressions instead of blanket suppression.

---

### Finding 5 — Factory Class Naming Could Collide

**Severity: LOW**

**Location:** `SourceGenerationECS~/StradaFactoryGenerator.cs`, lines 217

**Description:**
Factory class names are derived from `symbol.Name` (the simple class name):

```csharp
var factoryName = $"{service.ClassName}__Factory";
```

If two classes in different namespaces share the same simple name (e.g., `Foo.MyService` and `Bar.MyService`), both would generate `MyService__Factory` in the same `Strada.Generated` namespace, causing a compile error. This is a correctness issue rather than a security vulnerability.

**Recommendation:** Include namespace or a hash in the factory class name to prevent collisions, e.g., `MyNamespace_MyService__Factory`.

---

### Finding 6 — EntityQueryGenerator Uses Fixed Patterns Only (No External Input)

**Severity: INFO**

**Location:** `SourceGenerationECS~/EntityQueryGenerator.cs`, entire file

**Description:**
Unlike the DI generators, `EntityQueryGenerator` does not inspect user source code at all. It uses `RegisterPostInitializationOutput` to emit a fixed set of structs (T9 through T16) with deterministic code. The generated output is identical regardless of the project being compiled. This eliminates any possibility of code injection through this generator.

**Recommendation:** None. This is a positive security observation.

---

### Finding 7 — StradaDISourceGenerator Thread Safety of Singleton Registration

**Severity: LOW**

**Location:** `SourceGenerationDI~/StradaDISourceGenerator.cs`, lines 138–141

**Description:**
The generated container eagerly creates singletons during construction:

```csharp
if (service.Lifetime == "Singleton")
{
    sb.AppendLine($"            _singletons[typeof({service.InterfaceType})] = {factoryMethod};");
}
```

Singletons are instantiated in `RegisterServices()` (called from the constructor), which means they are created once during container construction. This is safe for single-threaded use. However, the generated `Resolve<T>()` method reads from `_singletons` and `_factories` dictionaries without synchronization. If the container is accessed from multiple threads, this could lead to race conditions — though in Unity's single-threaded execution model, this is unlikely to be an issue.

**Recommendation:** If multi-threaded access is ever intended, consider using `ConcurrentDictionary` or add synchronization in the generated code.

## Summary Table

| # | Finding | Severity | Location | Exploitable? |
|---|---------|----------|----------|-------------|
| 1 | Unsafe pointer arithmetic without upper-bound check | MEDIUM | EntityQueryGenerator.cs:94-124 | No (compile-time only patterns, but runtime risk if storage APIs have bugs) |
| 2 | Incomplete type name sanitization for method identifiers | LOW | StradaDISourceGenerator.cs:196-199 | No (causes compile error, not injection) |
| 3 | Type names interpolated without explicit validation | LOW | Both DI generators | No (Roslyn semantic model constrains values) |
| 4 | Blanket nullable warning suppression in generated code | LOW | StradaFactoryGenerator.cs:193-194 | No (masks warnings only) |
| 5 | Factory class name collision possible | LOW | StradaFactoryGenerator.cs:217 | No (causes compile error, not injection) |
| 6 | EntityQueryGenerator uses fixed patterns only | INFO | EntityQueryGenerator.cs (entire) | N/A (positive observation) |
| 7 | Generated singleton container lacks thread safety | LOW | StradaDISourceGenerator.cs:138-141 | No (Unity single-threaded model) |
