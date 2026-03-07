# Unit 17 — Reflection & Dynamic Type Loading: Security Review

## Executive Summary

The Strata.Core framework makes extensive use of reflection and dynamic type loading across its dependency injection (DI), module discovery, ECS system instantiation, and editor tooling subsystems. The review identified **no critical vulnerabilities** but found several patterns that warrant attention depending on the deployment model.

The primary risk pattern is the `SerializableType` class, which resolves types from assembly-qualified name strings stored in Unity serialized assets. These strings flow into `Activator.CreateInstance` in the `SystemRunner`, creating a chain where serialized data controls type instantiation. In a standard Unity game context, this risk is **low** because Unity asset serialization is not exposed to untrusted input at runtime. However, if asset bundles are loaded from untrusted sources or if mod support is added, this becomes a significant concern.

All reflection usage in the Runtime layer is internally driven (attribute scanning, DI wiring, generic method construction). No pattern was found where network input, user-supplied strings, or external data directly reaches `Type.GetType` or `Activator.CreateInstance` in the current codebase.

---

## Inventory of Reflection Usage

### Type.GetType (Dynamic Type Resolution from Strings)

| File | Line(s) | Context |
|------|---------|---------|
| `Runtime/DI/ContainerBuilderExtensions.cs` | 41-43, 70-71 | Hardcoded lookup for `Strada.Generated.StradaGeneratedRegistry` |
| `Runtime/Modules/SerializableType.cs` | 26 | Resolves type from serialized `_assemblyQualifiedName` |
| `Editor/PropertyDrawers/SerializableTypeDrawer.cs` | 27 | Editor-only: resolves type for property drawer display |
| `Editor/PropertyDrawers/SystemEntryDrawer.cs` | 50, 208 | Editor-only: resolves type for system entry drawer |
| `Editor/Inspectors/EntityMediatorInspector.cs` | 79 | Editor-only: hardcoded type lookup |

### Activator.CreateInstance (Dynamic Instantiation)

| File | Line | Context |
|------|------|---------|
| `Runtime/Modules/SystemRunner.cs` | 265 | Creates ECS system instances from `SystemEntry.GetSystemType()` |
| `Runtime/Modules/ModuleRegistry.cs` | 132 | Creates `IModuleInstaller` instances from discovered types |
| `Editor/Windows/StradaEntityInspectorWindow.cs` | 855 | Editor-only: creates component instances for inspector |

### MakeGenericMethod (Generic Method Construction)

| File | Line(s) | Context |
|------|---------|---------|
| `Runtime/Modules/ModuleBuilder.cs` | 57, 67 | Constructs generic `Register<T>` calls from runtime Type objects |
| `Runtime/DI/Container.cs` | 273 | Constructs generic `CreateDirectFactoryWrapper<T>` |
| `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs` | 171, 180, 190 | Constructs generic `Register` calls for auto-binding |
| `Editor/Windows/StradaEntityInspectorWindow.cs` | 859 | Editor-only |
| `Editor/DataProviders/BusDataProvider.cs` | 191 | Editor-only |

### MethodInfo.Invoke (Reflective Invocation)

| File | Line(s) | Context |
|------|---------|---------|
| `Runtime/DI/InjectionProcessor.cs` | 100 | Invokes `[Inject]`-attributed methods on DI targets |
| `Runtime/DI/LifecycleProcessor.cs` | 26, 41 | Invokes `[PostConstruct]`/`[DeConstruct]` lifecycle methods |
| `Runtime/DI/ContainerBuilderExtensions.cs` | 56 | Invokes `RegisterAll` on source-generated registry |
| `Runtime/DI/Container.cs` | 274 | Invokes `CreateDirectFactoryWrapper` generic method |
| `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs` | 172, 181, 191 | Invokes generic `Register` methods |
| `Runtime/Modules/ModuleBuilder.cs` | 58, 68 | Invokes generic `Register` methods |
| `Runtime/ECS/Storage/ComponentStorage.cs` | 177, 195 | Invokes `Get`/`Set` on typed storage via reflection |

### BindingFlags.NonPublic (Access to Non-Public Members)

| File | Line(s) | Context |
|------|---------|---------|
| `Runtime/DI/InjectionProcessor.cs` | 57 | Scans private fields/methods/properties for `[Inject]` |
| `Runtime/DI/LifecycleProcessor.cs` | 12 | Scans private methods for `[PostConstruct]`/`[DeConstruct]` |
| `Runtime/DI/Container.cs` | 272 | Accesses private static method on own type |
| Multiple Editor files | Various | Editor tooling accessing internal state for debugging/inspection |

### AppDomain.CurrentDomain.GetAssemblies (Assembly Enumeration)

| File | Line | Context |
|------|------|---------|
| `Runtime/Modules/ModuleRegistry.cs` | 28 | Module discovery |
| `Runtime/Modules/RuntimeSystemDiscovery.cs` | 109 | System discovery |
| `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs` | 51 | Auto-binding scanning |
| Multiple Editor files | Various | Editor tooling for code generation, validation, debugging |

### Assembly.GetTypes (Type Enumeration)

Used in all assembly scanning locations listed above, always following `GetAssemblies()`.

### Expression.Compile (Compiled Expressions)

| File | Line(s) | Context |
|------|---------|---------|
| `Runtime/DI/Container.cs` | 297, 307 | Compiles factory lambdas for DI constructor injection |

### MakeGenericType (Generic Type Construction)

| File | Line | Context |
|------|------|---------|
| `Runtime/DI/TypeRegistry.cs` | 28 | Constructs `TypeId<T>` to retrieve static type IDs |

---

## Detailed Findings

### Finding 1: SerializableType → Activator.CreateInstance Pipeline

**Severity: MEDIUM**
**Category: Arbitrary Type Instantiation**

**Location:**
- `Runtime/Modules/SerializableType.cs:26` — `Type.GetType(_assemblyQualifiedName)`
- `Runtime/Modules/SystemRunner.cs:265` — `Activator.CreateInstance(systemType)`
- `Runtime/Modules/SystemEntry.cs:65` — bridges SerializableType to SystemRunner

**Description:**
`SerializableType` stores an assembly-qualified type name as a serialized string in Unity assets (ScriptableObjects). When `SystemRunner.CreateSystem()` is called, it resolves the type via `SerializableType.Type` (which calls `Type.GetType(_assemblyQualifiedName)`) and then calls `Activator.CreateInstance(systemType)`.

There is no validation that the resolved type implements `ISystem` before calling `Activator.CreateInstance`. The `as ISystem` cast on line 265 will return null for non-system types, but the constructor side effects of the instantiated type will have already executed.

**Data flow:**
```
Serialized asset string → SerializableType._assemblyQualifiedName → Type.GetType() → SystemEntry.GetSystemType() → Activator.CreateInstance()
```

**Risk assessment:**
- In a standard Unity build, serialized assets are baked into the build and not modifiable by end users. Risk is LOW.
- If the game loads asset bundles from untrusted sources (e.g., mods, downloaded content), an attacker could craft a malicious ScriptableObject with an arbitrary type name, causing instantiation of any type with a parameterless constructor. Risk becomes HIGH.
- The `as ISystem` cast does not prevent the constructor from running.

**Recommendation:**
Add a type validation check before `Activator.CreateInstance`:
```csharp
if (!typeof(ISystem).IsAssignableFrom(systemType))
{
    Debug.LogError($"[SystemRunner] Type {systemType.Name} does not implement ISystem");
    return null;
}
```

---

### Finding 2: ModuleRegistry Discovers and Instantiates IModuleInstaller from All Assemblies

**Severity: MEDIUM**
**Category: Arbitrary Type Instantiation**

**Location:**
- `Runtime/Modules/ModuleRegistry.cs:28,122,132`

**Description:**
`ModuleRegistry.DiscoverModules()` enumerates all loaded assemblies, finds every type implementing `IModuleInstaller` with a parameterless constructor, and calls `Activator.CreateInstance(type)` on each. While it does check `installerType.IsAssignableFrom(t)`, this means any assembly loaded into the AppDomain that contains an `IModuleInstaller` implementation will have its installer automatically instantiated and executed.

**Risk assessment:**
- If an attacker can load a malicious assembly into the AppDomain (e.g., via mod support, plugin system, or asset bundle with managed code), any `IModuleInstaller` in that assembly would be automatically discovered and instantiated.
- The `assemblyFilter` parameter provides a mitigation path but is optional and defaults to null (scan all).

**Recommendation:**
Consider using the `assemblyFilter` parameter by default to restrict scanning to known assembly name prefixes (similar to `RuntimeSystemDiscovery.ShouldSkipAssembly`).

---

### Finding 3: InjectionProcessor Accesses Non-Public Members via Reflection

**Severity: LOW**
**Category: Access Control Bypass via Reflection**

**Location:**
- `Runtime/DI/InjectionProcessor.cs:57-88`

**Description:**
The `InjectionProcessor` uses `BindingFlags.NonPublic` to scan for private fields, properties, and methods marked with `[Inject]`. It then sets field values, property values, and invokes methods reflectively. This is a standard DI pattern (used by VContainer, Zenject, etc.) but it bypasses normal access control.

**Risk assessment:**
- This is an intentional design pattern for dependency injection frameworks.
- The injection targets are controlled by the `[Inject]` attribute, which must be explicitly applied by the developer.
- No untrusted input controls which members are injected.
- Risk is LOW in the current architecture since only framework-registered types are injected.

**Recommendation:**
No action needed. This is standard DI framework behavior. Consider documenting that `[Inject]` on private members bypasses encapsulation intentionally.

---

### Finding 4: Container.CompileFactory Uses Expression.Compile Without Caching Concerns

**Severity: LOW**
**Category: Code Execution via Compiled Expressions**

**Location:**
- `Runtime/DI/Container.cs:290-308`

**Description:**
The DI container compiles expression trees into delegates for constructor injection. The compiled delegates are stored in `_factories` and `_scopedFactories` arrays indexed by type ID. The expressions are constructed from registered type information (constructor parameters mapped to other registered types).

**Risk assessment:**
- The expression trees are built from `Type` objects and `ConstructorInfo` already known to the container — not from user strings.
- Compiled expressions are cached in the factory arrays, preventing repeated compilation.
- No path exists for untrusted input to influence the expression tree construction.

**Recommendation:**
No action needed. The compiled expressions are safely constructed from internal type metadata.

---

### Finding 5: ContainerBuilderExtensions Uses Hardcoded Type.GetType for Source-Generated Registry

**Severity: INFO**
**Category: Dynamic Type Resolution**

**Location:**
- `Runtime/DI/ContainerBuilderExtensions.cs:41-43, 70-71`

**Description:**
The `TryUseSourceGenerated` method looks up `Strada.Generated.StradaGeneratedRegistry` by hardcoded string. This is a common Unity pattern for bridging framework code with source-generated code that lives in a different assembly.

**Risk assessment:**
- The type name is hardcoded, not derived from external input.
- An attacker would need to inject an assembly containing this exact type name into the AppDomain to hijack registration — but they would also need to control assembly loading.
- Risk is negligible in standard Unity deployment.

**Recommendation:**
No action needed. This is a standard source-generation bridging pattern.

---

### Finding 6: TypeRegistry Uses MakeGenericType for Type ID Allocation

**Severity: INFO**
**Category: Dynamic Type Resolution**

**Location:**
- `Runtime/DI/TypeRegistry.cs:27-30`

**Description:**
`TypeRegistry.GetId(Type type)` uses `typeof(TypeId<>).MakeGenericType(type)` to access the static `Id` field of the generic type. This is used to assign integer IDs to types for the DI container's index-based resolution.

**Risk assessment:**
- The `type` parameter comes from registered service/implementation types, not from external input.
- `MakeGenericType` with an attacker-controlled type could theoretically cause unexpected generic instantiations, but no path for this exists in the current code.

**Recommendation:**
No action needed.

---

### Finding 7: RuntimeAutoBindingScanner Scans Assemblies with Configurable Filters

**Severity: LOW**
**Category: Assembly Enumeration / Type Instantiation**

**Location:**
- `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs:46-68, 80`

**Description:**
The scanner iterates all loaded assemblies (filtered by include/exclude patterns) and examines every type for auto-registration attributes. Default include patterns are `Strada.*`, `Game.*`, `Assembly-CSharp`. Default exclude patterns filter out `Unity.*`, `System.*`, etc.

**Risk assessment:**
- The include/exclude pattern matching uses simple string prefix/suffix matching, not regex.
- If a malicious assembly were loaded with a name matching include patterns (e.g., `Game.Malicious`), its types with `[AutoRegister]` attributes would be automatically registered in the DI container.
- The risk requires the attacker to load an assembly into the AppDomain first.

**Recommendation:**
Consider providing a mechanism to lock down assembly scanning to an explicit allowlist rather than pattern-based filtering in security-sensitive deployments.

---

### Finding 8: ComponentStorage Uses Reflection for Boxed Get/Set

**Severity: INFO**
**Category: Reflective Invocation**

**Location:**
- `Runtime/ECS/Storage/ComponentStorage.cs:167-200`

**Description:**
`GetComponentBoxed` and `SetComponentBoxed` use `GetMethod("Get")` / `GetMethod("Set")` and `Invoke` to access typed storage in a type-erased manner. This is a convenience path for editor tooling and debugging.

**Risk assessment:**
- The `componentType` parameter comes from internal entity component type lookups, not external input.
- The methods are invoked on internally managed storage objects.
- No security concern in the current usage.

**Recommendation:**
No action needed.

---

## Security Analysis Checklist

| Check | Status | Notes |
|-------|--------|-------|
| Type confusion attacks | Low risk | `SerializableType` resolves types from serialized strings but constrained by Unity asset pipeline |
| Arbitrary type instantiation | Medium risk | `Activator.CreateInstance` in `SystemRunner` lacks pre-instantiation type validation; mitigated by Unity asset trust model |
| Access control bypass via reflection | Low risk | Standard DI pattern using `[Inject]` attribute; intentional design |
| Code execution via compiled expressions | Low risk | Expression trees built from internal type metadata only |
| Assembly loading attacks | Not applicable | No `Assembly.Load`/`LoadFrom`/`LoadFile` calls found in codebase |

---

## Summary Table

| # | Finding | Severity | Location | Recommendation |
|---|---------|----------|----------|----------------|
| 1 | SerializableType to Activator.CreateInstance pipeline lacks type validation | MEDIUM | `SystemRunner.cs:265`, `SerializableType.cs:26` | Add `ISystem` assignability check before instantiation |
| 2 | ModuleRegistry auto-discovers and instantiates from all assemblies | MEDIUM | `ModuleRegistry.cs:28,132` | Default to filtered assembly scanning |
| 3 | InjectionProcessor accesses non-public members | LOW | `InjectionProcessor.cs:57` | No action (standard DI pattern) |
| 4 | Container compiles expressions from type metadata | LOW | `Container.cs:297,307` | No action (safely constructed) |
| 5 | Hardcoded Type.GetType for source-generated registry | INFO | `ContainerBuilderExtensions.cs:41-43` | No action |
| 6 | TypeRegistry uses MakeGenericType | INFO | `TypeRegistry.cs:27-30` | No action |
| 7 | Auto-binding scanner uses pattern-based assembly filtering | LOW | `RuntimeAutoBindingScanner.cs:46-68` | Consider explicit allowlist option |
| 8 | ComponentStorage boxed access via reflection | INFO | `ComponentStorage.cs:167-200` | No action |
