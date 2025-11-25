# Performance Benchmarks

> **Transparency Note:** These benchmarks were run on Apple M1 Pro hardware using Unity 6000.0.58f2 in PlayMode (IL2CPP/Release equivalent environment).

## Dependency Injection

Strada's DI container is optimized for **Gameplay Hot Paths**. While we recommend caching references, the container is fast enough to be used in `Update()` loops for moderate loads.

### Resolution Speed (Lower is Better)

| Framework | Singleton (100k ops) | Transient (10k ops) | GC Alloc |
|-----------|----------------------|---------------------|----------|
| **Strada** | **4ms (0.05μs)** | **2ms (0.25μs)** | **0 B** |
| Reflex | ~0.80μs | ~1.00μs | 0 B |
| VContainer| ~1.50μs | ~2.00μs | 0 B |
| Zenject | ~15.00μs | ~20.00μs | High |

**Verdict:** Strada is currently the fastest DI container available for Unity in pure resolution speed.

---

## Entity Component System (ECS)

Strada ECS is a **Managed ECS**. It uses standard C# classes and structs, meaning it does not use Unity's `UnsafeUtility` or `BlobAsset` memory layout (yet). This provides a massive usability boost at the cost of raw memory throughput compared to Unity DOTS.

### Entity Operations

| Operation | Count | Time | Avg per Entity |
|-----------|-------|------|----------------|
| **Create Entity** | 100,000 | 4ms | 0.04 μs |
| **Destroy Entity** | 100,000 | 13ms | 0.13 μs |
| **Add Component** | 100,000 | 11ms | 0.11 μs |

### Query & Iteration

| Operation | Count | Time | Note |
|-----------|-------|------|------|
| **Query (1 Comp)** | 100,000 | **<1ms** | Near zero cost (Array iteration) |
| **Query (2 Comp)** | 100,000 | 1ms | |
| **Update Loop** | 100,000 | 17ms | Complex logic simulation |

**Context:**
For a standard mobile game with ~5,000 active entities, Strada ECS takes **less than 0.2ms** overhead per frame. This leaves 16.4ms (60fps) or 33ms (30fps) entirely for your rendering and game logic.

---

## Bridge Performance

The cost of unifying MVCS and ECS is the "Binding" layer.

*   **Binding Sync:** ~400ns per sync.
*   **View Spawn:** ~2.2μs (pooled).

This is fast enough to handle thousands of visible units, but we recommend pure ECS (no Views) for particle effects or massive swarms (>5000 visible units).
