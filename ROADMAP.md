# Strada Framework Roadmap

## Current State (v1.0)

Strada is a production-ready Unity framework that unifies MVCS and ECS patterns with:

- **DI Container**: Fast expression-compiled dependency injection with singleton/transient/scoped lifetimes
- **MVCS Architecture**: Model-View-Controller-Service pattern with reactive properties
- **ECS System**: Entity-Component-System with SparseSet storage and query system
- **Bridge System**: ViewMediator and ComponentBinding for MVCS-ECS integration
- **PlayerLoop Integration**: Zero-MonoBehaviour lifecycle management
- **StradaBus**: Unified command/event/query bus for cross-system communication

## Planned Features

### Near-Term

#### ECS Baking System
- Unity DOTS-style baking for editor-time conversion
- Subscene support for large worlds
- Hybrid entity workflows

#### Source Generators
- Query generation for 4+ component queries
- Auto-factory generation for DI
- Compile-time validation of registrations

#### Editor Tooling
- Entity inspector window
- System debugger
- DI container visualizer
- Performance profiler integration

### Mid-Term

#### ECS Communication
- Direct system-to-system messaging
- Job-safe event dispatch
- Burst-compatible event handling

#### Networking Integration
- Photon Quantum 3 compatibility layer
- State synchronization helpers
- Prediction/rollback support

#### Addressables Integration
- Asset loading through DI
- Entity prefab instantiation
- Memory management utilities

### Long-Term

#### Visual Scripting
- System graph editor
- Component flow visualization
- State machine designer

#### Performance Optimization
- SIMD-optimized queries
- Memory pooling improvements
- Parallel system execution

## Removed Features (Design Decisions)

The following were intentionally removed to maintain simplicity:

- **EntityBinding<T>**: Consolidated into ComponentBinding for simpler API
- **MediatorBase**: Consolidated into ViewMediator (pure C# over MonoBehaviour)
- **Empty ECS folders**: Removed placeholder directories (Backend, Baking, Attributes, Communication, Interfaces)

## Contributing

Future features should follow these principles:

1. **Zero allocation in hot paths**: No GC pressure during gameplay
2. **Minimal MonoBehaviour usage**: Pure C# where possible
3. **Test-first development**: Every feature needs unit tests and benchmarks
4. **No over-engineering**: YAGNI principle - only add what's needed
