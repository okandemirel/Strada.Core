# Strada Framework

**고성능 Unity 프레임워크 — Patterns 아키텍처와 ECS 시뮬레이션의 통합**

> **언어**: [English](README.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko.md)

[![Tests](https://img.shields.io/badge/tests-324%20passing-brightgreen)]()
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue)]()
[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-purple)]()

Strada는 엔터프라이즈급 의존성 주입과 고성능 ECS를 결합하여 깔끔한 모듈러 아키텍처로 제공합니다. 익숙한 패턴으로 UI를 구축하면서 동시에 고성능 시뮬레이션에는 ECS를 활용할 수 있습니다. 패러다임 간의 선택을 강요하지 않습니다.

---

## 목차

- [주요 기능](#주요-기능)
- [설치](#설치)
- [빠른 시작](#빠른-시작)
- [성능](#성능)
- [문서](#문서)
- [아키텍처](#아키텍처)
- [API 레퍼런스](#api-레퍼런스)
- [테스트](#테스트)
- [라이선스](#라이선스)

---

## 주요 기능

### 의존성 주입 ([문서](Documentation~/DI.md))
- **컨테이너**: Expression 트리 컴파일 팩토리 (수동 `new()` 대비 1.56배 오버헤드)
- **수명 주기**: Singleton, Transient, Scoped — 스레드 안전 초기화 지원
- **해제**: LIFO 순서로 해제 (의존 대상보다 의존하는 객체가 먼저 해제)
- **자동 바인딩**: `[AutoRegister]`, `[AutoRegisterSingleton]` 등 어트리뷰트 기반 서비스 등록
- **순환 참조 감지**: 빌드 시점에 순환 의존성을 감지하여 런타임 오류 방지
- **Zero-alloc 리졸브**: Singleton/Scoped 경로에서 GC 할당 없음
- **스레드 안전**: Volatile 읽기, ConcurrentDictionary, 락 기반 해제 안전성 보장

### 엔티티 컴포넌트 시스템 ([문서](Documentation~/ECS.md))
- **SparseSet 저장소**: 캐시 친화적 컴포넌트 순회 (엔티티당 6-28ns)
- **쿼리 시스템**: `ForEach<T1...T16>()` — 최대 8개까지 수작성, 9-16개는 소스 생성
- **안전성**: `EntityCommandBuffer`를 통한 순회 중 안전한 구조 변경
- **병렬 작업**: Burst 컴파일 작업으로 순차 처리 대비 17배 속도 향상
- **엔티티 재활용**: 버전 추적을 통한 자동 인덱스 재사용
- **소스 생성**: 9-16개 컴포넌트에 대한 컴파일 타임 쿼리 생성

### 메시징 ([문서](Documentation~/Messaging.md))
- **MessageBus**: 커맨드/쿼리/이벤트 통합 버스, 배열 인덱스 디스패치 (4ns/디스패치)
- **풀링된 커맨드**: ICommand 객체 실행 후 자동 풀 반환
- **Zero-alloc 퍼블리시**: 구조체 기반 메시지, 박싱 없음
- **예외 격리**: 핸들러 실패가 다른 구독자에게 영향을 주지 않음

### Patterns-ECS 동기화 ([문서](Documentation~/Sync.md))
- **이벤트 기반 통합**: ECS 시스템이 ComponentChanged 이벤트를 발행하고 Patterns 컨트롤러가 구독
- **EntityMediator**: ECS 엔티티를 UI 뷰에 바인딩하며 자동 동기화 및 MessageBus 통합 지원
- **양방향 흐름**: 컨트롤러가 MessageBus를 통해 ECS에 커맨드를 전송하고 이벤트를 수신

### 리액티브 바인딩 ([문서](Documentation~/Sync.md))
- **ReactiveProperty**: 변경 알림이 포함된 관찰 가능한 값
- **ReactiveCollection**: 추가/제거/초기화 이벤트가 포함된 관찰 가능한 리스트
- **ComputedProperty**: 자동 의존성 추적이 포함된 파생 값

### 모듈러 아키텍처 ([문서](Documentation~/Modules.md))
- **ModuleConfig**: ScriptableObject 기반 모듈 설정
- **인스펙터 시스템**: 드래그 앤 드롭으로 ECS 시스템 구성
- **IModuleBuilder**: VContainer와 유사한 플루언트 API로 DI 등록
- **시스템 탐색**: `[StradaSystem]` 어트리뷰트로 시스템 자동 검색
- **우선순위 정렬**: 모듈 초기화 순서 제어

### 유틸리티
- **ObjectPool**: 라이프사이클 훅(Spawn/Despawn)이 포함된 범용 풀링
- **StateMachine**: 조건부 전환이 포함된 타입 안전 FSM
- **TimerService**: 일시정지/재개를 지원하는 관리형 타이머

---

## 설치

Unity 프로젝트의 `Packages/manifest.json`에 다음을 추가하세요:

```json
{
  "dependencies": {
    "com.strada.core": "file:../Packages/com.strada.core"
  }
}
```

또는 `Packages/com.strada.core` 폴더를 프로젝트에 직접 복사하세요.

**요구 사항:**
- Unity 6000.0+ (Unity 6)
- .NET Standard 2.1

---

## 빠른 시작

### 의존성 주입

```csharp
using Strada.Core.DI;
using Strada.Core.DI.Attributes;

// 방법 1: 수동 등록
var builder = new ContainerBuilder();
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
using var container = builder.Build();

// 방법 2: 어트리뷰트를 사용한 자동 바인딩
[AutoRegisterSingleton(As = typeof(IPlayerService))]
public class PlayerService : IPlayerService { }

[AutoRegisterTransient]
public class EnemyController { }

// 어트리뷰트가 적용된 모든 타입 자동 등록
var builder = new ContainerBuilder();
builder.RegisterAutoBindings();  // [AutoRegister*] 어트리뷰트를 스캔합니다
using var container = builder.Build();
```

### ECS 시스템

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

// 컴포넌트 정의 (반드시 언매니지드 구조체여야 합니다)
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public int Current, Max; }
public struct Damage : IComponent { public int Value; }

// 최대 8개 컴포넌트 쿼리 (수작성, 최적 성능)
entityManager.ForEach<Position, Velocity, Health, Damage>(
    (int entity, ref Position pos, ref Velocity vel, ref Health hp, ref Damage dmg) =>
    {
        pos.X += vel.X * deltaTime;
    });

// 9-16개 컴포넌트 쿼리 (소스 생성)
entityManager.ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9>(...);

// 또는 SystemBase를 사용하여 더 깔끔한 코드 작성
public class MovementSystem : SystemBase<Position, Velocity>
{
    protected override void OnUpdateEntity(int entity, ref Position pos, ref Velocity vel, float dt)
    {
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
        pos.Z += vel.Z * dt;
    }
}
```

### 메시징

```csharp
using Strada.Core.Communication;

// 메시지를 구조체로 정의
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// 버스 설정
var bus = new MessageBus();

// 이벤트 구독
bus.Subscribe<PlayerDamaged>(e => Debug.Log($"Player took {e.Damage} damage"));

// 이벤트 발행 (할당 없음)
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// 커맨드 핸들러 등록
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnEnemyAt(cmd.X, cmd.Y));
bus.Send(new SpawnEnemy { X = 10, Y = 20 });
```

### 리액티브 프로퍼티

```csharp
using Strada.Core.Sync;

// 리액티브 프로퍼티 생성
var health = new ReactiveProperty<int>(100);

// 변경 사항 구독
health.Subscribe(value => healthBar.SetValue(value));

// 값 변경 시 구독자에게 자동 알림
health.Value = 75; // healthBar가 자동으로 업데이트됩니다
```

---

## 성능

Apple Silicon (Unity 6, Mono) 환경에서 측정한 **정직한 벤치마크**입니다:

### DI 컨테이너

| 연산 | 시간 | 비고 |
|------|------|------|
| 단순 Transient | **0.11μs** | 단일 클래스, 의존성 없음 |
| 4단계 깊이 체인 | **0.27μs** | A→B→C→D 의존성 체인 |
| 광범위 서비스 (5개 의존성) | **0.42μs** | 5개의 주입된 의존성을 가진 클래스 |
| Singleton 조회 | **61ns** | 이미 생성된 싱글턴 |
| Scoped 조회 | **21ns** | 기존 스코프 내에서 |
| 컨테이너 빌드 (100개 타입) | **0.05ms** | 등록당 약 0.5μs |
| **수동 `new()` 대비** | **1.56배** | 최고 수준의 Unity DI와 경쟁력 있는 성능 |

### ECS

| 연산 | 시간 | 비고 |
|------|------|------|
| 엔티티 생성 | **54ns** | 빈 엔티티 |
| 엔티티 + 3개 컴포넌트 | **374ns** | 전체 엔티티 설정 |
| 단일 컴포넌트 쿼리 | **6.6ns/엔티티** | 100k 엔티티 |
| 2개 컴포넌트 쿼리 | **18ns/엔티티** | 100k 엔티티 |
| 3개 컴포넌트 쿼리 | **28ns/엔티티** | 100k 엔티티 |
| GetComponent | **67ns** | 랜덤 액세스 |
| 시뮬레이션 (100k, 10 프레임) | **1.62ms/프레임** | Position += Velocity |
| **병렬 작업 속도 향상** | **17배** | 순차 ForEach 대비 |

### 메모리

| 지표 | 값 |
|------|-----|
| 엔티티당 메모리 (2개 컴포넌트) | 56 바이트 |
| GC 할당 (Singleton 리졸브) | 0 바이트 |
| GC 할당 (Scoped 리졸브) | 0 바이트 |

### 비교

| 프레임워크 | 리졸브 속도 | 수동 대비 |
|------------|------------|----------|
| **Strada** | 0.11-0.27μs | **1.56배** |
| VContainer | ~0.2-0.3μs | ~2배 |
| Reflex | ~0.5-1.0μs | ~3-5배 |
| Zenject | ~2-5μs | ~20-50배 |

---

## 문서

| 문서 | 설명 |
|------|------|
| [모듈](Documentation~/Modules.md) | 모듈러 아키텍처, ModuleConfig, 인스펙터 구성 가능한 시스템 |
| [DI 컨테이너](Documentation~/DI.md) | 의존성 주입, 수명 주기, 스코프 |
| [ECS 시스템](Documentation~/ECS.md) | 엔티티, 컴포넌트, 쿼리, 시스템 |
| [메시징](Documentation~/Messaging.md) | MessageBus, 커맨드, 이벤트, 쿼리 |
| [동기화](Documentation~/Sync.md) | 리액티브 프로퍼티, 바인딩, EntityMediator |
| [풀링](Documentation~/Pooling.md) | 오브젝트 풀, 라이프사이클 훅 |
| [상태 머신](Documentation~/StateMachine.md) | 전환이 포함된 FSM |
| [타이머 서비스](Documentation~/TimerService.md) | 일시정지/재개를 지원하는 관리형 타이머 |
| [디버깅](Documentation~/Debugging.md) | 문제 해결, 일반적인 이슈, 디버깅 도구 |
| [벤치마크](Documentation~/Benchmarks.md) | 전체 성능 데이터 |

---

## 아키텍처

### 시스템 개요

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          STRADA FRAMEWORK                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   ┌─────────────────────────┐     ┌─────────────────────────┐          │
│   │    PATTERNS LAYER       │     │      ECS LAYER          │          │
│   │                         │     │                         │          │
│   │  ┌─────────────────┐    │     │  ┌─────────────────┐    │          │
│   │  │     Views       │    │     │  │    Systems      │    │          │
│   │  │ (MonoBehaviour) │    │     │  │  (SystemBase)   │    │          │
│   │  └────────┬────────┘    │     │  └────────┬────────┘    │          │
│   │           │             │     │           │             │          │
│   │  ┌────────▼────────┐    │     │  ┌────────▼────────┐    │          │
│   │  │   Controllers   │    │     │  │    Entities     │    │          │
│   │  │  (Controller)   │    │     │  │  (EntityManager)│    │          │
│   │  └────────┬────────┘    │     │  └────────┬────────┘    │          │
│   │           │             │     │           │             │          │
│   │  ┌────────▼────────┐    │     │  ┌────────▼────────┐    │          │
│   │  │    Services     │    │     │  │   Components    │    │          │
│   │  │    (Service)    │    │     │  │  (IComponent)   │    │          │
│   │  └────────┬────────┘    │     │  └─────────────────┘    │          │
│   │           │             │     │                         │          │
│   │  ┌────────▼────────┐    │     │                         │          │
│   │  │     Models      │    │     │                         │          │
│   │  │    (Model)      │    │     │                         │          │
│   │  └─────────────────┘    │     │                         │          │
│   └────────────┬────────────┘     └────────────┬────────────┘          │
│                │                               │                        │
│                └───────────┬───────────────────┘                        │
│                            │                                            │
│                ┌───────────▼───────────┐                                │
│                │      MessageBus       │                                │
│                │  (Events/Commands/    │                                │
│                │       Queries)        │                                │
│                └───────────┬───────────┘                                │
│                            │                                            │
│    ┌───────────────────────┼───────────────────────┐                    │
│    │                       │                       │                    │
│    ▼                       ▼                       ▼                    │
│ ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐         │
│ │     DI       │   │   Reactive   │   │   Sync/Mediator      │         │
│ │  Container   │   │  Properties  │   │      Registry        │         │
│ │ (Container)  │   │(ReactiveProperty) │  (EntityMediator)    │         │
│ └──────────────┘   └──────────────┘   └──────────────────────┘         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 데이터 흐름

```
┌──────────────┐   Commands    ┌──────────────┐   Component     ┌──────────┐
│  Controller  │──────────────▶│  MessageBus  │───Updates──────▶│   ECS    │
│              │               │              │                 │  System  │
└──────────────┘               └──────────────┘                 └──────────┘
       ▲                              │                               │
       │                              │                               │
       │         ComponentChanged     │      Publish Events           │
       └──────────────────────────────┴───────────────────────────────┘
```

### 폴더 구조

```
Packages/com.strada.core/
├── Runtime/
│   ├── DI/                    # 의존성 주입
│   │   ├── ContainerBuilder.cs
│   │   ├── Container.cs
│   │   ├── ContainerScope.cs
│   │   ├── Lifetime.cs
│   │   ├── Attributes/        # 자동 바인딩 어트리뷰트
│   │   │   └── AutoRegisterAttribute.cs
│   │   └── AutoBinding/       # 런타임 스캐너
│   │       └── RuntimeAutoBindingScanner.cs
│   ├── ECS/                   # 엔티티 컴포넌트 시스템
│   │   ├── Core/EntityManager.cs
│   │   ├── Storage/SparseSet.cs
│   │   ├── Query/QueryBuilder.cs
│   │   ├── Systems/SystemBase.cs
│   │   └── Jobs/ParallelComponentJob.cs
│   ├── Communication/         # 통합 메시징
│   │   └── MessageBus.cs
│   ├── Commands/              # 커맨드 패턴
│   │   ├── ICommand.cs
│   │   ├── CommandPool.cs
│   │   └── CommandSequencer.cs
│   ├── Sync/                  # Patterns-ECS 통합
│   │   ├── ReactiveProperty.cs
│   │   ├── ComputedProperty.cs
│   │   ├── EntityMediator.cs
│   │   └── SyncEvents.cs
│   ├── Modules/               # 모듈러 아키텍처
│   │   ├── ModuleConfig.cs        # 기본 모듈 ScriptableObject
│   │   ├── IModuleBuilder.cs      # 플루언트 등록 API
│   │   ├── ModuleBuilder.cs       # 빌더 구현
│   │   ├── SystemRunner.cs        # 설정 기반 시스템 실행
│   │   ├── SystemEntry.cs         # 시스템 설정
│   │   ├── ServiceEntry.cs        # 서비스 설정
│   │   └── SystemAttributes.cs    # [StradaSystem] 어트리뷰트
│   ├── Bootstrap/             # 애플리케이션 부트스트랩
│   │   ├── GameBootstrapper.cs    # 메인 진입점
│   │   └── GameBootstrapperConfig.cs  # 중앙 오케스트레이터
│   ├── Pooling/               # 오브젝트 풀링
│   │   └── ObjectPool.cs
│   └── StateMachine/          # FSM
│       └── StateMachine.cs
├── Documentation~/            # 상세 문서
│   ├── Modules.md             # 모듈러 아키텍처 가이드
│   ├── DI.md                  # 의존성 주입 가이드
│   ├── ECS.md                 # 엔티티 컴포넌트 시스템 가이드
│   ├── Messaging.md           # MessageBus 메시징 가이드
│   ├── Sync.md                # 리액티브 바인딩 가이드
│   ├── Pooling.md             # 오브젝트 풀링 가이드
│   ├── StateMachine.md        # FSM 가이드
│   └── Benchmarks.md          # 성능 벤치마크
├── SourceGenerationDI~/       # DI Roslyn 소스 생성기
│   └── StradaDISourceGenerator.cs  # DI 자동 바인딩 생성
├── SourceGenerationECS~/      # ECS Roslyn 소스 생성기
│   ├── StradaFactoryGenerator.cs   # 팩토리 생성
│   └── EntityQueryGenerator.cs     # 쿼리 T9-T16 생성
├── Editor/                    # 에디터 도구
└── Tests/                     # 테스트 스위트
    ├── Runtime/               # 기능 테스트 (324개)
    └── Performance/           # 벤치마크 (93개)
```

---

## API 레퍼런스

### ContainerBuilder

```csharp
// 인터페이스 → 구현체 등록
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// 구체 타입 등록
builder.Register<MyService>(Lifetime.Transient);

// 팩토리 등록
builder.RegisterFactory<IService>(c => new ServiceImpl(c.Resolve<IDep>()));

// 인스턴스 등록
builder.RegisterInstance<IConfig>(configInstance);

// 컨테이너 빌드
IContainer container = builder.Build();
```

### IContainer

```csharp
T Resolve<T>() where T : class;
object Resolve(Type type);
bool TryResolve<T>(out T instance) where T : class;
bool IsRegistered<T>() where T : class;
IContainerScope CreateScope();
```

### EntityManager

```csharp
Entity CreateEntity();
void DestroyEntity(Entity entity);
bool Exists(Entity entity);

void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent;
void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent;
bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent;
T GetComponent<T>(Entity entity) where T : unmanaged, IComponent;
void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent;
```

### MessageBus

```csharp
// 이벤트 (발행/구독)
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;

// 구조체 커맨드 (요청/응답)
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;

// 오브젝트 커맨드 (풀링, 비동기)
void Execute(ICommand command);          // 풀링된 커맨드를 자동 반환합니다
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);

// 쿼리 (반환값이 있는 요청/응답)
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler);
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
```

### ReactiveProperty

```csharp
var prop = new ReactiveProperty<int>(initialValue);

prop.Value;                          // 현재 값 가져오기
prop.Value = newValue;               // 값 설정 및 알림
prop.SetWithoutNotify(value);        // 알림 없이 값 설정
prop.Subscribe(handler);             // 변경 사항 구독
prop.SubscribeAndInvoke(handler);    // 구독 및 즉시 호출
prop.Unsubscribe(handler);           // 구독 해제
```

### ObjectPool

```csharp
var pool = new ObjectPool<Enemy>(
    factory: () => new Enemy(),
    onSpawn: e => e.Reset(),
    onDespawn: e => e.Cleanup(),
    initialSize: 10,
    maxSize: 100
);

Enemy enemy = pool.Spawn();
pool.Despawn(enemy);
pool.Prewarm(20);
pool.Clear();
```

### StateMachine

```csharp
var fsm = new StateMachine<IState>();

fsm.AddState(new IdleState());
fsm.AddState(new WalkState());
fsm.AddState(new AttackState());

fsm.AddTransition<IdleState, WalkState>(() => input.IsMoving);
fsm.AddTransition<WalkState, IdleState>(() => !input.IsMoving);
fsm.AddAnyTransition<AttackState>(() => input.IsAttacking);

fsm.Start<IdleState>();
fsm.Update(deltaTime);
```

---

## 테스트

```bash
# 모든 테스트 실행 (Unity가 닫혀 있어야 합니다)
./run_tests.sh

# 기능 테스트만 실행
UNITY_PATH="/path/to/Unity" PROJECT_PATH="/path/to/project"
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "!Performance"

# 벤치마크만 실행
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "Performance"
```

**테스트 커버리지:**
- 330개 기능 테스트 (강화된 DI 및 ECS 엣지 케이스)
- 94개 성능 벤치마크 (현실적인 시뮬레이션 시나리오 추가)
- 총 424개 테스트 모두 통과

---

## 라이선스

독점 소프트웨어 - 모든 권리 보유

---

## 기여

이 프레임워크는 비공개 프로젝트입니다. 버그 보고 또는 기능 요청은 관리자에게 문의하세요.

---

*성능과 클린 아키텍처를 염두에 두고 Unity 6용으로 구축되었습니다.*
