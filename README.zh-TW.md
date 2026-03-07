# Strada Framework

**高效能 Unity 框架，統一 Patterns 架構與 ECS 模擬系統**

> **語言**: [English](README.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko.md)

[![Tests](https://img.shields.io/badge/tests-324%20passing-brightgreen)]()
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue)]()
[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-purple)]()

Strada 將企業級依賴注入與高效能 ECS 結合，以簡潔的模組化架構封裝。使用熟悉的模式建構 UI，同時利用 ECS 進行高效能模擬——無需在兩種範式之間做出取捨。

---

## 目錄

- [功能特色](#功能特色)
- [安裝方式](#安裝方式)
- [快速上手](#快速上手)
- [效能表現](#效能表現)
- [技術文件](#技術文件)
- [架構設計](#架構設計)
- [API 參考](#api-參考)
- [測試](#測試)
- [授權條款](#授權條款)

---

## 功能特色

### 依賴注入 ([文件](Documentation~/DI.md))
- **容器**：Expression Tree 編譯工廠（僅 1.56 倍手動 `new()` 開銷）
- **生命週期**：Singleton、Transient、Scoped，支援執行緒安全初始化
- **資源釋放**：LIFO 釋放順序（依賴者先於被依賴者釋放）
- **自動繫結**：透過 `[AutoRegister]`、`[AutoRegisterSingleton]` 等屬性實現基於特性的服務註冊
- **循環偵測**：建置階段偵測循環依賴，防止執行階段錯誤
- **零配置解析**：Singleton/Scoped 路徑無 GC 配置
- **執行緒安全**：採用 Volatile 讀取、ConcurrentDictionary 及鎖定機制確保釋放安全

### 實體元件系統 ([文件](Documentation~/ECS.md))
- **SparseSet 儲存**：快取友善的元件迭代（每實體 6-28ns）
- **查詢系統**：`ForEach<T1...T16>()` — 最多 8 個手寫實作，9-16 個透過 Source Generator 產生
- **安全性**：`EntityCommandBuffer` 確保迭代期間結構變更的安全性
- **平行作業**：Burst 編譯作業，相較循序執行提升 17 倍速度
- **實體回收**：自動索引重用與版本追蹤
- **原始碼產生**：編譯時期為 9-16 個元件產生查詢程式碼

### 訊息傳遞 ([文件](Documentation~/Messaging.md))
- **MessageBus**：統一的命令/查詢/事件匯流排，採用陣列索引分派（每次分派 4ns）
- **池化命令**：執行 ICommand 物件後自動歸還物件池
- **零配置發布**：基於結構的訊息，無裝箱操作
- **例外隔離**：處理器失敗不會中斷其他訂閱者

### Patterns-ECS 同步 ([文件](Documentation~/Sync.md))
- **事件驅動整合**：ECS 系統發布 ComponentChanged 事件，Patterns 控制器進行訂閱
- **EntityMediator**：將 ECS 實體繫結至 UI 視圖，支援自動同步與 MessageBus 整合
- **雙向資料流**：控制器透過 MessageBus 向 ECS 發送命令，並接收回傳事件

### 響應式繫結 ([文件](Documentation~/Sync.md))
- **ReactiveProperty**：可觀察值，支援變更通知
- **ReactiveCollection**：可觀察清單，支援新增/移除/清除事件
- **ComputedProperty**：衍生值，支援自動依賴追蹤

### 模組化架構 ([文件](Documentation~/Modules.md))
- **ModuleConfig**：基於 ScriptableObject 的模組配置
- **Inspector 系統**：透過拖放方式在 Inspector 中設定 ECS 系統
- **IModuleBuilder**：類似 VContainer 的流暢 API，用於 DI 註冊
- **系統探索**：透過 `[StradaSystem]` 屬性自動尋找系統
- **優先順序排序**：控制模組初始化順序

### 工具程式
- **ObjectPool**：泛型物件池，支援生命週期回呼（Spawn/Despawn）
- **StateMachine**：型別安全的有限狀態機，支援條件式轉換
- **TimerService**：受管理的計時器，支援暫停/恢復功能

---

## 安裝方式

將以下內容加入 Unity 專案的 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.strada.core": "file:../Packages/com.strada.core"
  }
}
```

或直接將 `Packages/com.strada.core` 資料夾複製到您的專案中。

**系統需求：**
- Unity 6000.0+（Unity 6）
- .NET Standard 2.1

---

## 快速上手

### 依賴注入

```csharp
using Strada.Core.DI;
using Strada.Core.DI.Attributes;

// 方式一：手動註冊
var builder = new ContainerBuilder();
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
using var container = builder.Build();

// 方式二：透過屬性自動繫結
[AutoRegisterSingleton(As = typeof(IPlayerService))]
public class PlayerService : IPlayerService { }

[AutoRegisterTransient]
public class EnemyController { }

// 自動註冊所有標記屬性的型別
var builder = new ContainerBuilder();
builder.RegisterAutoBindings();  // 掃描 [AutoRegister*] 屬性
using var container = builder.Build();
```

### ECS 系統

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

// 定義元件（必須為 unmanaged 結構）
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public int Current, Max; }
public struct Damage : IComponent { public int Value; }

// 查詢最多 8 個元件（手寫實作，最佳效能）
entityManager.ForEach<Position, Velocity, Health, Damage>(
    (int entity, ref Position pos, ref Velocity vel, ref Health hp, ref Damage dmg) =>
    {
        pos.X += vel.X * deltaTime;
    });

// 查詢 9-16 個元件（Source Generator 產生）
entityManager.ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9>(...);

// 或使用 SystemBase 撰寫更簡潔的程式碼
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

### 訊息傳遞

```csharp
using Strada.Core.Communication;

// 將訊息定義為結構
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// 設定匯流排
var bus = new MessageBus();

// 訂閱事件
bus.Subscribe<PlayerDamaged>(e => Debug.Log($"Player took {e.Damage} damage"));

// 發布事件（零配置）
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// 註冊命令處理器
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnEnemyAt(cmd.X, cmd.Y));
bus.Send(new SpawnEnemy { X = 10, Y = 20 });
```

### 響應式屬性

```csharp
using Strada.Core.Sync;

// 建立響應式屬性
var health = new ReactiveProperty<int>(100);

// 訂閱變更
health.Subscribe(value => healthBar.SetValue(value));

// 變更時自動通知訂閱者
health.Value = 75; // healthBar 自動更新
```

---

## 效能表現

**誠實的基準測試**，於 Apple Silicon（Unity 6, Mono）上測量：

### DI 容器

| 操作 | 時間 | 備註 |
|------|------|------|
| 簡單 Transient | **0.11μs** | 單一類別，無依賴 |
| 4 層深度鏈結 | **0.27μs** | A→B→C→D 依賴鏈 |
| 寬依賴服務（5 個依賴） | **0.42μs** | 注入 5 個依賴的類別 |
| Singleton 查詢 | **61ns** | 已建立的 Singleton |
| Scoped 查詢 | **21ns** | 於現有 Scope 內 |
| 容器建置（100 個型別） | **0.05ms** | 每次註冊約 0.5μs |
| **相較手動 `new()`** | **1.56 倍** | 與最佳 Unity DI 框架相當 |

### ECS

| 操作 | 時間 | 備註 |
|------|------|------|
| 實體建立 | **54ns** | 空白實體 |
| 實體 + 3 個元件 | **374ns** | 完整實體設定 |
| 單元件查詢 | **6.6ns/實體** | 100k 實體 |
| 雙元件查詢 | **18ns/實體** | 100k 實體 |
| 三元件查詢 | **28ns/實體** | 100k 實體 |
| GetComponent | **67ns** | 隨機存取 |
| 模擬（100k，10 幀） | **1.62ms/幀** | Position += Velocity |
| **平行作業加速比** | **17 倍** | 相較循序 ForEach |

### 記憶體

| 指標 | 數值 |
|------|------|
| 每實體記憶體用量（2 個元件） | 56 位元組 |
| GC 配置（Singleton 解析） | 0 位元組 |
| GC 配置（Scoped 解析） | 0 位元組 |

### 框架比較

| 框架 | 解析速度 | 相較手動 |
|------|----------|----------|
| **Strada** | 0.11-0.27μs | **1.56 倍** |
| VContainer | ~0.2-0.3μs | ~2 倍 |
| Reflex | ~0.5-1.0μs | ~3-5 倍 |
| Zenject | ~2-5μs | ~20-50 倍 |

---

## 技術文件

| 文件 | 說明 |
|------|------|
| [模組](Documentation~/Modules.md) | 模組化架構、ModuleConfig、Inspector 可配置系統 |
| [DI 容器](Documentation~/DI.md) | 依賴注入、生命週期、作用域 |
| [ECS 系統](Documentation~/ECS.md) | 實體、元件、查詢、系統 |
| [訊息傳遞](Documentation~/Messaging.md) | MessageBus、命令、事件、查詢 |
| [同步](Documentation~/Sync.md) | 響應式屬性、繫結、EntityMediator |
| [物件池](Documentation~/Pooling.md) | 物件池、生命週期回呼 |
| [狀態機](Documentation~/StateMachine.md) | 有限狀態機與狀態轉換 |
| [計時器服務](Documentation~/TimerService.md) | 受管理的計時器，支援暫停/恢復 |
| [除錯](Documentation~/Debugging.md) | 疑難排解、常見問題、除錯工具 |
| [基準測試](Documentation~/Benchmarks.md) | 完整效能數據 |

---

## 架構設計

### 系統概覽

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

### 資料流

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

### 資料夾結構

```
Packages/com.strada.core/
├── Runtime/
│   ├── DI/                    # 依賴注入
│   │   ├── ContainerBuilder.cs
│   │   ├── Container.cs
│   │   ├── ContainerScope.cs
│   │   ├── Lifetime.cs
│   │   ├── Attributes/        # 自動繫結屬性
│   │   │   └── AutoRegisterAttribute.cs
│   │   └── AutoBinding/       # 執行階段掃描器
│   │       └── RuntimeAutoBindingScanner.cs
│   ├── ECS/                   # 實體元件系統
│   │   ├── Core/EntityManager.cs
│   │   ├── Storage/SparseSet.cs
│   │   ├── Query/QueryBuilder.cs
│   │   ├── Systems/SystemBase.cs
│   │   └── Jobs/ParallelComponentJob.cs
│   ├── Communication/         # 統一訊息傳遞
│   │   └── MessageBus.cs
│   ├── Commands/              # 命令模式
│   │   ├── ICommand.cs
│   │   ├── CommandPool.cs
│   │   └── CommandSequencer.cs
│   ├── Sync/                  # Patterns-ECS 整合
│   │   ├── ReactiveProperty.cs
│   │   ├── ComputedProperty.cs
│   │   ├── EntityMediator.cs
│   │   └── SyncEvents.cs
│   ├── Modules/               # 模組化架構
│   │   ├── ModuleConfig.cs        # 基礎模組 ScriptableObject
│   │   ├── IModuleBuilder.cs      # 流暢註冊 API
│   │   ├── ModuleBuilder.cs       # Builder 實作
│   │   ├── SystemRunner.cs        # 配置驅動的系統執行
│   │   ├── SystemEntry.cs         # 系統配置
│   │   ├── ServiceEntry.cs        # 服務配置
│   │   └── SystemAttributes.cs    # [StradaSystem] 屬性
│   ├── Bootstrap/             # 應用程式啟動
│   │   ├── GameBootstrapper.cs    # 主要進入點
│   │   └── GameBootstrapperConfig.cs  # 中央協調器
│   ├── Pooling/               # 物件池
│   │   └── ObjectPool.cs
│   └── StateMachine/          # 有限狀態機
│       └── StateMachine.cs
├── Documentation~/            # 詳細文件
│   ├── Modules.md             # 模組化架構指南
│   ├── DI.md                  # 依賴注入指南
│   ├── ECS.md                 # 實體元件系統指南
│   ├── Messaging.md           # MessageBus 訊息傳遞指南
│   ├── Sync.md                # 響應式繫結指南
│   ├── Pooling.md             # 物件池指南
│   ├── StateMachine.md        # 有限狀態機指南
│   └── Benchmarks.md          # 效能基準測試
├── SourceGenerationDI~/       # DI Roslyn Source Generator
│   └── StradaDISourceGenerator.cs  # DI 自動繫結產生
├── SourceGenerationECS~/      # ECS Roslyn Source Generator
│   ├── StradaFactoryGenerator.cs   # 工廠產生
│   └── EntityQueryGenerator.cs     # 查詢 T9-T16 產生
├── Editor/                    # 編輯器工具
└── Tests/                     # 測試套件
    ├── Runtime/               # 功能測試 (324)
    └── Performance/           # 基準測試 (93)
```

---

## API 參考

### ContainerBuilder

```csharp
// 註冊介面 → 實作
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// 註冊具體型別
builder.Register<MyService>(Lifetime.Transient);

// 註冊工廠
builder.RegisterFactory<IService>(c => new ServiceImpl(c.Resolve<IDep>()));

// 註冊實例
builder.RegisterInstance<IConfig>(configInstance);

// 建置容器
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
// 事件（發布/訂閱）
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;

// 結構命令（請求/回應）
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;

// 物件命令（池化、非同步）
void Execute(ICommand command);          // 自動歸還池化命令
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);

// 查詢（帶回傳值的請求/回應）
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler);
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
```

### ReactiveProperty

```csharp
var prop = new ReactiveProperty<int>(initialValue);

prop.Value;                          // 取得目前值
prop.Value = newValue;               // 設定並通知
prop.SetWithoutNotify(value);        // 設定但不通知
prop.Subscribe(handler);             // 訂閱變更
prop.SubscribeAndInvoke(handler);    // 訂閱並立即呼叫
prop.Unsubscribe(handler);           // 取消訂閱
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

## 測試

```bash
# 執行所有測試（需先關閉 Unity）
./run_tests.sh

# 僅執行功能測試
UNITY_PATH="/path/to/Unity" PROJECT_PATH="/path/to/project"
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "!Performance"

# 僅執行基準測試
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "Performance"
```

**測試涵蓋範圍：**
- 330 項功能測試（強化 DI 與 ECS 邊界案例）
- 94 項效能基準測試（新增真實模擬情境）
- 全部 424 項測試通過

---

## 授權條款

專有授權 - 保留所有權利

---

## 貢獻

本框架為私有專案。如需回報錯誤或提出功能需求，請聯繫維護者。

---

*專為 Unity 6 打造，兼顧效能與簡潔架構。*
