# Strada Competitive Analysis & Architecture Design

## Executive Summary

This document analyzes VContainer, Reflex, Svelto ECS, Unity DOTS, Strange IoC, and Photon Quantum to design a unified MVCS+ECS framework that defeats all competitors in their respective strengths.

---

## Competitor Deep Dive

### 1. Reflex - Current Performance King (DI)

**Performance Claims:**
- 414% faster than VContainer
- 800% faster than Zenject
- 28% less memory allocation than VContainer

**Why It's Fast:**
- No runtime Emit (AOT-friendly, IL2CPP compatible)
- Immutable container design (lock-free thread safety)
- Contract table for O(1) bulk resolution

**Architecture:**
- Hierarchical containers (ProjectScope → SceneScope)
- Explicit opt-in injection (no auto-magic)
- Singleton, Transient, Factory lifetimes

**Weakness:**
- DI-only, no ECS integration
- No structural patterns (Commands, Mediators)

---

### 2. VContainer - Popular & Well-Documented

**Performance:**
- 5-10x faster than Zenject
- Zero-allocation resolution
- Source generator acceleration

**Key Features:**
- PlayerLoopSystem integration
- Entry point pattern (pure C# initialization)
- Flexible scoping with async support

**Weakness:**
- Slower than Reflex
- No ECS integration
- No MVCS patterns

---

### 3. Svelto ECS - Best ECS Design Philosophy

**Key Innovations:**
- Groups = Entity States (not just component sets)
- Filters add query dimensions without structural changes
- EntityDescriptors enforce domain understanding
- Static archetypes prevent "structural change explosion"

**Performance:**
- Allocation-free operations
- Native collections across platforms
- Cache-friendly iteration
- Burst compatible

**Design Philosophy:**
> "Maintainability and code design improvements over pure performance"

**Weakness:**
- No DI container
- No MVCS integration
- Steeper learning curve

---

### 4. Unity DOTS/ECS - Official Solution

**Architecture:**
- Archetype-chunk storage (16KB chunks)
- EntityQuery caching
- Change detection with version numbers
- ISystem + SystemAPI (preferred over SystemBase)

**Performance:**
- Best raw performance with Burst
- Parallel job execution
- Memory-aligned data

**Weakness:**
- Complex API
- Poor integration with traditional Unity
- No MVCS patterns
- Difficult for UI/game logic

---

### 5. Strange IoC - Best MVCS Architecture

**MVCS Pattern:**
- **Model**: Data holders (state)
- **View**: MonoBehaviours (presentation)
- **Controller**: Commands (logic)
- **Service**: External communication

**Key Features:**
- Signals for type-safe events
- CommandBinder maps signals to commands
- Mediators translate Views to application
- Unidirectional data flow

**Weakness:**
- Deprecated/unmaintained
- Heavy reflection usage
- No ECS integration
- Poor performance

---

### 6. Photon Quantum - Best Asset Architecture

**Asset System:**
- `AssetObject` extends ScriptableObject
- `asset_ref<T>` for safe ECS references
- Immutable runtime assets
- Deterministic GUIDs for networking

**Configuration Pattern:**
- SimulationConfig, RuntimeConfig separation
- SystemsConfig for system registration
- QuantumDefaultConfigs for centralized defaults

**Weakness:**
- Tied to Photon networking
- Not open source
- Complex setup

---

## SWOT Analysis for Strada

### Strengths to Build
| Competitor | Feature to Adopt |
|------------|-----------------|
| Reflex | No-emit resolution, immutable containers |
| VContainer | Source generators, PlayerLoop integration |
| Svelto | Groups as states, Filters, EntityDescriptors |
| Unity DOTS | Archetype storage, Burst compatibility |
| Strange IoC | Signals, Commands, Mediators |
| Photon Quantum | AssetObject pattern, config separation |

### Opportunities
1. **Unified Architecture** - No competitor unifies MVCS + ECS
2. **Best-of-breed** - Combine best patterns from each
3. **Modern C#** - Use latest language features
4. **Developer Experience** - Better tooling than all competitors

### Threats
1. Unity's continued DOTS development
2. Reflex's performance lead
3. VContainer's community adoption

---

## Strada Unified Architecture: MECS

**Model-Entity-Controller-Service** - The first framework to truly unify MVCS and ECS.

### Core Principles

1. **Data is Everything**
   - All state lives in Components (ECS) or Models (MVCS)
   - ScriptableObjects are configuration containers only
   - No logic in data classes

2. **Groups are States**
   - Entities belong to Groups representing their state
   - State transitions = group swaps (not add/remove components)
   - Filters query across groups without structural changes

3. **Signals Connect Layers**
   - ECS → MVCS: Systems publish Signals
   - MVCS → ECS: Commands enqueue component mutations
   - Zero coupling between layers

4. **Commands are Controllers**
   - Business logic lives in Commands
   - Commands can access both ECS and MVCS
   - Async command support for services

5. **Modules are Boundaries**
   - Each module has its own assembly
   - Modules declare dependencies explicitly
   - Cross-module communication via Signals only

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION                              │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐                         │
│  │  View   │  │  View   │  │  View   │  (MonoBehaviours)       │
│  └────┬────┘  └────┬────┘  └────┬────┘                         │
│       │            │            │                               │
│  ┌────▼────┐  ┌────▼────┐  ┌────▼────┐                         │
│  │Mediator │  │Mediator │  │Mediator │  (Signal listeners)     │
│  └────┬────┘  └────┬────┘  └────┬────┘                         │
└───────┼────────────┼────────────┼───────────────────────────────┘
        │            │            │
        ▼            ▼            ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SIGNAL BUS                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Signal<T>  Signal<T1,T2>  Signal<T1,T2,T3>             │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────┬────────────┬────────────┬───────────────────────────────┘
        │            │            │
        ▼            ▼            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      CONTROLLER LAYER                            │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │Command  │  │Command  │  │Command  │  │Command  │           │
│  │(MVCS)   │  │(ECS)    │  │(Hybrid) │  │(Async)  │           │
│  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘           │
└───────┼────────────┼────────────┼────────────┼──────────────────┘
        │            │            │            │
        ▼            ▼            ▼            ▼
┌─────────────────────────────────────────────────────────────────┐
│                       MODEL/DATA LAYER                           │
│                                                                  │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐ │
│  │      MVCS SIDE      │    │         ECS SIDE                │ │
│  │  ┌───────────────┐  │    │  ┌───────────────────────────┐  │ │
│  │  │    Models     │  │    │  │   EntityManager           │  │ │
│  │  │  (IModel<T>)  │  │    │  │   ┌─────────────────┐     │  │ │
│  │  └───────────────┘  │    │  │   │   Archetypes    │     │  │ │
│  │  ┌───────────────┐  │    │  │   │  ┌───────────┐  │     │  │ │
│  │  │   Services    │  │    │  │   │  │  Chunks   │  │     │  │ │
│  │  │  (IService)   │  │    │  │   │  └───────────┘  │     │  │ │
│  │  └───────────────┘  │    │  │   └─────────────────┘     │  │ │
│  └─────────────────────┘    │  │   ┌─────────────────┐     │  │ │
│                             │  │   │     Groups      │     │  │ │
│                             │  │   │   (States)      │     │  │ │
│                             │  │   └─────────────────┘     │  │ │
│                             │  └───────────────────────────┘  │ │
│                             └─────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
        │                              │
        ▼                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    SIMULATION LAYER (ECS)                        │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │ System  │  │ System  │  │ System  │  │ System  │           │
│  │(Update) │  │(Physics)│  │(Render) │  │(AI)     │           │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘           │
└─────────────────────────────────────────────────────────────────┘
```

### Data Architecture (Photon Quantum Pattern)

```
Modules/
  InputModule/
    Scripts/
      Data/
        UnityObjects/          # ScriptableObject containers
          CD_Input.cs          # ConfigData_Input
        ValueObjects/          # Serialized data classes
          InputConfig.cs       # Actual data
          InputBindings.cs
      Interfaces/
        IInputService.cs
      Controllers/
        InputController.cs
      Systems/
        InputSystem.cs
      Commands/
        ProcessInputCommand.cs
```

**Naming Conventions:**
- `CD_` prefix: ConfigData ScriptableObjects
- `Config` suffix: Runtime configuration data
- `Data` suffix: Serialized value objects
- `Spec` suffix: Entity specifications (like Quantum)

---

## Performance Targets

### DI Resolution (10k transients, 4-level depth)
| Framework | Target | Current |
|-----------|--------|---------|
| Strada | <8ms | 12.2ms |
| Reflex | 10ms | - |
| VContainer | 51ms | - |
| Zenject | 100ms+ | - |

### ECS Iteration (100k entities)
| Operation | Target |
|-----------|--------|
| Single component | <1ms |
| 2-component query | <2ms |
| Group filter | <0.5ms |

### Memory
| Metric | Target |
|--------|--------|
| DI resolution | 0 allocation after warmup |
| Entity creation | <64 bytes per entity |
| Signal dispatch | 0 allocation |

---

## Implementation Priorities

### Phase 1: Foundation (Current)
- [x] FastContainer DI
- [x] Basic ECS (Entity, Component, SparseSet)
- [x] Signals
- [ ] Query system with filters

### Phase 2: Integration
- [ ] Archetype storage
- [ ] Groups (entity states)
- [ ] Command-Signal binding
- [ ] Mediator pattern

### Phase 3: Performance
- [ ] Source generators
- [ ] Burst-compatible queries
- [ ] Zero-allocation events
- [ ] Native container pooling

### Phase 4: Developer Experience
- [ ] Entity debugger window
- [ ] Performance monitor
- [ ] Module scaffolding tools
- [ ] Config data wizard

---

## Key Differentiators

1. **Only Unified Framework**: MVCS + ECS in one coherent architecture
2. **Groups as States**: Svelto's best idea, built into core
3. **Zero-Reflection Resolution**: Match Reflex performance
4. **Photon-style Assets**: Clean config/runtime separation
5. **Strange-style Commands**: Type-safe signal-command binding
6. **Modern C# 9+**: Records, pattern matching, source generators

---

## References

- [Reflex GitHub](https://github.com/gustavopsantos/Reflex)
- [VContainer GitHub](https://github.com/hadashiA/VContainer)
- [Svelto ECS GitHub](https://github.com/sebas77/Svelto.ECS)
- [Unity DOTS Documentation](https://docs.unity3d.com/Packages/com.unity.entities@0.50/manual/ecs_core.html)
- [Strange IoC Guide](https://strangeioc.github.io/strangeioc/TheBigStrangeHowTo.html)
- [Photon Quantum Assets](https://doc.photonengine.com/quantum/current/manual/assets/assets-simulation)
