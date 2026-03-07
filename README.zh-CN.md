# Strada Framework

**高性能 Unity 框架，将 Patterns 架构与 ECS 仿真统一融合**

> **语言**: [English](README.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko.md)

[![Tests](https://img.shields.io/badge/tests-324%20passing-brightgreen)]()
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue)]()
[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-purple)]()

Strada 将企业级依赖注入与高性能 ECS 相结合，封装在简洁的模块化架构中。使用熟悉的模式构建 UI，同时利用 ECS 进行高性能仿真——无需在两种范式之间做出取舍。

---

## 目录

- [功能特性](#功能特性)
- [安装](#安装)
- [快速入门](#快速入门)
- [性能](#性能)
- [文档](#文档)
- [架构](#架构)
- [API 参考](#api-参考)
- [测试](#测试)
- [许可证](#许可证)

---

## 功能特性

### 依赖注入 ([文档](Documentation~/DI.md))
- **容器**：表达式树编译工厂（仅为手动 `new()` 的 1.56 倍开销）
- **生命周期**：支持 Singleton、Transient、Scoped，具备线程安全初始化
- **释放管理**：LIFO 释放顺序（依赖方先于被依赖方释放）
- **自动绑定**：基于特性的服务注册，支持 `[AutoRegister]`、`[AutoRegisterSingleton]` 等
- **循环检测**：构建时循环依赖检测，防止运行时错误
- **零分配解析**：Singleton/Scoped 路径无 GC 分配
- **线程安全**：采用 Volatile 读取、ConcurrentDictionary 及基于锁的释放安全机制

### 实体组件系统 ([文档](Documentation~/ECS.md))
- **稀疏集存储**：缓存友好的组件迭代（每实体 6-28ns）
- **查询系统**：`ForEach<T1...T16>()` — 最多 8 个手写实现，9-16 个源生成
- **安全性**：`EntityCommandBuffer` 确保迭代期间结构变更的安全
- **并行作业**：Burst 编译作业，相比顺序执行提速 17 倍
- **实体回收**：自动索引重用并带版本追踪
- **源生成**：编译时查询生成，支持 9-16 个组件

### 消息系统 ([文档](Documentation~/Messaging.md))
- **MessageBus**：统一的命令/查询/事件总线，基于数组索引分发（4ns/次分发）
- **池化命令**：执行 ICommand 对象并自动归还对象池
- **零分配发布**：基于结构体的消息，无装箱操作
- **异常隔离**：处理程序失败不会中断其他订阅者

### Patterns-ECS 同步 ([文档](Documentation~/Sync.md))
- **事件驱动集成**：ECS 系统发布 ComponentChanged 事件，Patterns 控制器订阅
- **EntityMediator**：将 ECS 实体绑定到 UI 视图，支持自动同步和 MessageBus 集成
- **双向数据流**：控制器通过 MessageBus 向 ECS 发送命令，并接收事件回调

### 响应式绑定 ([文档](Documentation~/Sync.md))
- **ReactiveProperty**：可观察值，支持变更通知
- **ReactiveCollection**：可观察列表，支持添加/移除/清空事件
- **ComputedProperty**：派生值，具备自动依赖追踪

### 模块化架构 ([文档](Documentation~/Modules.md))
- **ModuleConfig**：基于 ScriptableObject 的模块配置
- **Inspector 系统**：通过拖放方式配置 ECS 系统
- **IModuleBuilder**：类似 VContainer 的流式 API，用于 DI 注册
- **系统发现**：使用 `[StradaSystem]` 特性自动查找系统
- **优先级排序**：控制模块初始化顺序

### 实用工具
- **ObjectPool**：通用对象池，支持生命周期钩子（Spawn/Despawn）
- **StateMachine**：类型安全的有限状态机，支持条件转换
- **TimerService**：托管计时器，支持暂停/恢复

---

## 安装

将以下内容添加到 Unity 项目的 `Packages/manifest.json` 中：

```json
{
  "dependencies": {
    "com.strada.core": "file:../Packages/com.strada.core"
  }
}
```

或者直接将 `Packages/com.strada.core` 文件夹复制到你的项目中。

**系统要求：**
- Unity 6000.0+（Unity 6）
- .NET Standard 2.1

---

## 快速入门

### 依赖注入

```csharp
using Strada.Core.DI;
using Strada.Core.DI.Attributes;

// 方式 1：手动注册
var builder = new ContainerBuilder();
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
using var container = builder.Build();

// 方式 2：使用特性自动绑定
[AutoRegisterSingleton(As = typeof(IPlayerService))]
public class PlayerService : IPlayerService { }

[AutoRegisterTransient]
public class EnemyController { }

// 自动注册所有标记了特性的类型
var builder = new ContainerBuilder();
builder.RegisterAutoBindings();  // 扫描 [AutoRegister*] 特性
using var container = builder.Build();
```

### ECS 系统

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

// 定义组件（必须为非托管结构体）
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public int Current, Max; }
public struct Damage : IComponent { public int Value; }

// 查询最多 8 个组件（手写实现，最优性能）
entityManager.ForEach<Position, Velocity, Health, Damage>(
    (int entity, ref Position pos, ref Velocity vel, ref Health hp, ref Damage dmg) =>
    {
        pos.X += vel.X * deltaTime;
    });

// 查询 9-16 个组件（源生成）
entityManager.ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9>(...);

// 或使用 SystemBase 编写更简洁的代码
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

### 消息系统

```csharp
using Strada.Core.Communication;

// 将消息定义为结构体
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// 设置消息总线
var bus = new MessageBus();

// 订阅事件
bus.Subscribe<PlayerDamaged>(e => Debug.Log($"Player took {e.Damage} damage"));

// 发布事件（零分配）
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// 注册命令处理程序
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnEnemyAt(cmd.X, cmd.Y));
bus.Send(new SpawnEnemy { X = 10, Y = 20 });
```

### 响应式属性

```csharp
using Strada.Core.Sync;

// 创建响应式属性
var health = new ReactiveProperty<int>(100);

// 订阅变更
health.Subscribe(value => healthBar.SetValue(value));

// 值变更时自动通知订阅者
health.Value = 75; // healthBar 自动更新
```

---

## 性能

**真实基准测试**，在 Apple Silicon 上测量（Unity 6，Mono）：

### DI 容器

| 操作 | 耗时 | 备注 |
|------|------|------|
| 简单 Transient | **0.11μs** | 单个类，无依赖 |
| 4 层深度链 | **0.27μs** | A→B→C→D 依赖链 |
| 宽服务（5 个依赖） | **0.42μs** | 具有 5 个注入依赖的类 |
| Singleton 查找 | **61ns** | 已创建的单例 |
| Scoped 查找 | **21ns** | 在现有作用域内 |
| 容器构建（100 个类型） | **0.05ms** | 每次注册约 0.5μs |
| **对比手动 `new()`** | **1.56 倍** | 与最佳 Unity DI 方案持平 |

### ECS

| 操作 | 耗时 | 备注 |
|------|------|------|
| 实体创建 | **54ns** | 空实体 |
| 实体 + 3 个组件 | **374ns** | 完整实体创建 |
| 单组件查询 | **6.6ns/实体** | 10 万实体 |
| 双组件查询 | **18ns/实体** | 10 万实体 |
| 三组件查询 | **28ns/实体** | 10 万实体 |
| GetComponent | **67ns** | 随机访问 |
| 仿真（10 万实体，10 帧） | **1.62ms/帧** | Position += Velocity |
| **并行作业加速比** | **17 倍** | 对比顺序 ForEach |

### 内存

| 指标 | 值 |
|------|------|
| 每实体内存占用（2 个组件） | 56 字节 |
| GC 分配（Singleton 解析） | 0 字节 |
| GC 分配（Scoped 解析） | 0 字节 |

### 对比

| 框架 | 解析速度 | 对比手动 |
|------|----------|----------|
| **Strada** | 0.11-0.27μs | **1.56 倍** |
| VContainer | ~0.2-0.3μs | ~2 倍 |
| Reflex | ~0.5-1.0μs | ~3-5 倍 |
| Zenject | ~2-5μs | ~20-50 倍 |

---

## 文档

| 文档 | 描述 |
|------|------|
| [模块系统](Documentation~/Modules.md) | 模块化架构、ModuleConfig、Inspector 可配置系统 |
| [DI 容器](Documentation~/DI.md) | 依赖注入、生命周期、作用域 |
| [ECS 系统](Documentation~/ECS.md) | 实体、组件、查询、系统 |
| [消息系统](Documentation~/Messaging.md) | MessageBus、命令、事件、查询 |
| [同步](Documentation~/Sync.md) | 响应式属性、绑定、EntityMediator |
| [对象池](Documentation~/Pooling.md) | 对象池、生命周期钩子 |
| [状态机](Documentation~/StateMachine.md) | 有限状态机与转换 |
| [计时器服务](Documentation~/TimerService.md) | 托管计时器，支持暂停/恢复 |
| [调试](Documentation~/Debugging.md) | 故障排查、常见问题、调试工具 |
| [基准测试](Documentation~/Benchmarks.md) | 完整性能数据 |

---

## 架构

### 系统概览

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

### 数据流

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

### 文件夹结构

```
Packages/com.strada.core/
├── Runtime/
│   ├── DI/                    # 依赖注入
│   │   ├── ContainerBuilder.cs
│   │   ├── Container.cs
│   │   ├── ContainerScope.cs
│   │   ├── Lifetime.cs
│   │   ├── Attributes/        # 自动绑定特性
│   │   │   └── AutoRegisterAttribute.cs
│   │   └── AutoBinding/       # 运行时扫描器
│   │       └── RuntimeAutoBindingScanner.cs
│   ├── ECS/                   # 实体组件系统
│   │   ├── Core/EntityManager.cs
│   │   ├── Storage/SparseSet.cs
│   │   ├── Query/QueryBuilder.cs
│   │   ├── Systems/SystemBase.cs
│   │   └── Jobs/ParallelComponentJob.cs
│   ├── Communication/         # 统一消息系统
│   │   └── MessageBus.cs
│   ├── Commands/              # 命令模式
│   │   ├── ICommand.cs
│   │   ├── CommandPool.cs
│   │   └── CommandSequencer.cs
│   ├── Sync/                  # Patterns-ECS 集成
│   │   ├── ReactiveProperty.cs
│   │   ├── ComputedProperty.cs
│   │   ├── EntityMediator.cs
│   │   └── SyncEvents.cs
│   ├── Modules/               # 模块化架构
│   │   ├── ModuleConfig.cs        # 模块基础 ScriptableObject
│   │   ├── IModuleBuilder.cs      # 流式注册 API
│   │   ├── ModuleBuilder.cs       # Builder 实现
│   │   ├── SystemRunner.cs        # 配置驱动的系统执行
│   │   ├── SystemEntry.cs         # 系统配置
│   │   ├── ServiceEntry.cs        # 服务配置
│   │   └── SystemAttributes.cs    # [StradaSystem] 特性
│   ├── Bootstrap/             # 应用引导
│   │   ├── GameBootstrapper.cs    # 主入口点
│   │   └── GameBootstrapperConfig.cs  # 中央编排器
│   ├── Pooling/               # 对象池
│   │   └── ObjectPool.cs
│   └── StateMachine/          # 有限状态机
│       └── StateMachine.cs
├── Documentation~/            # 详细文档
│   ├── Modules.md             # 模块化架构指南
│   ├── DI.md                  # 依赖注入指南
│   ├── ECS.md                 # 实体组件系统指南
│   ├── Messaging.md           # MessageBus 消息系统指南
│   ├── Sync.md                # 响应式绑定指南
│   ├── Pooling.md             # 对象池指南
│   ├── StateMachine.md        # 有限状态机指南
│   └── Benchmarks.md          # 性能基准测试
├── SourceGenerationDI~/       # DI Roslyn 源生成器
│   └── StradaDISourceGenerator.cs  # DI 自动绑定生成
├── SourceGenerationECS~/      # ECS Roslyn 源生成器
│   ├── StradaFactoryGenerator.cs   # 工厂生成
│   └── EntityQueryGenerator.cs     # 查询 T9-T16 生成
├── Editor/                    # 编辑器工具
└── Tests/                     # 测试套件
    ├── Runtime/               # 功能测试（324 个）
    └── Performance/           # 基准测试（93 个）
```

---

## API 参考

### ContainerBuilder

```csharp
// 注册接口 → 实现
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// 注册具体类型
builder.Register<MyService>(Lifetime.Transient);

// 注册工厂
builder.RegisterFactory<IService>(c => new ServiceImpl(c.Resolve<IDep>()));

// 注册实例
builder.RegisterInstance<IConfig>(configInstance);

// 构建容器
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
// 事件（发布/订阅）
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;

// 结构体命令（请求/响应）
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;

// 对象命令（池化，异步）
void Execute(ICommand command);          // 自动归还池化命令
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);

// 查询（带返回值的请求/响应）
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler);
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
```

### ReactiveProperty

```csharp
var prop = new ReactiveProperty<int>(initialValue);

prop.Value;                          // 获取当前值
prop.Value = newValue;               // 设置值并通知
prop.SetWithoutNotify(value);        // 设置值但不通知
prop.Subscribe(handler);             // 订阅变更
prop.SubscribeAndInvoke(handler);    // 订阅并立即调用
prop.Unsubscribe(handler);           // 取消订阅
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

## 测试

```bash
# 运行所有测试（需关闭 Unity）
./run_tests.sh

# 仅运行功能测试
UNITY_PATH="/path/to/Unity" PROJECT_PATH="/path/to/project"
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "!Performance"

# 仅运行基准测试
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "Performance"
```

**测试覆盖：**
- 330 个功能测试（强化的 DI 与 ECS 边界情况）
- 94 个性能基准测试（新增真实仿真场景）
- 全部 424 个测试通过

---

## 许可证

专有软件 - 保留所有权利

---

## 贡献

本框架为私有项目。如需报告问题或提出功能需求，请联系维护者。

---

*专为 Unity 6 打造，兼顾性能与整洁架构。*
