# Strada Framework

**高性能なUnityフレームワーク — PatternsアーキテクチャとECSシミュレーションを統合**

> **言語**: [English](README.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko.md)

[![Tests](https://img.shields.io/badge/tests-324%20passing-brightgreen)]()
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue)]()
[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-purple)]()

Stradaは、エンタープライズグレードの依存性注入とパフォーマンス重視のECSを組み合わせ、クリーンなモジュラーアーキテクチャで包括したフレームワークです。UIの構築には馴染みのあるパターンを使用しつつ、高性能シミュレーションにはECSを活用できます。パラダイムの選択に悩む必要はありません。

---

## 目次

- [機能](#機能)
- [インストール](#インストール)
- [クイックスタート](#クイックスタート)
- [パフォーマンス](#パフォーマンス)
- [ドキュメント](#ドキュメント)
- [アーキテクチャ](#アーキテクチャ)
- [APIリファレンス](#apiリファレンス)
- [テスト](#テスト)
- [ライセンス](#ライセンス)

---

## 機能

### 依存性注入 ([ドキュメント](Documentation~/DI.md))
- **コンテナ**: 式ツリーによるコンパイル済みファクトリ（手動 `new()` の1.56倍のオーバーヘッド）
- **ライフタイム**: シングルトン、トランジェント、スコープ付き（スレッドセーフな初期化）
- **破棄**: LIFO順序での破棄（依存先より先に依存元を破棄）
- **自動バインディング**: `[AutoRegister]`、`[AutoRegisterSingleton]` 等の属性ベースのサービス登録
- **循環参照検出**: ビルド時の循環検出によりランタイムエラーを防止
- **ゼロアロケーション解決**: シングルトン/スコープパスでGCアロケーションなし
- **スレッドセーフ**: Volatile読み取り、ConcurrentDictionary、ロックベースの安全な破棄

### エンティティコンポーネントシステム ([ドキュメント](Documentation~/ECS.md))
- **SparseSetストレージ**: キャッシュフレンドリーなコンポーネントイテレーション（エンティティあたり6〜28ns）
- **クエリシステム**: `ForEach<T1...T16>()` — 最大8つは手書き、9〜16はソース生成
- **安全性**: `EntityCommandBuffer` によるイテレーション中の安全な構造変更
- **並列ジョブ**: Burstコンパイル済みジョブで逐次処理の17倍高速化
- **エンティティリサイクル**: バージョントラッキングによるインデックスの自動再利用
- **ソース生成**: 9〜16コンポーネント用のコンパイル時クエリ生成

### メッセージング ([ドキュメント](Documentation~/Messaging.md))
- **MessageBus**: 配列インデックスディスパッチによる統一コマンド/クエリ/イベントバス（4ns/ディスパッチ）
- **プールドコマンド**: ICommandオブジェクトの実行と自動プール返却
- **ゼロアロケーション配信**: 構造体ベースのメッセージ、ボクシングなし
- **例外分離**: ハンドラの失敗が他のサブスクライバに影響しない

### Patterns-ECS同期 ([ドキュメント](Documentation~/Sync.md))
- **イベント駆動統合**: ECSシステムがComponentChangedイベントを発行し、Patternsコントローラがサブスクライブ
- **EntityMediator**: ECSエンティティとUIビューをバインドし、自動同期とMessageBus統合を実現
- **双方向フロー**: コントローラがMessageBus経由でECSにコマンドを送信し、イベントを受信

### リアクティブバインディング ([ドキュメント](Documentation~/Sync.md))
- **ReactiveProperty**: 変更通知付きのオブザーバブルな値
- **ReactiveCollection**: 追加/削除/クリアイベント付きのオブザーバブルなリスト
- **ComputedProperty**: 自動依存関係トラッキングによる派生値

### モジュラーアーキテクチャ ([ドキュメント](Documentation~/Modules.md))
- **ModuleConfig**: ScriptableObjectベースのモジュール設定
- **インスペクタシステム**: ドラッグ＆ドロップでECSシステムを設定
- **IModuleBuilder**: VContainerライクな流暢なDI登録API
- **システムディスカバリ**: `[StradaSystem]` 属性によるシステムの自動検出
- **優先度順序**: モジュール初期化順序の制御

### ユーティリティ
- **ObjectPool**: ライフサイクルフック付きの汎用プーリング（Spawn/Despawn）
- **StateMachine**: 条件付きトランジション対応の型安全なFSM
- **TimerService**: 一時停止/再開対応のマネージドタイマー

---

## インストール

Unityプロジェクトの `Packages/manifest.json` に以下を追加します：

```json
{
  "dependencies": {
    "com.strada.core": "file:../Packages/com.strada.core"
  }
}
```

または、`Packages/com.strada.core` フォルダを直接プロジェクトにコピーしてください。

**要件：**
- Unity 6000.0+（Unity 6）
- .NET Standard 2.1

---

## クイックスタート

### 依存性注入

```csharp
using Strada.Core.DI;
using Strada.Core.DI.Attributes;

// 方法1: 手動登録
var builder = new ContainerBuilder();
builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
builder.Register<IInputService, InputService>(Lifetime.Singleton);
using var container = builder.Build();

// 方法2: 属性による自動バインディング
[AutoRegisterSingleton(As = typeof(IPlayerService))]
public class PlayerService : IPlayerService { }

[AutoRegisterTransient]
public class EnemyController { }

// 属性が付与されたすべての型を自動登録
var builder = new ContainerBuilder();
builder.RegisterAutoBindings();  // [AutoRegister*] 属性をスキャン
using var container = builder.Build();
```

### ECSシステム

```csharp
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

// コンポーネントの定義（アンマネージド構造体である必要あり）
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public int Current, Max; }
public struct Damage : IComponent { public int Value; }

// 最大8コンポーネントのクエリ（手書き、最適パフォーマンス）
entityManager.ForEach<Position, Velocity, Health, Damage>(
    (int entity, ref Position pos, ref Velocity vel, ref Health hp, ref Damage dmg) =>
    {
        pos.X += vel.X * deltaTime;
    });

// 9〜16コンポーネントのクエリ（ソース生成）
entityManager.ForEach<T1, T2, T3, T4, T5, T6, T7, T8, T9>(...);

// またはSystemBaseを使用してコードを簡潔に
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

### メッセージング

```csharp
using Strada.Core.Communication;

// メッセージを構造体として定義
public struct PlayerDamaged { public int EntityId; public int Damage; }
public struct SpawnEnemy { public float X, Y; }

// バスのセットアップ
var bus = new MessageBus();

// イベントをサブスクライブ
bus.Subscribe<PlayerDamaged>(e => Debug.Log($"Player took {e.Damage} damage"));

// イベントを配信（ゼロアロケーション）
bus.Publish(new PlayerDamaged { EntityId = 1, Damage = 10 });

// コマンドハンドラを登録
bus.RegisterCommandHandler<SpawnEnemy>(cmd => SpawnEnemyAt(cmd.X, cmd.Y));
bus.Send(new SpawnEnemy { X = 10, Y = 20 });
```

### リアクティブプロパティ

```csharp
using Strada.Core.Sync;

// リアクティブプロパティの作成
var health = new ReactiveProperty<int>(100);

// 変更をサブスクライブ
health.Subscribe(value => healthBar.SetValue(value));

// 値の変更でサブスクライバに自動通知
health.Value = 75; // healthBarが自動的に更新される
```

---

## パフォーマンス

**正直なベンチマーク** — Apple Silicon（Unity 6、Mono）で計測：

### DIコンテナ

| 操作 | 時間 | 備考 |
|------|------|------|
| 単純なトランジェント | **0.11μs** | 単一クラス、依存関係なし |
| 4階層の依存チェーン | **0.27μs** | A→B→C→Dの依存チェーン |
| 広範なサービス（5依存） | **0.42μs** | 5つの注入された依存関係を持つクラス |
| シングルトンルックアップ | **61ns** | 作成済みシングルトン |
| スコープドルックアップ | **21ns** | 既存スコープ内 |
| コンテナビルド（100型） | **0.05ms** | 登録あたり約0.5μs |
| **手動 `new()` との比較** | **1.56倍** | Unity向けDIの中でトップクラス |

### ECS

| 操作 | 時間 | 備考 |
|------|------|------|
| エンティティ生成 | **54ns** | 空のエンティティ |
| エンティティ + 3コンポーネント | **374ns** | フルエンティティセットアップ |
| 単一コンポーネントクエリ | **6.6ns/エンティティ** | 10万エンティティ |
| 2コンポーネントクエリ | **18ns/エンティティ** | 10万エンティティ |
| 3コンポーネントクエリ | **28ns/エンティティ** | 10万エンティティ |
| GetComponent | **67ns** | ランダムアクセス |
| シミュレーション（10万、10フレーム） | **1.62ms/フレーム** | Position += Velocity |
| **並列ジョブ高速化** | **17倍** | 逐次ForEachとの比較 |

### メモリ

| 指標 | 値 |
|------|-----|
| エンティティあたりのメモリ（2コンポーネント） | 56バイト |
| GCアロケーション（シングルトン解決） | 0バイト |
| GCアロケーション（スコープド解決） | 0バイト |

### 比較

| フレームワーク | 解決速度 | 手動比 |
|--------------|---------|--------|
| **Strada** | 0.11-0.27μs | **1.56倍** |
| VContainer | 約0.2-0.3μs | 約2倍 |
| Reflex | 約0.5-1.0μs | 約3-5倍 |
| Zenject | 約2-5μs | 約20-50倍 |

---

## ドキュメント

| ドキュメント | 説明 |
|------------|------|
| [Modules](Documentation~/Modules.md) | モジュラーアーキテクチャ、ModuleConfig、インスペクタ設定可能なシステム |
| [DI Container](Documentation~/DI.md) | 依存性注入、ライフタイム、スコープ |
| [ECS System](Documentation~/ECS.md) | エンティティ、コンポーネント、クエリ、システム |
| [Messaging](Documentation~/Messaging.md) | MessageBus、コマンド、イベント、クエリ |
| [Sync](Documentation~/Sync.md) | リアクティブプロパティ、バインディング、EntityMediator |
| [Pooling](Documentation~/Pooling.md) | オブジェクトプール、ライフサイクルフック |
| [StateMachine](Documentation~/StateMachine.md) | トランジション付きFSM |
| [TimerService](Documentation~/TimerService.md) | 一時停止/再開対応のマネージドタイマー |
| [Debugging](Documentation~/Debugging.md) | トラブルシューティング、よくある問題、デバッグツール |
| [Benchmarks](Documentation~/Benchmarks.md) | 詳細パフォーマンスデータ |

---

## アーキテクチャ

### システム概要

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

### データフロー

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

### フォルダ構成

```
Packages/com.strada.core/
├── Runtime/
│   ├── DI/                    # 依存性注入
│   │   ├── ContainerBuilder.cs
│   │   ├── Container.cs
│   │   ├── ContainerScope.cs
│   │   ├── Lifetime.cs
│   │   ├── Attributes/        # 自動バインディング属性
│   │   │   └── AutoRegisterAttribute.cs
│   │   └── AutoBinding/       # ランタイムスキャナ
│   │       └── RuntimeAutoBindingScanner.cs
│   ├── ECS/                   # エンティティコンポーネントシステム
│   │   ├── Core/EntityManager.cs
│   │   ├── Storage/SparseSet.cs
│   │   ├── Query/QueryBuilder.cs
│   │   ├── Systems/SystemBase.cs
│   │   └── Jobs/ParallelComponentJob.cs
│   ├── Communication/         # 統一メッセージング
│   │   └── MessageBus.cs
│   ├── Commands/              # コマンドパターン
│   │   ├── ICommand.cs
│   │   ├── CommandPool.cs
│   │   └── CommandSequencer.cs
│   ├── Sync/                  # Patterns-ECS統合
│   │   ├── ReactiveProperty.cs
│   │   ├── ComputedProperty.cs
│   │   ├── EntityMediator.cs
│   │   └── SyncEvents.cs
│   ├── Modules/               # モジュラーアーキテクチャ
│   │   ├── ModuleConfig.cs        # ベースモジュールScriptableObject
│   │   ├── IModuleBuilder.cs      # 流暢な登録API
│   │   ├── ModuleBuilder.cs       # ビルダー実装
│   │   ├── SystemRunner.cs        # 設定駆動のシステム実行
│   │   ├── SystemEntry.cs         # システム設定
│   │   ├── ServiceEntry.cs        # サービス設定
│   │   └── SystemAttributes.cs    # [StradaSystem] 属性
│   ├── Bootstrap/             # アプリケーションブートストラップ
│   │   ├── GameBootstrapper.cs    # メインエントリポイント
│   │   └── GameBootstrapperConfig.cs  # 中央オーケストレータ
│   ├── Pooling/               # オブジェクトプーリング
│   │   └── ObjectPool.cs
│   └── StateMachine/          # FSM
│       └── StateMachine.cs
├── Documentation~/            # 詳細ドキュメント
│   ├── Modules.md             # モジュラーアーキテクチャガイド
│   ├── DI.md                  # 依存性注入ガイド
│   ├── ECS.md                 # エンティティコンポーネントシステムガイド
│   ├── Messaging.md           # MessageBusメッセージングガイド
│   ├── Sync.md                # リアクティブバインディングガイド
│   ├── Pooling.md             # オブジェクトプーリングガイド
│   ├── StateMachine.md        # FSMガイド
│   └── Benchmarks.md          # パフォーマンスベンチマーク
├── SourceGenerationDI~/       # DI Roslynソースジェネレータ
│   └── StradaDISourceGenerator.cs  # DI自動バインディング生成
├── SourceGenerationECS~/      # ECS Roslynソースジェネレータ
│   ├── StradaFactoryGenerator.cs   # ファクトリ生成
│   └── EntityQueryGenerator.cs     # クエリT9-T16生成
├── Editor/                    # エディタツール
└── Tests/                     # テストスイート
    ├── Runtime/               # 機能テスト（324）
    └── Performance/           # ベンチマーク（93）
```

---

## APIリファレンス

### ContainerBuilder

```csharp
// インターフェース → 実装の登録
builder.Register<IService, ServiceImpl>(Lifetime.Singleton);

// 具象型の登録
builder.Register<MyService>(Lifetime.Transient);

// ファクトリの登録
builder.RegisterFactory<IService>(c => new ServiceImpl(c.Resolve<IDep>()));

// インスタンスの登録
builder.RegisterInstance<IConfig>(configInstance);

// コンテナのビルド
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
// イベント（パブリッシュ/サブスクライブ）
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
void Publish<TEvent>(TEvent evt) where TEvent : struct;

// 構造体コマンド（リクエスト/レスポンス）
void RegisterCommandHandler<TCommand>(Action<TCommand> handler) where TCommand : struct;
void Send<TCommand>(TCommand command) where TCommand : struct;

// オブジェクトコマンド（プールド、非同期）
void Execute(ICommand command);          // プールドコマンドを自動返却
void ExecuteAsync(IAsyncCommand command, Action onComplete = null);

// クエリ（戻り値付きリクエスト/レスポンス）
void RegisterQueryHandler<TQuery, TResult>(Func<TQuery, TResult> handler);
TResult Query<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
```

### ReactiveProperty

```csharp
var prop = new ReactiveProperty<int>(initialValue);

prop.Value;                          // 現在の値を取得
prop.Value = newValue;               // 値を設定して通知
prop.SetWithoutNotify(value);        // 通知なしで設定
prop.Subscribe(handler);             // 変更をサブスクライブ
prop.SubscribeAndInvoke(handler);    // サブスクライブして即座に呼び出し
prop.Unsubscribe(handler);           // サブスクリプションを解除
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

## テスト

```bash
# 全テストを実行（Unityを閉じた状態で）
./run_tests.sh

# 機能テストのみ実行
UNITY_PATH="/path/to/Unity" PROJECT_PATH="/path/to/project"
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "!Performance"

# ベンチマークのみ実行
"$UNITY_PATH" -batchmode -projectPath "$PROJECT_PATH" \
  -runTests -testPlatform playmode \
  -testCategory "Performance"
```

**テストカバレッジ：**
- 330の機能テスト（強化されたDIおよびECSのエッジケース）
- 94のパフォーマンスベンチマーク（現実的なシミュレーションシナリオを追加）
- 全424テスト合格

---

## ライセンス

プロプライエタリ — 全著作権所有

---

## コントリビューション

本フレームワークはプライベートです。バグ報告や機能リクエストについては、メンテナーにお問い合わせください。

---

*パフォーマンスとクリーンアーキテクチャを念頭に、Unity 6向けに構築されました。*
