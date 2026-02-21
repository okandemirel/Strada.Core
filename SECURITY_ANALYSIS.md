# Strada Framework Core - Security Analysis Report

**Date:** 2026-02-21
**Scope:** Full codebase review of `com.strada.core` Unity framework package
**Severity Levels:** CRITICAL / HIGH / MEDIUM / LOW / INFO

---

## Executive Summary

Strada Core is a Unity MVCS+ECS framework with a custom DI container, EventBus, code generation, and native memory management. Since this is a game framework (not a web application), the threat model focuses on: **memory safety**, **thread safety**, **resource exhaustion**, **code injection via code generation**, and **DI container integrity**.

10 distinct vulnerability categories were identified across 18 affected files.

---

## 1. UNSAFE CODE - Buffer Overflows & Missing Bounds Checks

**Severity: HIGH**
**Affected Files:**
- `Runtime/ECS/Storage/SparseSet.cs`
- `Runtime/ECS/Jobs/EntityCommandBuffer.cs`

### 1a. SparseSet - Unguarded Access

```csharp
// SparseSet.cs:76 - No bounds check on entityIndex
public T Get(int entityIndex)
{
    return _data[_sparse[entityIndex]]; // Crashes if entityIndex >= _sparse.Length or < 0
}

// SparseSet.cs:102-104 - Same issue
public void Set(int entityIndex, T component)
{
    _data[_sparse[entityIndex]] = component; // No validation
}

// SparseSet.cs:80-84 - Returns raw unsafe pointer without validation
public ref T GetRef(int entityIndex)
{
    int denseIndex = _sparse[entityIndex]; // No check
    return ref ((T*)_data.GetUnsafePtr())[denseIndex];
}
```

**Risk:** `IndexOutOfRangeException` at best, memory corruption via dangling pointer at worst. `GetRef` returns a reference that becomes invalid if the entity is destroyed or the array is resized.

**Recommendation:**
```csharp
public T Get(int entityIndex)
{
    if (entityIndex < 0 || entityIndex >= _sparse.Length)
        throw new ArgumentOutOfRangeException(nameof(entityIndex));
    int denseIndex = _sparse[entityIndex];
    if (denseIndex < 0 || denseIndex >= _count)
        throw new InvalidOperationException($"Entity {entityIndex} has no component");
    return _data[denseIndex];
}
```

### 1b. EntityCommandBuffer - Buffer Read Overflows

```csharp
// EntityCommandBuffer.cs:257-265 - CommandReader.ReadInt() reads 4 bytes with no remaining check
public unsafe int ReadInt()
{
    int value = 0;
    var ptr = (byte*)&value;
    ptr[0] = _data[_position++]; // Could read past end of buffer
    ptr[1] = _data[_position++];
    ptr[2] = _data[_position++];
    ptr[3] = _data[_position++];
    return value;
}

// EntityCommandBuffer.cs:279-283 - Returns raw pointer without validating count
public unsafe byte* ReadBytes(int count)
{
    byte* result = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
    _position += count; // No check: _position + count <= _data.Length
    return result;
}
```

**Risk:** If the command stream is corrupted or truncated, the reader will read past allocated memory, causing undefined behavior in Burst-compiled code.

### 1c. ComponentPlaybackHandler - Size Mismatch

```csharp
// EntityCommandBuffer.cs:349-352
public unsafe void AddComponent(EntityManager em, Entity entity, byte* data, int size)
{
    T component = *(T*)data; // No check: size == sizeof(T)
    em.AddComponent(entity, component);
}
```

**Risk:** If `size != sizeof(T)`, this reads incorrect memory and creates a corrupt component.

**Recommendation:** Add a size assertion: `if (size != sizeof(T)) throw new InvalidOperationException(...);`

### 1d. SparseSet - Unbounded Allocation

```csharp
// SparseSet.cs:175-188
private void EnsureSparseCapacity(int required)
{
    if (required <= _sparse.Length) return;
    int newCapacity = Math.Max(required, _sparse.Length * 3 / 2);
    // No upper limit! entityIndex = int.MaxValue would allocate ~8GB
    var newSparse = new NativeArray<int>(newCapacity, _allocator);
    ...
}
```

**Risk:** A single entity with a very large index (e.g., `int.MaxValue`) causes out-of-memory allocation attempt.

**Recommendation:** Add `const int MaxSparseCapacity = 1_048_576;` and validate against it.

---

## 2. THREAD SAFETY - Race Conditions

**Severity: HIGH**
**Affected Files:**
- `Runtime/DI/Container.cs`
- `Runtime/Communication/EventBus.cs`
- `Runtime/Sync/ReactiveProperty.cs`
- `Runtime/DI/ContainerScope.cs`

### 2a. Container.Resolve - TOCTOU Race

```csharp
// Container.cs:45-68
public T Resolve<T>() where T : class
{
    if (_disposed) ThrowDisposed(); // Not under lock
    var typeId = TypeId<T>.Id;
    if (typeId <= _maxTypeId)
    {
        var index = _typeIdToIndex[typeId];
        if (index >= 0)
        {
            var lifetime = _lifetimes[index];
            if (lifetime == Lifetime.Singleton || lifetime == Lifetime.Scoped)
            {
                lock (_lock)
                {
                    return (T)_factories[index](this);
                }
            }
            return (T)_factories[index](this); // Transient: no sync
        }
    }
    ...
}
```

**Risk:** Thread A checks `_disposed = false`, Thread B calls `Dispose()`, Thread A proceeds to use disposed resources. For Transient lifetime, the factory runs completely unsynchronized.

### 2b. Container Singleton - Double Factory Execution

```csharp
// Container.cs:236-256 - Singleton factory can run multiple times
_factories[index] = _ =>
{
    var instance = _singletons[index];
    if (instance != null) return instance;

    instance = rawFactory(this); // Can execute concurrently!

    var prev = Interlocked.CompareExchange(ref _singletons[index], instance, null);
    if (prev != null)
    {
        if (instance is IDisposable d) d.Dispose(); // Duplicate disposed
        return prev;
    }
    ...
};
```

**Risk:** The raw factory runs without synchronization. If it has side effects (opens connections, registers handlers), those side effects execute multiple times even though only one instance is kept.

**Recommendation:** Use double-checked locking or `Lazy<T>` for singleton creation.

### 2c. EventBus - Publish/Subscribe Race

```csharp
// EventBus.cs:342-348 - Publish reads without lock
public void Publish(ref T message)
{
    var handlers = _handlers; // Snapshot reference
    for (int i = 0; i < handlers.Length; i++)
        handlers[i](message); // handlers array could be replaced mid-iteration
}
```

The copy-on-write pattern is mostly correct, but:
- `_handlers` is not `volatile`, so the JIT could cache the read.
- No memory barrier ensures the thread sees the latest array.

**Recommendation:** Mark `_handlers` as `volatile` or use `Volatile.Read()`.

### 2d. ReactiveProperty - No Thread Safety

```csharp
// ReactiveProperty.cs:40-48 - Not thread-safe
set
{
    if (_comparer.Equals(_value, value))
        return;
    _value = value;
    Notify(); // Iterates _handlers list which can be modified concurrently
}
```

**Risk:** Concurrent `Value` sets can cause missed notifications or `_handlers` list corruption during iteration.

**Recommendation:** Either document as single-threaded only or add synchronization.

---

## 3. CODE GENERATION - Injection Risk

**Severity: MEDIUM**
**Affected Files:**
- `Editor/CodeGen/SystemRegistryGenerator.cs`
- `Editor/ModuleGenerator/Pipeline/Steps/FileGenerationStep.cs`
- `Editor/ModuleGenerator/Utilities/TemplateProcessor.cs`

### 3a. Type Name Injection in Generated Code

```csharp
// SystemRegistryGenerator.cs:92-93
var typeName = GetFullTypeName(s.Type);
sb.AppendLine($"            typeof({typeName}),"); // Direct interpolation
```

While type names come from reflection (not direct user input), a malicious or buggy assembly with crafted type names containing `)` or `;` characters could inject arbitrary C# into the generated file.

### 3b. Namespace Not Validated in Templates

```csharp
// FileGenerationStep.cs:30-31
var name = context.Definition.ModuleName;
var ns = context.Definition.FullNamespace;
// name is validated by ModuleNameValidator (^[A-Z][a-zA-Z0-9]*$)
// ns is NOT validated the same way - could contain malicious content
```

**Recommendation:**
- Validate namespace with the same rigor as module names
- Sanitize type names before code generation: `if (!Regex.IsMatch(typeName, @"^[\w.]+$")) continue;`

---

## 4. DI CONTAINER - Reflection & Access Control

**Severity: MEDIUM**
**Affected Files:**
- `Runtime/DI/InjectionProcessor.cs`
- `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs`

### 4a. Private Member Injection

```csharp
// InjectionProcessor.cs:57
const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
```

The DI processor discovers and injects into **private** fields, properties, and methods. While this is common in DI frameworks, it bypasses C# access control.

**Risk:** Any type can have its private state modified if passed to `InjectionProcessor.Inject()`.

### 4b. Auto-Registration Service Takeover

```csharp
// RuntimeAutoBindingScanner.cs:46
includePatterns ??= new[] { "Strada.*", "Game.*", "Assembly-CSharp" };
```

Any assembly matching these patterns will have its `[AutoRegister]` types automatically registered. A third-party package with types in the `Game.*` namespace could register services that override legitimate implementations.

**Recommendation:**
- Add a whitelist mechanism for allowed assemblies
- Log warnings when auto-registration overrides existing registrations
- Consider adding an `[AllowAutoRegister]` assembly-level attribute

### 4c. Reflection Cache Not Invalidated on Domain Reload

```csharp
// InjectionProcessor.cs:11
private static readonly Dictionary<Type, TypeInjectionInfo> _cache = new(64);
```

The static cache persists across Unity domain reloads (in editor), potentially serving stale reflection data after code changes.

**Recommendation:** Clear cache on `[InitializeOnLoadMethod]` or subscribe to `AssemblyReloadEvents`.

---

## 5. ERROR HANDLING - Silent Failures

**Severity: MEDIUM**
**Affected Files:**
- `Runtime/ECS/Storage/ComponentStorage.cs`
- `Editor/CodeGen/SystemRegistryGenerator.cs`
- `Runtime/DI/AutoBinding/RuntimeAutoBindingScanner.cs`

### 5a. Empty Catch Blocks Hiding Bugs

```csharp
// ComponentStorage.cs:178-182 - Silent failure
public object GetComponentBoxed(int entityIndex, Type componentType)
{
    try { return method.Invoke(storage, new object[] { entityIndex }); }
    catch { return null; } // All exceptions swallowed
}

// ComponentStorage.cs:196-199 - Same issue
public void SetComponentBoxed(int entityIndex, Type componentType, object value)
{
    try { method.Invoke(storage, new object[] { entityIndex, value }); }
    catch { } // Silently ignores write failures
}
```

**Risk:** Bugs in component access (wrong entity index, type mismatch) are silently swallowed, leading to hard-to-diagnose issues.

```csharp
// SystemRegistryGenerator.cs:64
catch { } // Assembly scanning failures hidden

// RuntimeAutoBindingScanner.cs:66-67
catch (ReflectionTypeLoadException) { } // Type load failures hidden
```

**Recommendation:** At minimum, log warnings in catch blocks. For `SetComponentBoxed`, a silent failure means game state corruption goes undetected.

---

## 6. RESOURCE MANAGEMENT - Dispose & Leak Patterns

**Severity: MEDIUM**
**Affected Files:**
- `Runtime/DI/Container.cs`
- `Runtime/Communication/EventBus.cs`
- `Runtime/ECS/Core/EntityManager.cs`

### 6a. Container Dispose Race

```csharp
// Container.cs:130-131
public void Dispose()
{
    if (_disposed) return;
    _disposed = true; // Set before actual disposal
    ...
}
```

Setting `_disposed = true` before completing disposal means another thread could see the container as disposed while resources are still being cleaned up.

### 6b. EventBus Post-Dispose Usage

```csharp
// EventBus.cs:79-88 - Send() does not check _disposed
public void Send<TSignal>(ref TSignal signal) where TSignal : struct
{
    var id = SignalTypeId<TSignal>.Id;
    var handlers = _signalHandlers; // No disposed check!
    ...
}
```

After `Dispose()` calls `Clear()`, the handler arrays are zeroed but `Send` doesn't check `_disposed`. This causes `NullReferenceException` instead of a proper `ObjectDisposedException`.

### 6c. EntityManager Native Memory Leaks

If `EntityManager.Dispose()` is not called (e.g., exception during initialization), the `NativeArray` and `NativeList` allocations leak native memory since they use `Allocator.Persistent`.

**Recommendation:** Consider implementing a destructor/finalizer that logs a warning if Dispose was never called, or use `SafetyHandle` patterns.

---

## 7. DESERIALIZATION

**Severity: LOW**
**Affected Files:**
- `Editor/Benchmarking/BenchmarkPersistence.cs`
- `Editor/HotReload/HotReloadManager.cs`

### 7a. DateTime.Parse Without Format Validation

```csharp
// BenchmarkPersistence.cs:197
Timestamp = DateTime.Parse(timestamp), // No format, no culture
```

Malformed JSON timestamp strings cause `FormatException`. Not a security risk in isolation, but causes crash during data loading.

**Recommendation:** Use `DateTime.TryParseExact()` with expected format.

### 7b. HotReload Config Deserialization

```csharp
// HotReloadManager.cs:302-303
JsonUtility.FromJsonOverwrite(json, config);
```

Unity's `JsonUtility` is safe from type injection attacks, but overwriting a config object with malformed JSON could put the game in an inconsistent state.

---

## 8. OBJECT POOL - Double Return

**Severity: LOW**
**Affected Files:**
- `Runtime/Pooling/ObjectPool.cs`

```csharp
// ObjectPool.cs:61-74
public void Despawn(T instance)
{
    if (instance == null) return;
    if (_disposed) return;
    // No check: is this instance already in the pool?
    if (_available.Count < _maxSize)
        _available.Push(instance); // Same instance can be pushed multiple times
}
```

**Risk:** Returning the same object twice to the pool means two subsequent `Spawn()` calls return the same instance, causing shared mutable state bugs.

**Recommendation:** Add a `HashSet<T>` to track active instances, or add a debug-only check.

---

## 9. EVENT SYSTEM - Handler Lifecycle

**Severity: LOW**
**Affected Files:**
- `Runtime/Communication/EventBus.cs`

### 9a. Signal Handler Replacement Without Warning

```csharp
// EventBus.cs:136-141
public void RegisterSignalHandler<TSignal>(Action<TSignal> handler)
{
    lock (_lock)
    {
        _signalHandlers[id] = handler; // Silently replaces previous handler
    }
}
```

Unlike event subscriptions (which are additive), signal handlers are replaced silently. A second registration overwrites the first without warning.

### 9b. No Weak References for Subscribers

Event subscribers hold strong references to handler delegates, which in turn hold references to their target objects. This can prevent garbage collection of subscribers that forget to unsubscribe.

---

## 10. TYPE HASH COLLISION

**Severity: LOW**
**Affected Files:**
- `Runtime/ECS/Jobs/EntityCommandBuffer.cs`

```csharp
// EntityCommandBuffer.cs:292-302
private static ulong ComputeHash()
{
    string name = typeof(T).FullName ?? typeof(T).Name;
    ulong hash = 14695981039346656037UL; // FNV-1a
    foreach (char c in name)
    {
        hash ^= c;
        hash *= 1099511628211UL;
    }
    return hash;
}
```

FNV-1a on type names has collision probability. If two component types hash to the same value, `ComponentPlayback` will silently use the wrong handler, causing memory corruption.

**Recommendation:** Use `RuntimeTypeHandle.Value` as the hash key, or add collision detection in `ComponentPlayback.RegisterHandler()`.

---

## Summary Table

| # | Category | Severity | Files | Fix Effort |
|---|----------|----------|-------|------------|
| 1 | Unsafe Code / Buffer Overflows | HIGH | SparseSet, EntityCommandBuffer | Medium |
| 2 | Thread Safety / Race Conditions | HIGH | Container, EventBus, ReactiveProperty | High |
| 3 | Code Generation Injection | MEDIUM | SystemRegistryGenerator, FileGenerationStep | Low |
| 4 | DI Reflection & Access Control | MEDIUM | InjectionProcessor, AutoBindingScanner | Medium |
| 5 | Silent Error Handling | MEDIUM | ComponentStorage, AutoBindingScanner | Low |
| 6 | Resource Management / Leaks | MEDIUM | Container, EventBus, EntityManager | Medium |
| 7 | Deserialization | LOW | BenchmarkPersistence, HotReloadManager | Low |
| 8 | Object Pool Double Return | LOW | ObjectPool | Low |
| 9 | Event Handler Lifecycle | LOW | EventBus | Low |
| 10 | Type Hash Collision | LOW | EntityCommandBuffer | Low |

---

## Recommended Priority Order

1. **Add bounds checks to SparseSet** (Get, Set, GetRef) - prevents crashes and memory corruption
2. **Add buffer validation to CommandReader** - prevents buffer overflows in unsafe code
3. **Fix Container.Resolve TOCTOU race** - use `Volatile.Read` for disposed check
4. **Add size validation in ComponentPlaybackHandler** - prevents memory corruption
5. **Mark EventChannel._handlers as volatile** - prevents stale reads
6. **Add max capacity to SparseSet** - prevents OOM from large entity indices
7. **Validate namespace in code generation** - prevents injection
8. **Log warnings in empty catch blocks** - aids debugging
9. **Add double-return detection to ObjectPool** - prevents shared state bugs
10. **Add collision detection to TypeHash** - prevents silent data corruption
