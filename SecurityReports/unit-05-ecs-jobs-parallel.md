# Security Review: Unit 5 -- ECS Jobs & Parallel Processing

**Review Date:** 2026-03-07
**Reviewer:** Automated Security Analysis
**Scope:** `Runtime/ECS/Jobs/`, `Runtime/ECS/Query/`
**Files Analyzed:**
- `Runtime/ECS/Jobs/EntityCommandBuffer.cs`
- `Runtime/ECS/Jobs/EntityJobs.cs`
- `Runtime/ECS/Jobs/IJobComponent.cs`
- `Runtime/ECS/Jobs/ParallelComponentJob.cs`
- `Runtime/ECS/Jobs/EntityManagerJobExtensions.cs`
- `Runtime/ECS/Query/EntityQuery.cs`
- `Runtime/ECS/Query/EntityQueryExtended.cs`
- `Runtime/ECS/Query/FilteredQuery.cs`
- `Runtime/ECS/Query/QueryBuilder.cs`

---

## Executive Summary

The ECS Jobs and Query subsystems make extensive use of unsafe pointer operations for performance-critical entity iteration and command buffering. The code follows standard Unity ECS patterns using Burst compilation and `NativeDisableUnsafePtrRestriction`. The most significant risks are in `EntityCommandBuffer.cs`, where a custom binary command stream is read back with pointer arithmetic that lacks bounds validation. Several medium-severity issues exist around the parallel job structs that disable Unity's pointer safety checks. The query system is internally sound but shares the same class of raw-pointer risks. No injection vectors were found in the query builder pattern.

---

## Detailed Findings

### Finding 1: CommandReader lacks bounds checking before reads

**Severity:** HIGH
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 250--283
**Category:** Buffer over-read / out-of-bounds access

The `CommandReader` inner struct checks `HasRemaining` only at the top of the `Playback` while-loop (line 100). Individual read methods (`ReadInt`, `ReadULong`, `ReadByte`, `ReadBytes`) increment `_position` without verifying that enough bytes remain. A corrupted or truncated command stream would cause `_position` to advance past `_data.Length`, producing out-of-bounds reads from the underlying `NativeArray`.

`ReadBytes` (line 279--283) is the most dangerous variant: it returns a raw pointer into the `NativeArray` at the current offset and advances `_position` by a caller-supplied `count`, with no validation that `_position + count <= _data.Length`.

```csharp
public unsafe byte* ReadBytes(int count)
{
    byte* result = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
    _position += count;
    return result;
}
```

**Recommendation:** Add bounds checks before each read. At minimum, validate in `ReadBytes` that `_position + count <= _data.Length` and throw or early-return on violation.

---

### Finding 2: Deferred entity index used without bounds validation

**Severity:** HIGH
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 225--235
**Category:** Out-of-bounds access / potential memory corruption

In `ReadEntity`, when `isDeferred == 1`, the code indexes directly into `_createdEntities[index]` without checking that `index` falls within `[0, _createEntityCount)`. A malformed command stream (or an integer overflow in the deferred index) would produce an out-of-bounds access on the `NativeList`.

```csharp
if (isDeferred == 1)
    return _createdEntities[index];
```

**Recommendation:** Validate `index >= 0 && index < _createdEntities.Length` before access.

---

### Finding 3: NativeDisableUnsafePtrRestriction on all parallel job pointers

**Severity:** MEDIUM
**File:** `Runtime/ECS/Jobs/ParallelComponentJob.cs`, lines 14--16, 37--41, 65--71, 98--106
**Category:** Safety system bypass / potential data races

All `ComponentJobParallel` variants mark every pointer field with `[NativeDisableUnsafePtrRestriction]`. This attribute silences Unity's job safety system, which normally prevents unsafe pointer access from parallel jobs. While the sparse-set bounds check (`entity < MaxSparse`) provides partial protection, the data pointers (`Components1`, etc.) are accessed at indices returned from the sparse array without independent bounds validation against the dense array length. If the sparse array contains stale or corrupt indices, this could read or write arbitrary memory.

**Recommendation:** Where possible, use `NativeArray` wrappers instead of raw pointers to retain safety checks. If raw pointers are required for Burst performance, add explicit debug-mode bounds assertions (e.g., `Unity.Collections.LowLevel.Unsafe` check macros or `#if ENABLE_UNITY_COLLECTIONS_CHECKS` guards).

---

### Finding 4: ComponentPlaybackHandler performs unchecked pointer cast

**Severity:** MEDIUM
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 349--352, 360--363
**Category:** Type confusion / memory corruption

`ComponentPlaybackHandler<T>.AddComponent` and `SetComponent` cast a `byte*` directly to `T*` and dereference it:

```csharp
T component = *(T*)data;
```

The `size` parameter is available but never validated against `sizeof(T)`. If the serialized size in the command stream differs from the actual struct size (due to versioning, corruption, or a type-hash collision), this produces a misaligned or undersized read, risking memory corruption or information disclosure from adjacent memory.

**Recommendation:** Assert `size == sizeof(T)` before the cast.

---

### Finding 5: Type hash collisions using FNV-1a on FullName only

**Severity:** MEDIUM
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 288--303
**Category:** Type confusion

`TypeHash<T>` computes a 64-bit FNV-1a hash of the type's `FullName`. While 64-bit collisions are statistically unlikely, the hash is used as the sole key to dispatch component playback (line 316). A collision between two component types would cause data to be deserialized into the wrong type, leading to silent memory corruption. No secondary validation (e.g., size check or type token) is performed.

**Recommendation:** Add a secondary check (e.g., compare `size` against the expected `sizeof(T)`) in the playback handler to detect mismatches.

---

### Finding 6: EntityCommandBuffer is not thread-safe but used with jobs

**Severity:** MEDIUM
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 20--47
**Category:** Race condition

`EntityCommandBuffer` is an unsafe struct with mutable state (`_commandStream`, `_createEntityCount`, `CommandCount`). It is not marked `[NativeContainer]` and has no thread-safety checks. If multiple jobs or threads write to the same buffer concurrently, data races will corrupt the command stream. The Unity pattern of one `EntityCommandBuffer.ParallelWriter` per thread is not implemented here.

**Recommendation:** Either implement a `ParallelWriter` inner struct with thread-indexed segments (as Unity's own ECB does), or document clearly that each buffer must be used from a single thread only.

---

### Finding 7: No validation of entity version in command playback

**Severity:** LOW
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 191--195, 225--234
**Category:** Use-after-free (logical)

When playing back `DestroyEntity`, `AddComponent`, `SetComponent`, or `RemoveComponent` commands, the entity's version is read from the stream (line 228) but never compared against the entity's current version in the `EntityManager`. If the entity was destroyed and its index recycled between recording and playback, commands will silently operate on the wrong entity.

**Recommendation:** Validate the entity version matches the current version in the `EntityManager` before executing each command.

---

### Finding 8: Raw pointer arithmetic in query ForEach without dense-array bounds check

**Severity:** LOW
**File:** `Runtime/ECS/Query/EntityQuery.cs`, lines 26--35, 80--100; `Runtime/ECS/Query/EntityQueryExtended.cs` (all `ForEach` methods); `Runtime/ECS/Query/FilteredQuery.cs` (all `ForEach` methods)
**Category:** Out-of-bounds read/write

Throughout the query system, `GetDataPtr()` returns a raw pointer, and indices from `GetDenseIndex()` are used for pointer arithmetic (e.g., `set1.GetDataPtr() + idx1`). While negative indices are filtered out (`if (idx < 0) return/continue`), there is no upper-bound check ensuring the index does not exceed the dense array length. A bug in the sparse set's `GetDenseIndex` returning a positive but out-of-range value would cause out-of-bounds memory access.

**Recommendation:** In debug/editor builds, add upper-bound assertions (`idx < set.Count`) before pointer arithmetic.

---

### Finding 9: Dispose does not prevent double-dispose of NativeList

**Severity:** LOW
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 131--137
**Category:** Resource management

The `Dispose` method sets `_isCreated = false` after disposal, which guards against double-dispose at the `EntityCommandBuffer` level. However, if `_commandStream` or `_createdEntities` are individually disposed elsewhere (e.g., copied by value in a struct), calling `Dispose` again on a copy would attempt to dispose already-freed native memory. Since `EntityCommandBuffer` is a struct, copies are implicit and easy to create accidentally.

**Recommendation:** Document that `EntityCommandBuffer` must not be copied after creation, or convert it to a class / use a shared disposal token.

---

### Finding 10: Static mutable dictionary in ComponentPlayback is not thread-safe

**Severity:** LOW
**File:** `Runtime/ECS/Jobs/EntityCommandBuffer.cs`, lines 305--337
**Category:** Race condition

`ComponentPlayback._handlers` is a static `Dictionary<ulong, IComponentPlaybackHandler>` with no synchronization. If `EnsureHandler<T>()` or `RegisterHandler<T>()` is called from multiple threads while `Playback` is reading from the dictionary, the dictionary can be corrupted. In Unity, this would typically only occur during initialization, but there is no enforcement of that assumption.

**Recommendation:** Use `ConcurrentDictionary` or add a lock, or initialize all handlers before any playback occurs and document this requirement.

---

### Finding 11: Query builder pattern -- no injection risk

**Severity:** INFO
**File:** `Runtime/ECS/Query/QueryBuilder.cs`, `Runtime/ECS/Query/FilteredQuery.cs`
**Category:** Injection analysis (clean)

The query builder uses compile-time generic type parameters exclusively. There are no string-based type lookups, no reflection-based component resolution, and no user-supplied strings that influence query construction. The `Also<T>()` and `None<T>()` filter methods use strongly-typed generics. This design is inherently safe against injection attacks.

---

## Summary Table

| # | Finding | Severity | File | Category |
|---|---------|----------|------|----------|
| 1 | CommandReader reads without bounds checking | HIGH | EntityCommandBuffer.cs:250--283 | Buffer over-read |
| 2 | Deferred entity index not validated | HIGH | EntityCommandBuffer.cs:225--235 | Out-of-bounds access |
| 3 | NativeDisableUnsafePtrRestriction on all job pointers | MEDIUM | ParallelComponentJob.cs | Safety bypass |
| 4 | Unchecked pointer cast in playback handler | MEDIUM | EntityCommandBuffer.cs:349--363 | Type confusion |
| 5 | Type hash collisions with no secondary check | MEDIUM | EntityCommandBuffer.cs:288--303 | Type confusion |
| 6 | EntityCommandBuffer not thread-safe | MEDIUM | EntityCommandBuffer.cs:20--47 | Race condition |
| 7 | Entity version not validated during playback | LOW | EntityCommandBuffer.cs:191--234 | Use-after-free (logical) |
| 8 | Query pointer arithmetic lacks upper-bound checks | LOW | EntityQuery.cs, EntityQueryExtended.cs, FilteredQuery.cs | Out-of-bounds access |
| 9 | Struct-based ECB risks double-dispose on copy | LOW | EntityCommandBuffer.cs:131--137 | Resource leak |
| 10 | Static handler dictionary not thread-safe | LOW | EntityCommandBuffer.cs:305--337 | Race condition |
| 11 | Query builder pattern is injection-safe | INFO | QueryBuilder.cs, FilteredQuery.cs | Clean |

**Total: 2 HIGH, 4 MEDIUM, 4 LOW, 1 INFO**
