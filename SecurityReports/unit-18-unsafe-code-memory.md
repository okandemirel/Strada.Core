# Unit 18 -- Unsafe Code & Memory Safety (Cross-Cutting Security Review)

## Executive Summary

The Strata.Core ECS framework makes extensive use of `unsafe` code throughout its core data structures and query systems. All unsafe usage is concentrated in the `Runtime/ECS/` subsystem, specifically in the SparseSet data structure, EntityCommandBuffer, parallel job structs, and entity query iteration. The unsafe code is generally well-structured and follows Unity ECS conventions, but several findings warrant attention -- primarily around missing bounds checks in the command buffer reader, potential use-after-free with raw pointer exposure, and a memory leak in `JobSystemBase`.

**Risk Summary:**
- 1 MEDIUM finding (command buffer read overflow)
- 4 LOW findings (missing bounds checks, pointer lifetime, memory leak pattern, NativeDisableUnsafePtrRestriction usage)
- 3 INFO findings (general patterns)

---

## Inventory of Unsafe Code

### Files containing `unsafe` keyword or unsafe operations

| File | Unsafe Constructs | Purpose |
|------|-------------------|---------|
| `Runtime/ECS/Storage/SparseSet.cs` | `unsafe struct`, `UnsafeUtility.MemSet`, raw pointer casts (`int*`, `T*`), `GetUnsafePtr()` | Core ECS sparse set data structure |
| `Runtime/ECS/Jobs/EntityCommandBuffer.cs` | `unsafe struct`, `byte*` pointer arithmetic, `GetUnsafeReadOnlyPtr()`, raw casts | Deferred command recording/playback |
| `Runtime/ECS/Core/EntityManager.cs` | `unsafe` block, `UnsafeUtility.MemClear`, `GetUnsafePtr()` | Entity lifecycle, bulk clear |
| `Runtime/ECS/Jobs/ParallelComponentJob.cs` | `unsafe struct` (x4 variants), `NativeDisableUnsafePtrRestriction`, raw `int*`/`T*` | Burst-compiled parallel job structs |
| `Runtime/ECS/Query/EntityQuery.cs` | `unsafe` blocks, `int*`/`T*` pointer arithmetic | Single/multi-component query iteration |
| `Runtime/ECS/Query/EntityQueryExtended.cs` | `unsafe` blocks, `int*` pointer usage | 4-8 component query iteration |
| `Runtime/ECS/Query/FilteredQuery.cs` | `unsafe` blocks, `int*`/`T*` pointer arithmetic | Filtered query iteration |
| `Runtime/ECS/Jobs/EntityJobs.cs` | `unsafe` blocks, raw pointer passing | Job scheduling with pointer setup |
| `Runtime/ECS/Storage/ComponentStorage.cs` | `unsafe` block, `GetDenseEntityReadOnlyPtr()` | Entity index enumeration |
| `Runtime/ECS/Systems/JobSystemBase.cs` | `NativeArray`/`NativeList` via `EntityCommandBuffer` | Job system base with command buffer |
| `Editor/Windows/StradaEntityInspectorWindow.cs` | `Marshal.SizeOf` | Editor-only size display |

### Native Collection Usage

| Collection Type | Files | Allocator |
|----------------|-------|-----------|
| `NativeArray<int>` | SparseSet, EntityManager | Persistent, Temp |
| `NativeArray<byte>` | EntityManager | Persistent |
| `NativeArray<T>` | SparseSet | Persistent |
| `NativeList<int>` | EntityManager | Persistent |
| `NativeList<byte>` | EntityCommandBuffer | TempJob, Persistent |
| `NativeList<Entity>` | EntityCommandBuffer | TempJob, Persistent |

---

## Detailed Findings

### Finding 1: CommandReader.ReadBytes Missing Bounds Check

**Severity:** MEDIUM
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 279-283
**Category:** Buffer overflow

```csharp
public unsafe byte* ReadBytes(int count)
{
    byte* result = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
    _position += count;
    return result;
}
```

**Issue:** The `ReadBytes` method does not validate that `_position + count` is within the bounds of `_data`. If a corrupted or malformed command stream contains an oversized `size` field (read at lines 201, 219 via `reader.ReadInt()`), this would advance `_position` past the end of the buffer and return a pointer into unallocated memory. The `count` value comes from data written by `WriteComponent<T>` which writes `sizeof(T)`, but during playback, this value is read from the byte stream and is not validated.

**Exploitation scenario:** A corrupted command stream (e.g., from a race condition on the NativeList, or a bug in serialization) could cause an out-of-bounds read.

**Recommendation:** Add bounds validation before the pointer arithmetic:
```csharp
if (_position + count > _data.Length)
    throw new InvalidOperationException("Command stream read overflow");
```

---

### Finding 2: CommandReader.ReadInt / ReadULong Missing Bounds Check

**Severity:** LOW
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 257-276
**Category:** Buffer overflow

```csharp
public unsafe int ReadInt()
{
    int value = 0;
    var ptr = (byte*)&value;
    ptr[0] = _data[_position++];
    ptr[1] = _data[_position++];
    ptr[2] = _data[_position++];
    ptr[3] = _data[_position++];
    return value;
}
```

**Issue:** While `_data[_position++]` will trigger NativeArray bounds checking (in editor/debug builds), there is no pre-check that 4 (or 8) bytes remain before starting the read. A partially-consumed or truncated command stream would cause the first successful reads to advance `_position` before the bounds check fails on a subsequent byte, leaving the reader in an inconsistent state. In Burst-compiled release builds, NativeArray bounds checks may be stripped, converting this to an unguarded out-of-bounds access.

**Recommendation:** Add a remaining-bytes check at the start of each multi-byte read.

---

### Finding 3: SparseSet Exposes Raw Pointers Without Lifetime Guarantee

**Severity:** LOW
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 107-111
**Category:** Use-after-free potential

```csharp
public int* GetDenseEntityPtr() => (int*)_dense.GetUnsafePtr();
public T* GetDataPtr() => (T*)_data.GetUnsafePtr();
public int* GetDenseEntityReadOnlyPtr() => (int*)_dense.GetUnsafeReadOnlyPtr();
public T* GetDataReadOnlyPtr() => (T*)_data.GetUnsafeReadOnlyPtr();
public int* GetSparsePtr() => (int*)_sparse.GetUnsafePtr();
```

**Issue:** These methods return raw pointers to the underlying NativeArray memory. If the SparseSet resizes (via `EnsureSparseCapacity` or `EnsureDenseCapacity`), the old NativeArrays are disposed and replaced, invalidating any previously obtained pointers. This is an inherent trade-off in performance-oriented ECS design.

**Mitigating factors:** The query and job systems obtain these pointers and use them within a single iteration scope (ForEach loop or job Execute). Structural changes during iteration would cause issues, but the ECS command buffer pattern is designed to defer structural changes.

**Risk:** If a user-provided delegate in `ForEach` adds/removes components on the same storage being iterated, a resize could invalidate the pointers held in the loop. The framework does not enforce this invariant.

**Recommendation:** Document this constraint clearly. Consider adding a "version" counter to SparseSet that is checked after the loop to detect invalidation in debug builds.

---

### Finding 4: JobSystemBase EntityCommandBuffer Never Disposed

**Severity:** LOW
**File:** `Runtime/ECS/Systems/JobSystemBase.cs`, lines 21-32
**Category:** Memory leak

```csharp
protected EntityCommandBuffer CommandBuffer
{
    get
    {
        if (!_commandBufferCreated)
        {
            _commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            _commandBufferCreated = true;
        }
        return _commandBuffer;
    }
}
```

**Issue:** The `EntityCommandBuffer` is allocated with `Allocator.TempJob` and is created lazily, but there is no `Dispose()` call in the `JobSystemBase` lifecycle. The `OnDispose()`/`OnDestroy()` virtual methods do not dispose the command buffer. The buffer is only `Clear()`-ed on each update cycle (line 47), not disposed. `Allocator.TempJob` allocations that are not freed within 4 frames will trigger Unity leak warnings.

**Mitigating factors:** The buffer is reused across frames via `Clear()`, so the leak is bounded to one buffer per system. However, Unity's leak detection for TempJob allocations may still flag this.

**Recommendation:** Override disposal to call `_commandBuffer.Dispose()` if `_commandBufferCreated` is true. Alternatively, use `Allocator.Persistent` since the buffer is long-lived.

---

### Finding 5: NativeDisableUnsafePtrRestriction Used Extensively in Parallel Jobs

**Severity:** LOW
**File:** `Runtime/ECS/Jobs/ParallelComponentJob.cs`, 24 occurrences across 4 structs
**Category:** Safety bypass

**Issue:** Every pointer field in the parallel job structs uses `[NativeDisableUnsafePtrRestriction]`. This attribute disables Unity's safety system checks that prevent jobs from accessing memory they should not. While this is standard practice for ECS-style pointer passing, it means the job system's race condition detection is bypassed for these fields. If two jobs write to the same component data concurrently without proper dependency management, data corruption could occur silently.

**Mitigating factors:** The bounds check `entity < MaxSparseN` prevents out-of-bounds access on the sparse array. The negative-index guard (`if (idx < 0) return;`) prevents dereferencing invalid dense indices. These are appropriate safety measures.

**Recommendation:** This is an accepted pattern in Unity ECS. Ensure proper `JobHandle` dependency chains are maintained by system authors.

---

### Finding 6: SparseSet.Get and Set Missing Bounds Validation

**Severity:** INFO
**File:** `Runtime/ECS/Storage/SparseSet.cs`, lines 75-78, 102-105
**Category:** Null pointer / out-of-bounds dereference

```csharp
public T Get(int entityIndex)
{
    return _data[_sparse[entityIndex]];
}

public void Set(int entityIndex, T component)
{
    _data[_sparse[entityIndex]] = component;
}
```

**Issue:** Neither `Get` nor `Set` validates that `entityIndex` is within the sparse array bounds or that `_sparse[entityIndex]` is non-negative (indicating the entity exists). A call with an invalid entityIndex will either throw an index-out-of-range exception (in debug) or access invalid memory (in release). The `TryGet` method (line 86) does have proper validation and is the safe alternative.

**Mitigating factors:** Callers in `EntityManager` (`GetComponent`, `SetComponent`) check `IsActiveIndex` before delegating to `ComponentStorage`, which provides some protection. However, `ComponentStorage.Get` and `ComponentStorage.Set` do not recheck `Contains` before calling through.

---

### Finding 7: Integer Multiplication in MemSet/MemClear Size Calculations

**Severity:** INFO
**File:** `Runtime/ECS/Storage/SparseSet.cs` line 28, 185; `Runtime/ECS/Core/EntityManager.cs` lines 264-265
**Category:** Integer overflow in size calculation

```csharp
UnsafeUtility.MemSet(_sparse.GetUnsafePtr(), 0xFF, sparseCapacity * sizeof(int));
UnsafeUtility.MemClear(_versions.GetUnsafePtr(), _versions.Length * sizeof(int));
```

**Issue:** The multiplication `capacity * sizeof(int)` uses `int` arithmetic. For extremely large capacities (over ~536 million elements), this could theoretically overflow a 32-bit int, resulting in a smaller-than-expected MemSet/MemClear and leaving memory uninitialized.

**Mitigating factors:** Practical ECS entity counts are far below the overflow threshold. NativeArray allocation itself would fail long before reaching these sizes.

---

### Finding 8: Marshal.SizeOf Used in Editor Inspector

**Severity:** INFO
**File:** `Editor/Windows/StradaEntityInspectorWindow.cs`, line 988
**Category:** Safe usage

```csharp
size = System.Runtime.InteropServices.Marshal.SizeOf(type);
```

**Issue:** `Marshal.SizeOf` returns the marshalled size which may differ from the managed size. However, this is used purely for display in an editor window and wrapped in a try-catch. No security concern.

---

## Security Analysis Checklist

| Check | Status | Notes |
|-------|--------|-------|
| Buffer overflows | MEDIUM | CommandReader.ReadBytes has no bounds check (Finding 1) |
| Use-after-free | LOW | Raw pointer exposure from SparseSet could be invalidated by resize (Finding 3) |
| Double-free | PASS | SparseSet and EntityManager check `IsCreated` before Dispose; EntityManager has `_disposed` guard |
| Memory leaks | LOW | JobSystemBase command buffer uses TempJob but is never disposed (Finding 4) |
| Integer overflow in size/index calculations | INFO | Theoretical overflow in MemSet size args at extreme scale (Finding 7) |
| Null pointer dereference | INFO | SparseSet.Get/Set skip validation; callers partially protect (Finding 6) |
| Uninitialized memory access | PASS | NativeArray allocations use Unity's default clearing; MemSet is used to initialize sparse arrays to 0xFF |

---

## Summary Table

| # | Finding | Severity | File | Category |
|---|---------|----------|------|----------|
| 1 | CommandReader.ReadBytes missing bounds check | MEDIUM | EntityCommandBuffer.cs:279 | Buffer overflow |
| 2 | CommandReader.ReadInt/ReadULong no pre-check on remaining bytes | LOW | EntityCommandBuffer.cs:257-276 | Buffer overflow |
| 3 | SparseSet raw pointer exposure without lifetime guarantee | LOW | SparseSet.cs:107-111 | Use-after-free |
| 4 | JobSystemBase EntityCommandBuffer never disposed (TempJob leak) | LOW | JobSystemBase.cs:21-32 | Memory leak |
| 5 | NativeDisableUnsafePtrRestriction bypasses safety checks in parallel jobs | LOW | ParallelComponentJob.cs (24 uses) | Safety bypass |
| 6 | SparseSet.Get/Set skip entity existence validation | INFO | SparseSet.cs:75-78, 102-105 | Out-of-bounds |
| 7 | Integer multiplication in MemSet/MemClear size args | INFO | SparseSet.cs:28, EntityManager.cs:264 | Integer overflow |
| 8 | Marshal.SizeOf in editor (display only) | INFO | StradaEntityInspectorWindow.cs:988 | Safe usage |
