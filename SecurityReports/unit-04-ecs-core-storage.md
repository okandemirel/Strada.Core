# Security Review: Unit 4 — ECS Core & Storage

**Review Date:** 2026-03-07
**Reviewer:** Claude (Automated Security Analysis)
**Scope:** `Runtime/ECS/Core/`, `Runtime/ECS/Storage/`, `Runtime/ECS/Archetypes/`, `Runtime/ECS/Entity.cs`, `Runtime/ECS/IComponent.cs`

---

## Executive Summary

The ECS core and storage layer uses unsafe native memory operations extensively via Unity's `UnsafeUtility` and `NativeArray`/`NativeList` types. The most significant risks center on the `SparseSet<T>` struct, which performs raw pointer arithmetic and `MemSet` operations without full bounds validation on all code paths. Several methods in `SparseSet<T>` skip bounds checks before indexing into the sparse array, which can produce out-of-bounds reads or memory corruption. The `EntityManager` has a potential integer overflow in its capacity-doubling logic and missing version validation on some component operations. Overall, the code follows reasonable patterns for a performance-oriented ECS but has several gaps in defensive validation that could lead to memory corruption or crashes.

---

## Detailed Findings

### Finding 1: Missing Bounds Check in `SparseSet.Get` and `SparseSet.Set`

**Severity:** HIGH
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 75-78, 102-105
**Category:** Unsafe memory operations without bounds checking

`Get(int entityIndex)` directly accesses `_sparse[entityIndex]` without checking whether `entityIndex` is within the sparse array bounds or whether the resulting dense index is valid. If called with an out-of-range or removed entity index, this produces an out-of-bounds read from the sparse array (undefined behavior or Unity safety check exception in editor, potential memory corruption in builds). `Set` has the same problem.

```csharp
public T Get(int entityIndex)
{
    return _data[_sparse[entityIndex]]; // No bounds check on entityIndex
}

public void Set(int entityIndex, T component)
{
    _data[_sparse[entityIndex]] = component; // No bounds check on entityIndex
}
```

Compare with `TryGet` (line 86) which properly validates both `entityIndex < _sparse.Length` and `denseIndex >= 0 && denseIndex < _count`. The `Get` and `Set` methods lack these guards entirely.

**Recommendation:** Add bounds validation or use `Contains()` as a precondition. At minimum, add debug-only assertions.

---

### Finding 2: Missing Bounds Check in `SparseSet.GetRef` — Unsafe Pointer Dereference

**Severity:** HIGH
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 80-84
**Category:** Buffer overflow via unsafe pointer arithmetic

`GetRef` retrieves a raw pointer via `GetUnsafePtr()` and indexes it with the dense index from `_sparse[entityIndex]`. Neither `entityIndex` nor `denseIndex` is validated. If `entityIndex` is out of range for the sparse array, or the entity has been removed (sparse value is -1 / 0xFFFFFFFF), the resulting pointer dereference reads/writes arbitrary memory.

```csharp
public ref T GetRef(int entityIndex)
{
    int denseIndex = _sparse[entityIndex]; // No bounds check
    return ref ((T*)_data.GetUnsafePtr())[denseIndex]; // Raw pointer arithmetic with unchecked index
}
```

If `denseIndex` equals -1 (0xFFFFFFFF as unsigned), this indexes far outside the `_data` buffer.

**Recommendation:** Validate `entityIndex` and `denseIndex` before performing pointer arithmetic.

---

### Finding 3: `SparseSet.Remove` Does Not Validate `denseIndex` Against `_count`

**Severity:** MEDIUM
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 49-68
**Category:** Buffer underflow / data corruption

`Remove` checks that `entityIndex < _sparse.Length` and `_sparse[entityIndex] >= 0`, but does not verify that the dense index is actually less than `_count`. If `_count` has been corrupted or if internal state is inconsistent, `_dense[lastIndex]` (where `lastIndex = _count - 1`) could read from uninitialized memory, and the swap-remove logic would corrupt state.

Additionally, if `_count` is 0 when `Remove` is called with a sparse entry that is >= 0 (inconsistent state), `lastIndex` becomes -1, leading to out-of-bounds array access.

**Recommendation:** Add `denseIndex < _count` validation.

---

### Finding 4: `EntityManager.GetComponent` Missing Entity Existence Validation

**Severity:** HIGH
**File:** `Runtime/ECS/Core/EntityManager.cs`, lines 180-185
**Category:** Use-after-free / stale entity access

`GetComponent<T>` does not validate that the entity exists or that its version matches before accessing component storage. A destroyed entity whose index has been recycled could return the wrong entity's component data. Compare with `GetComponentRef<T>` (line 193) which properly calls `Exists(entity)` first.

```csharp
public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
{
    var storage = _store.GetOrCreateStorage<T>();
    return storage.Get(entity.Index); // No version/existence check
}
```

**Recommendation:** Add `Exists(entity)` or at minimum `IsActiveIndex(entity.Index)` check, consistent with `GetComponentRef`.

---

### Finding 5: `EntityManager.SetComponent` Missing Version Validation

**Severity:** MEDIUM
**File:** `Runtime/ECS/Core/EntityManager.cs`, lines 213-221
**Category:** Use-after-free / stale entity access

`SetComponent` calls `IsActiveIndex` which checks that the index is active, but does not verify that `entity.Version` matches `_versions[entity.Index]`. If entity index N was destroyed and recycled to a new entity, a stale `Entity` handle with the old version could overwrite the new entity's component.

```csharp
public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
{
    if (!IsActiveIndex(entity.Index)) // Checks active, but NOT version
        return;
    var storage = _store.GetOrCreateStorage<T>();
    storage.Set(entity.Index, component);
}
```

The same pattern affects `AddComponent`, `RemoveComponent`, and `HasComponent` — all use `IsActiveIndex` without version checks.

**Recommendation:** Replace `IsActiveIndex(entity.Index)` with `Exists(entity)` in all component-mutating methods to include version validation.

---

### Finding 6: Integer Overflow in `EntityManager.EnsureCapacity`

**Severity:** MEDIUM
**File:** `Runtime/ECS/Core/EntityManager.cs`, lines 291-311
**Category:** Integer overflow in index calculations

The capacity-doubling loop `newCapacity *= 2` can overflow `int.MaxValue` if the entity count grows very large, resulting in a negative or zero capacity and subsequent `NativeArray` allocation failure or wrap-around.

```csharp
int newCapacity = _versions.Length;
while (newCapacity < required)
    newCapacity *= 2; // Can overflow for large values
```

**Recommendation:** Add an overflow guard, e.g., `if (newCapacity > int.MaxValue / 2) throw ...` or use `Math.Min`.

---

### Finding 7: `EntityManager.CreateEntities` Capacity Pre-calculation May Be Insufficient

**Severity:** MEDIUM
**File:** `Runtime/ECS/Core/EntityManager.cs`, lines 66-94
**Category:** Buffer overflow

`CreateEntities` calls `EnsureCapacity(_nextEntityIndex + count)` at the start, but the loop may consume recycled indices (which may have indices greater than the pre-calculated capacity). If a recycled index exceeds the current capacity, the arrays may already have been resized to fit only `_nextEntityIndex + count`, and no re-check occurs for recycled indices. However, recycled indices are always < `_nextEntityIndex` by definition, so the pre-allocation is sufficient in practice, but the `_nextEntityIndex + count` calculation itself could overflow for extremely large batches.

**Recommendation:** Add integer overflow check for `_nextEntityIndex + count`.

---

### Finding 8: `SparseSet.EnsureSparseCapacity` Growth Calculation Overflow

**Severity:** MEDIUM
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 175-189
**Category:** Integer overflow in index calculations

`Math.Max(required, _sparse.Length * 3 / 2)` — the multiplication `_sparse.Length * 3` can overflow when `_sparse.Length` exceeds ~1.43 billion, wrapping to a negative value. This would cause `Math.Max` to select `required`, which is safe, but the intermediate overflow is still undefined behavior potential.

The same pattern exists in `EnsureDenseCapacity` (line 195).

**Recommendation:** Use `_sparse.Length / 2 + _sparse.Length` to avoid overflow, or add bounds checking.

---

### Finding 9: Exposed Raw Pointers from `SparseSet`

**Severity:** MEDIUM
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 107-111
**Category:** Unsafe memory access surface

Methods `GetDenseEntityPtr()`, `GetDataPtr()`, `GetSparsePtr()` expose raw pointers to internal arrays without any lifetime tracking. If the `SparseSet` is resized (arrays are reallocated and old ones disposed) while a caller holds a pointer, the pointer becomes dangling. This is a classic use-after-free pattern.

```csharp
public int* GetDenseEntityPtr() => (int*)_dense.GetUnsafePtr();
public T* GetDataPtr() => (T*)_data.GetUnsafePtr();
```

`ComponentStorage.GetEntityIndices()` (line 67-79) uses `GetDenseEntityReadOnlyPtr()` safely within a local scope, but other callers could misuse these pointers.

**Recommendation:** Document the lifetime contract clearly. Consider returning `NativeSlice` (already done for some methods) instead of raw pointers where feasible.

---

### Finding 10: `ComponentStore` Swallows Exceptions Silently

**Severity:** LOW
**File:** `Runtime/ECS/Storage/ComponentStorage.cs`, lines 174-183, 193-199
**Category:** Error handling / information disclosure

`GetComponentBoxed` and `SetComponentBoxed` catch all exceptions and silently return `null` or do nothing. This masks bugs like accessing components on destroyed entities, making issues harder to diagnose.

```csharp
catch
{
    return null; // Swallows all exceptions
}
```

**Recommendation:** At minimum, log warnings in debug builds when exceptions are caught.

---

### Finding 11: `ArchetypeManager` Entity List Grows Without Compaction

**Severity:** LOW
**File:** `Runtime/ECS/Archetypes/ArchetypeManager.cs`, lines 76-82
**Category:** Resource leak (memory)

`DestroyEntity<T>` calls `list.Remove(entity)` which is O(n) and does not shrink the list capacity. Over time in high-churn scenarios, archetype entity lists will accumulate unused capacity. This is a memory inefficiency rather than a security vulnerability.

**Recommendation:** Consider periodic compaction or use a different data structure for archetype tracking.

---

### Finding 12: `EntityManager.RestoreState` Incomplete Validation

**Severity:** MEDIUM
**File:** `Runtime/ECS/Core/EntityManager.cs`, lines 312-336
**Category:** Data integrity

`RestoreState` accepts external arrays and writes them into internal state with minimal validation. A caller providing `activeIndices` with values >= `_active.Length` silently skips them, but `versions` array values are written directly without verifying they are non-negative or consistent with active state. Malformed input could put the entity manager into an inconsistent state where version numbers do not match active flags.

**Recommendation:** Validate that version values are positive for active indices and zero for inactive indices.

---

### Finding 13: No Thread Safety in `EntityManager` or `SparseSet`

**Severity:** LOW
**File:** `Runtime/ECS/Core/EntityManager.cs`, `Runtime/ECS/Storage/SparseSet.cs`
**Category:** Race conditions

Neither `EntityManager` nor `SparseSet` provides any thread-safety guarantees. Concurrent creation/destruction of entities or concurrent component modifications could corrupt internal state. This is typical for ECS frameworks (thread safety is expected to be handled at the system scheduling level), but it bears noting.

**Recommendation:** Document thread-safety expectations. The `SystemScheduler` / job system should enforce exclusive access.

---

### Finding 14: `SparseSet.Add` Negative Entity Index

**Severity:** LOW
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 31-47
**Category:** Input validation

`Add(int entityIndex, T component)` calls `EnsureSparseCapacity(entityIndex + 1)`. If `entityIndex` is negative, `entityIndex + 1` could be 0 or negative, and `_sparse[entityIndex]` would throw an out-of-range exception (or in unsafe context, access invalid memory). The caller (`EntityManager`) uses indices starting at 1, but the `SparseSet` API does not enforce this.

**Recommendation:** Add a guard `if (entityIndex < 0) throw ArgumentOutOfRangeException`.

---

## Summary Table

| # | Finding | Severity | File | Category |
|---|---------|----------|------|----------|
| 1 | `SparseSet.Get`/`Set` missing bounds checks | HIGH | SparseSet.cs:75,102 | Unsafe memory access |
| 2 | `SparseSet.GetRef` unchecked pointer dereference | HIGH | SparseSet.cs:80 | Buffer overflow |
| 3 | `SparseSet.Remove` does not validate dense index vs count | MEDIUM | SparseSet.cs:49 | Data corruption |
| 4 | `EntityManager.GetComponent` missing existence validation | HIGH | EntityManager.cs:180 | Use-after-free |
| 5 | `EntityManager.SetComponent` and others missing version check | MEDIUM | EntityManager.cs:213 | Use-after-free |
| 6 | `EntityManager.EnsureCapacity` integer overflow | MEDIUM | EntityManager.cs:296 | Integer overflow |
| 7 | `EntityManager.CreateEntities` capacity overflow | MEDIUM | EntityManager.cs:69 | Buffer overflow |
| 8 | `SparseSet.EnsureSparseCapacity` growth overflow | MEDIUM | SparseSet.cs:179 | Integer overflow |
| 9 | Exposed raw pointers with no lifetime tracking | MEDIUM | SparseSet.cs:107-111 | Use-after-free |
| 10 | `ComponentStore` silently swallows exceptions | LOW | ComponentStorage.cs:174 | Error handling |
| 11 | Archetype entity list unbounded growth | LOW | ArchetypeManager.cs:76 | Memory leak |
| 12 | `RestoreState` incomplete input validation | MEDIUM | EntityManager.cs:312 | Data integrity |
| 13 | No thread safety guarantees | LOW | Multiple | Race conditions |
| 14 | `SparseSet.Add` negative entity index | LOW | SparseSet.cs:31 | Input validation |

**Totals:** 3 HIGH, 6 MEDIUM, 5 LOW, 0 CRITICAL, 0 INFO
