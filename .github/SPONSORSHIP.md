# Sponsor Strada.Core

**A high-performance Unity 6 framework unifying Patterns architecture with ECS simulation**

---

## Why Sponsor?

Strada.Core is a solo-developed, open-source Unity framework that combines enterprise-grade dependency injection, a SparseSet-based ECS, and a unified MessageBus into a single cohesive architecture. It is designed for developers who refuse to choose between clean architecture and raw performance.

Building and maintaining a framework of this scope — DI with expression-tree compiled factories, a hand-written + source-generated ECS query system (T1–T16), zero-alloc messaging, reactive bindings, modular architecture, and 424 tests — requires sustained, focused effort. Your sponsorship directly funds continued development, performance research, and documentation.

---

## What Your Sponsorship Supports

- **Performance engineering** — Continuous benchmarking, Burst compilation paths, and zero-alloc optimizations across DI, ECS, and messaging subsystems.
- **Source generation** — Roslyn-based generators for DI auto-binding and ECS queries (T9–T16), reducing boilerplate while maintaining type safety.
- **Documentation & examples** — Comprehensive guides for DI, ECS, Messaging, Sync, Modules, Pooling, StateMachine, and Benchmarks.
- **Testing infrastructure** — 330 functional tests + 94 performance benchmarks ensuring framework reliability across Unity versions.
- **New subsystems** — Planned features including enhanced job system integration, networking modules, and editor tooling.

---

## Sponsorship Tiers

### 🌱 Supporter — $5/month
- My sincere thanks and a mention in the project's sponsors section.
- Early access to release notes and development updates.

### ⚡ Backer — $15/month
- Everything in Supporter.
- Priority on GitHub Issues — your bug reports and feature requests are reviewed first.
- Access to a sponsors-only discussion channel.

### 🏗️ Builder — $50/month
- Everything in Backer.
- Your name/logo in the README sponsors section.
- Monthly development update with roadmap insights.
- Vote on feature priority for upcoming releases.

### 🏢 Studio — $150/month
- Everything in Builder.
- Your studio name/logo prominently displayed in the README and documentation site.
- Direct support channel for integration questions (response within 48 hours).
- Quarterly call to discuss your use case and influence roadmap direction.

### 💎 Enterprise — $500/month
- Everything in Studio.
- Dedicated onboarding session for your team.
- Custom feature development consideration (scoped to framework goals).
- Co-development credits in release changelogs.

---

## One-Time Contributions

Not ready for a monthly commitment? One-time sponsorships of any amount are welcome and appreciated. Every contribution helps fund the next iteration of performance improvements, documentation, and tooling.

---

## How Funds Are Used

| Category | Allocation |
|---|---|
| Development & engineering | 60% |
| Testing infrastructure & CI | 15% |
| Documentation & examples | 15% |
| Community & support | 10% |

---

## Current Stats

- **424** tests passing (330 functional + 94 performance benchmarks)
- **1.56x** manual `new()` overhead for DI resolution — competitive with the best Unity DI frameworks
- **6ns/entity** single-component query iteration on 100k entities
- **4ns/dispatch** MessageBus event throughput
- **0 bytes** GC allocation on singleton/scoped resolution paths
- **17x** parallel job speedup over sequential ForEach

---

## Sponsor Now

[![Sponsor on GitHub](https://img.shields.io/badge/Sponsor-GitHub%20Sponsors-ea4aaa?style=for-the-badge&logo=github-sponsors)](https://github.com/sponsors/okandemirel)

Your support keeps Strada.Core evolving as a world-class Unity framework — built with performance and clean architecture in mind.

---

*Strada.Core is developed and maintained by [Okan Demirel](https://github.com/okandemirel).*
