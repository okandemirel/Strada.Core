# Security Review: Unit 2 — DI Injection & Lifecycle

**Review Date:** 2026-03-07
**Reviewer:** Claude (Automated Security Analysis)
**Scope:** `Runtime/DI/InjectionProcessor.cs`, `Runtime/DI/LifecycleProcessor.cs`, `Runtime/DI/Attributes/`

---

## Executive Summary

The DI injection and lifecycle subsystem uses reflection to inject dependencies into private fields, properties, and methods, and to invoke lifecycle callbacks. The core security concern is the broad use of `BindingFlags.NonPublic` which bypasses normal access control, combined with the absence of type validation on resolved values before injection. Thread safety in `LifecycleProcessor` is also lacking compared to the double-checked locking pattern used in `InjectionProcessor`. No critical vulnerabilities were found — the issues are consistent with a trusted-environment DI framework — but several findings could harden the code against misuse or unexpected behavior.

---

## Detailed Findings

### DI-01 — Reflection Bypasses Access Modifiers on Fields and Properties

| Attribute | Value |
|-----------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/InjectionProcessor.cs:57` |
| **Category** | Improper Access Control |

**Description:**
`BuildInjectionInfo` uses `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic` to discover fields, properties, and methods. This means any member marked `[Inject]` — including `private`, `protected`, or `internal` members — will be written to via reflection, bypassing the access control enforced by the C# compiler.

While this is a standard DI pattern, it means that a dependency registered in the container can be silently injected into private state that the class author may not have intended to expose. If a malicious or misconfigured assembly registers an unexpected type, it could overwrite private fields that the object relies on for invariant enforcement.

**Recommendation:**
Consider adding an opt-in configuration flag to restrict injection to public members only, or log a warning in debug builds when injecting into non-public members. Document the security implications for framework consumers.

---

### DI-02 — No Type Validation Before Reflection-Based Assignment

| Attribute | Value |
|-----------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/InjectionProcessor.cs:110-111, 121-122` |
| **Category** | Input Validation Gap |

**Description:**
In `InjectProperties` and `InjectFields`, the value returned by `container.Resolve(type)` is assigned directly via `PropertyInfo.SetValue` or `FieldInfo.SetValue` with no validation that the resolved object is actually assignable to the target member type. If the container returns an incompatible type (due to misconfiguration or a registration override), the reflection call will throw a runtime `ArgumentException` or `TargetException`.

Similarly, in `InjectMethods` (line 100), `MethodInfo.Invoke` is called with resolved arguments without verifying type compatibility first. A type mismatch would surface as an opaque `TargetInvocationException`.

**Recommendation:**
Add a pre-assignment type check using `Type.IsAssignableFrom` or `Type.IsInstanceOfType` and throw a descriptive exception that names the target type, member, and resolved type. This converts opaque reflection errors into actionable diagnostic messages and prevents potential type confusion.

---

### DI-03 — LifecycleProcessor Cache Is Not Thread-Safe

| Attribute | Value |
|-----------|-------|
| **Severity** | HIGH |
| **File** | `Runtime/DI/LifecycleProcessor.cs:19-23, 34-38` |
| **Category** | Race Condition / Thread Safety |

**Description:**
`InjectionProcessor` uses a double-checked locking pattern with a dedicated `_lock` object to protect its `_cache` dictionary. However, `LifecycleProcessor` uses plain `Dictionary<Type, MethodInfo[]>` for both `PostConstructCache` and `DeConstructCache` with no synchronization.

If `InvokePostConstruct` or `InvokeDeConstruct` is called concurrently from multiple threads for different types, the unsynchronized `TryGetValue` / indexer-set pattern on `Dictionary` can corrupt the internal hash table, leading to lost entries, infinite loops in bucket chains, or `NullReferenceException`. This is a well-documented hazard with `Dictionary<TKey, TValue>` under concurrent writers.

**Recommendation:**
Apply the same double-checked locking pattern used in `InjectionProcessor`, or replace the dictionaries with `ConcurrentDictionary<Type, MethodInfo[]>` and use `GetOrAdd`.

---

### DI-04 — Unhandled Exceptions in Lifecycle Method Invocation

| Attribute | Value |
|-----------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/LifecycleProcessor.cs:25-26, 40-41` |
| **Category** | Denial of Service / Resource Leak |

**Description:**
In `InvokePostConstruct` and `InvokeDeConstruct`, each discovered method is invoked sequentially without exception handling. If the first `[PostConstruct]` method throws, subsequent methods on the same object are skipped. More critically, if a `[DeConstruct]` method throws, remaining cleanup methods are skipped, which can cause resource leaks (event subscriptions not removed, unmanaged handles not released).

**Recommendation:**
Wrap each lifecycle method invocation in a try-catch. For `DeConstruct`, continue invoking remaining methods after catching an exception, and aggregate or log errors. Consider surfacing exceptions through a framework-level error handler rather than silently swallowing them.

---

### DI-05 — Unbounded Reflection Cache Growth

| Attribute | Value |
|-----------|-------|
| **Severity** | LOW |
| **File** | `Runtime/DI/InjectionProcessor.cs:11, 46` and `Runtime/DI/LifecycleProcessor.cs:10-11` |
| **Category** | Denial of Service |

**Description:**
Both `InjectionProcessor._cache` and `LifecycleProcessor`'s `PostConstructCache` / `DeConstructCache` grow without bound as new types are encountered. In a long-running application that dynamically loads assemblies or generates types at runtime (e.g., via `TypeBuilder` or IL weaving), this could lead to unbounded memory growth. `ClearCache()` exists but must be called manually.

While Unity games typically have a finite set of types, this is a latent risk if the framework is used in scenarios with dynamic type generation.

**Recommendation:**
Document the expected usage pattern (finite type set). For defense in depth, consider adding a configurable cache size limit or logging a warning when the cache exceeds a threshold. Ensure `ClearCache()` is called during scene transitions or domain reloads.

---

### DI-06 — LifecycleProcessor.ClearCache Is Not Thread-Safe

| Attribute | Value |
|-----------|-------|
| **Severity** | MEDIUM |
| **File** | `Runtime/DI/LifecycleProcessor.cs:58-62` |
| **Category** | Race Condition / Thread Safety |

**Description:**
`LifecycleProcessor.ClearCache()` calls `Clear()` on both dictionaries without any synchronization. If `ClearCache()` is called while another thread is inside `InvokePostConstruct` or `InvokeDeConstruct`, the dictionary can be corrupted (same root cause as DI-03). By contrast, `InjectionProcessor.ClearCache()` correctly acquires `_lock` before clearing.

**Recommendation:**
Add synchronization to `ClearCache()` consistent with the fix for DI-03.

---

### DI-07 — Lifecycle Attributes Allow Multiple Methods Without Defined Order

| Attribute | Value |
|-----------|-------|
| **Severity** | LOW |
| **File** | `Runtime/DI/LifecycleProcessor.cs:44-56`, `Runtime/DI/Attributes/LifecycleAttributes.cs:9, 16` |
| **Category** | Input Validation Gap |

**Description:**
The `PostConstructAttribute` and `DeConstructAttribute` are declared with `AllowMultiple = false`, which prevents them from being applied to the same method twice. However, nothing prevents multiple different methods on the same class from each carrying the attribute. `FindMethodsWithAttribute` collects all such methods into a list, but `Type.GetMethods()` does not guarantee a stable ordering. This means the execution order of multiple `[PostConstruct]` or `[DeConstruct]` methods is non-deterministic, which can lead to subtle initialization or cleanup bugs.

**Recommendation:**
Either enforce a single lifecycle method per attribute per type (throw if more than one is found), or add an `Order` property to the attributes and sort by it before invocation.

---

### DI-08 — AutoRegisterAttribute.As Property Lacks Type Constraint Validation

| Attribute | Value |
|-----------|-------|
| **Severity** | LOW |
| **File** | `Runtime/DI/Attributes/AutoRegisterAttribute.cs:9` |
| **Category** | Input Validation Gap |

**Description:**
The `As` property on `AutoRegisterAttribute` (and its singleton/transient/scoped variants) accepts any `Type`. There is no compile-time or runtime check that the annotated class actually implements or extends the specified type. If a developer writes `[AutoRegister(As = typeof(IFoo))]` on a class that does not implement `IFoo`, the registration will succeed but resolution and injection will fail at runtime with a type mismatch.

**Recommendation:**
At registration scan time, validate that the annotated class is assignable to the `As` type. Throw a descriptive error during container building rather than deferring to a runtime injection failure.

---

### DI-09 — Method Injection Invokes Arbitrary Methods Including Sensitive Operations

| Attribute | Value |
|-----------|-------|
| **Severity** | LOW |
| **File** | `Runtime/DI/InjectionProcessor.cs:59-71, 100` |
| **Category** | Injection Vulnerability |

**Description:**
The `[Inject]` attribute can be placed on any method (public or non-public) per its `AttributeUsage` which includes `AttributeTargets.Method`. The injection processor will then invoke that method via reflection with container-resolved arguments. There is no validation on the method name, return type, or side effects. If applied carelessly (or maliciously in a plugin scenario), this could trigger execution of methods that perform destructive operations (e.g., `DeleteSave`, `ResetProgress`) during object construction.

In the Unity ecosystem where third-party assets are common, this broadens the attack surface for a malicious package that marks harmful methods with `[Inject]`.

**Recommendation:**
Consider restricting method injection to methods with a `void` return type, or providing a whitelist/naming convention. Document that `[Inject]` on methods causes automatic invocation, as this is not obvious from the attribute name alone.

---

### DI-10 — InjectAttribute Includes Constructor Target but InjectionProcessor Does Not Handle Constructors

| Attribute | Value |
|-----------|-------|
| **Severity** | INFO |
| **File** | `Runtime/DI/Attributes/InjectAttribute.cs:5`, `Runtime/DI/InjectionProcessor.cs:59` |
| **Category** | Information Disclosure / Misconfiguration |

**Description:**
`InjectAttribute` declares `AttributeTargets.Constructor` in its usage, but `InjectionProcessor.BuildInjectionInfo` only calls `type.GetMethods(flags)`, which does not return constructors. A developer who places `[Inject]` on a constructor would get no error but the attribute would be silently ignored, leading to uninjected dependencies.

**Recommendation:**
Either remove `AttributeTargets.Constructor` from the `AttributeUsage` to prevent misleading usage, or implement constructor injection in `InjectionProcessor`.

---

## Summary Table

| ID | Severity | Title | File |
|----|----------|-------|------|
| DI-01 | MEDIUM | Reflection bypasses access modifiers | `InjectionProcessor.cs:57` |
| DI-02 | MEDIUM | No type validation before reflection assignment | `InjectionProcessor.cs:110-122` |
| DI-03 | HIGH | LifecycleProcessor cache not thread-safe | `LifecycleProcessor.cs:19-38` |
| DI-04 | MEDIUM | Unhandled exceptions in lifecycle invocation | `LifecycleProcessor.cs:25-41` |
| DI-05 | LOW | Unbounded reflection cache growth | `InjectionProcessor.cs:11`, `LifecycleProcessor.cs:10-11` |
| DI-06 | MEDIUM | LifecycleProcessor.ClearCache not thread-safe | `LifecycleProcessor.cs:58-62` |
| DI-07 | LOW | Multiple lifecycle methods with undefined order | `LifecycleProcessor.cs:44-56` |
| DI-08 | LOW | AutoRegister As property lacks type validation | `AutoRegisterAttribute.cs:9` |
| DI-09 | LOW | Method injection invokes arbitrary methods | `InjectionProcessor.cs:59-71, 100` |
| DI-10 | INFO | Constructor target declared but not implemented | `InjectAttribute.cs:5` |

**Total findings:** 10 (0 Critical, 1 High, 4 Medium, 4 Low, 1 Info)
