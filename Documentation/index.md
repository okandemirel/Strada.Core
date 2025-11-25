# Strada Core Documentation

Welcome to **Strada**, the high-performance Unity framework that unifies **MVCS** (Model-View-Controller-Service) architecture with a modern **ECS** (Entity Component System).

## 🚀 Why Strada?

Most Unity frameworks force you to choose:
*   **Traditional IOC (Zenject/VContainer):** Great for architecture and UI, terrible for gameplay simulation speed.
*   **DOTS/ECS:** Incredible speed, but painful developer experience (DX) and poor UI integration.

**Strada chooses both.**

It provides a **Unified Bridge** that allows you to write high-level logic in standard C# (MVCS) while simulation-heavy gameplay runs in a cache-friendly ECS, all wired together by a world-class Dependency Injection system that beats competitors like Reflex and VContainer.

## 📚 Documentation Sections

### 🏛️ [Architecture](./architecture/unified-mvcs-ecs.md)
Understand the core "MECS" (Model-Entity-Controller-Service) pattern.
*   [The Unified Bridge](./architecture/unified-mvcs-ecs.md) - How ECS and MVCS talk.
*   [Dependency Injection](./architecture/dependency-injection.md) - The O(1) resolution engine.
*   [Signal Bus](./architecture/signals.md) - Zero-allocation event system.

### ⚡ [Performance](./performance/benchmarks.md)
Real-world numbers verified on actual hardware.
*   **DI Resolution:** ~0.25μs (Transient), ~0.05μs (Singleton).
*   **ECS Update:** ~1.7ms for 100k entities.
*   **Zero GC:** Allocation-free hot paths.

### 🎓 [Tutorials](./tutorials/quick-start.md)
Get up and running in minutes.
*   [Quick Start Guide](./tutorials/quick-start.md)
*   [Modular Architecture & Bridge Examples](./tutorials/modular-architecture.md)
*   [Creating your first Module](./tutorials/modules.md)

### 📦 [Modules](./modules/overview.md)
How Strada organizes code into independent, testable assemblies.

### 🛠️ [Editor Tools](./tools/editor-suite.md)
Master the built-in tooling.
*   [Module Generator](./tools/editor-suite.md#1-module-generator)
*   [Entity Debugger](./tools/editor-suite.md#2-entity-debugger)

---

## ⚡ Quick Look

```csharp
// 1. Define a Component (ECS)
public struct Velocity : IComponent { public float3 Value; }

// 2. Define a View (MVCS)
public class PlayerView : StradaView 
{
    // 3. Bind them together!
    // The Bridge automatically syncs ECS data to this GameObject
    [BindComponent] public void OnVelocityChanged(Velocity v) 
    {
        transform.position += v.Value * Time.deltaTime;
    }
}
```
