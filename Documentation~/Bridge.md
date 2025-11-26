# Reactive Bridge

Strada's Bridge system provides reactive data binding for connecting UI, models, and ECS data.

## Table of Contents

- [Quick Start](#quick-start)
- [ReactiveProperty](#reactiveproperty)
- [ReactiveCollection](#reactivecollection)
- [ComputedProperty](#computedproperty)
- [BindingScope](#bindingscope)
- [Integration Patterns](#integration-patterns)
- [Best Practices](#best-practices)

---

## Quick Start

```csharp
using Strada.Core.Bridge;

// Create reactive property
var health = new ReactiveProperty<int>(100);

// Subscribe to changes
health.Subscribe(value => healthBar.SetFillAmount(value / 100f));

// Changes automatically notify subscribers
health.Value = 75; // UI updates automatically

// Subscribe and get current value immediately
health.SubscribeAndInvoke(value => Debug.Log($"Health: {value}"));
```

---

## ReactiveProperty

Observable value that notifies subscribers on change.

### Basic Usage

```csharp
// Create with initial value
var score = new ReactiveProperty<int>(0);
var playerName = new ReactiveProperty<string>("Player1");
var isAlive = new ReactiveProperty<bool>(true);

// Read value
int currentScore = score.Value;

// Write value (notifies subscribers if changed)
score.Value = 100;

// Write without notification
score.SetWithoutNotify(200);
```

### Subscribing

```csharp
// Simple subscription
score.Subscribe(value => UpdateScoreUI(value));

// Subscribe and invoke immediately with current value
score.SubscribeAndInvoke(value =>
{
    // Called immediately with current value
    // Then called on every change
    scoreText.text = value.ToString();
});

// Store reference for unsubscription
Action<int> handler = OnScoreChanged;
score.Subscribe(handler);

// Later...
score.Unsubscribe(handler);
```

### Change Detection

ReactiveProperty only notifies when value actually changes:

```csharp
var health = new ReactiveProperty<int>(100);
int notifyCount = 0;
health.Subscribe(_ => notifyCount++);

health.Value = 100; // No notification (same value)
health.Value = 99;  // Notifies (changed)
health.Value = 99;  // No notification (same value)

Assert.AreEqual(1, notifyCount);
```

### Manual Notification

```csharp
// Force notification even without change
health.SetWithoutNotify(50);
health.Notify(); // Manually trigger notification
```

### Implicit Conversion

```csharp
var health = new ReactiveProperty<int>(100);

// Implicit conversion to value type
int current = health; // Same as health.Value
```

### Disposal

```csharp
var prop = new ReactiveProperty<int>(0);

// Clear all subscribers
prop.Clear();

// Or dispose (also clears)
prop.Dispose();
```

---

## ReactiveCollection

Observable list with add/remove/clear notifications.

### Basic Usage

```csharp
var inventory = new ReactiveCollection<Item>();

// Subscribe to events
inventory.OnAdd(item => Debug.Log($"Added: {item.Name}"));
inventory.OnRemove(item => Debug.Log($"Removed: {item.Name}"));
inventory.OnClear(() => Debug.Log("Inventory cleared"));

// Modify collection
inventory.Add(sword);    // Triggers OnAdd
inventory.Remove(sword); // Triggers OnRemove
inventory.Clear();       // Triggers OnClear
```

### Collection Operations

```csharp
var items = new ReactiveCollection<string>();

// Add items
items.Add("Sword");
items.Add("Shield");

// Access by index
string first = items[0];
int count = items.Count;

// Remove operations
items.Remove("Sword");    // By value
items.RemoveAt(0);        // By index

// Clear all
items.Clear();
```

### UI Binding Example

```csharp
public class InventoryUI : MonoBehaviour
{
    public Transform itemContainer;
    public GameObject itemPrefab;

    private ReactiveCollection<Item> _inventory;
    private Dictionary<Item, GameObject> _itemViews = new();

    void Start()
    {
        _inventory = GameModel.Instance.Inventory;

        _inventory.OnAdd(item =>
        {
            var view = Instantiate(itemPrefab, itemContainer);
            view.GetComponent<ItemView>().Setup(item);
            _itemViews[item] = view;
        });

        _inventory.OnRemove(item =>
        {
            if (_itemViews.TryGetValue(item, out var view))
            {
                Destroy(view);
                _itemViews.Remove(item);
            }
        });

        _inventory.OnClear(() =>
        {
            foreach (var view in _itemViews.Values)
                Destroy(view);
            _itemViews.Clear();
        });
    }
}
```

---

## ComputedProperty

Derived value that automatically updates when dependencies change.

### Basic Usage

```csharp
var health = new ReactiveProperty<int>(100);
var maxHealth = new ReactiveProperty<int>(100);

// Computed property derived from two sources
var healthPercent = new ComputedProperty<float>(
    () => (float)health.Value / maxHealth.Value,
    health, maxHealth // Dependencies
);

// Subscribe to computed value
healthPercent.Subscribe(pct => healthBar.fillAmount = pct);

// When either dependency changes, computed updates
health.Value = 50;     // healthPercent becomes 0.5
maxHealth.Value = 200; // healthPercent becomes 0.25
```

### Complex Computations

```csharp
var baseAttack = new ReactiveProperty<int>(10);
var weaponBonus = new ReactiveProperty<int>(5);
var buffMultiplier = new ReactiveProperty<float>(1.0f);

var totalDamage = new ComputedProperty<int>(
    () => (int)((baseAttack.Value + weaponBonus.Value) * buffMultiplier.Value),
    baseAttack, weaponBonus, buffMultiplier
);

// totalDamage auto-updates when any input changes
```

### Chained Computations

```csharp
var a = new ReactiveProperty<int>(1);
var b = new ReactiveProperty<int>(2);

var sum = new ComputedProperty<int>(() => a.Value + b.Value, a, b);
var doubled = new ComputedProperty<int>(() => sum.Value * 2, sum);

// doubled updates when a or b changes
a.Value = 5; // sum=7, doubled=14
```

---

## BindingScope

Manages subscriptions with automatic cleanup.

### Basic Usage

```csharp
public class PlayerUI : MonoBehaviour
{
    private BindingScope _scope;

    void Start()
    {
        _scope = new BindingScope();

        var model = GameModel.Instance;

        // All subscriptions tracked by scope
        _scope.Bind(model.Health, v => healthBar.value = v);
        _scope.Bind(model.Score, v => scoreText.text = v.ToString());
        _scope.Bind(model.Level, v => levelText.text = $"Level {v}");
    }

    void OnDestroy()
    {
        // Automatically unsubscribes all
        _scope.Dispose();
    }
}
```

### One-Way Binding

```csharp
// Property → UI
_scope.Bind(health, value => healthText.text = value.ToString());

// Property → Multiple targets
_scope.Bind(score, value =>
{
    scoreText.text = value.ToString();
    highScoreText.text = value > highScore ? "NEW HIGH!" : "";
});
```

### Two-Way Binding

```csharp
// For input fields
_scope.BindTwoWay(
    playerName,
    () => inputField.text,
    value => inputField.text = value,
    inputField.onValueChanged
);
```

---

## Integration Patterns

### Model-View Binding

```csharp
// Model
public class PlayerModel
{
    public ReactiveProperty<int> Health { get; } = new(100);
    public ReactiveProperty<int> MaxHealth { get; } = new(100);
    public ReactiveProperty<int> Gold { get; } = new(0);

    public ComputedProperty<float> HealthPercent { get; }

    public PlayerModel()
    {
        HealthPercent = new ComputedProperty<float>(
            () => (float)Health.Value / MaxHealth.Value,
            Health, MaxHealth
        );
    }
}

// View
public class PlayerView : MonoBehaviour
{
    public Slider healthBar;
    public Text goldText;

    private BindingScope _scope;

    public void Bind(PlayerModel model)
    {
        _scope = new BindingScope();
        _scope.Bind(model.HealthPercent, v => healthBar.value = v);
        _scope.Bind(model.Gold, v => goldText.text = $"Gold: {v}");
    }

    void OnDestroy() => _scope?.Dispose();
}
```

### ECS ↔ Reactive Bridge

```csharp
// Sync ECS component to reactive property
public class HealthSyncSystem : SystemBase
{
    private ReactiveProperty<int> _playerHealth;

    public HealthSyncSystem(ReactiveProperty<int> playerHealth)
    {
        _playerHealth = playerHealth;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ForEach<Health, PlayerTag>((int e, ref Health h, ref PlayerTag _) =>
        {
            // Update reactive property from ECS
            if (_playerHealth.Value != h.Current)
                _playerHealth.Value = h.Current;
        });
    }
}
```

### Event-Driven Updates

```csharp
// Update reactive properties from bus events
bus.Subscribe<PlayerDamaged>(e =>
{
    model.Health.Value -= e.Damage;
});

bus.Subscribe<GoldCollected>(e =>
{
    model.Gold.Value += e.Amount;
});
```

---

## Best Practices

### 1. Use BindingScope for Cleanup

```csharp
// Good - automatic cleanup
private BindingScope _scope;

void OnEnable()
{
    _scope = new BindingScope();
    _scope.Bind(health, OnHealthChanged);
}

void OnDisable()
{
    _scope.Dispose();
}

// Avoid - manual management
void OnEnable()
{
    health.Subscribe(_handler); // Easy to forget cleanup
}
```

### 2. Keep Computed Logic Simple

```csharp
// Good - simple computation
var total = new ComputedProperty<int>(() => a.Value + b.Value, a, b);

// Avoid - complex side effects
var bad = new ComputedProperty<int>(() =>
{
    SendAnalytics(); // Don't do side effects
    return a.Value + b.Value;
}, a, b);
```

### 3. Dispose When Done

```csharp
public class GameUI : MonoBehaviour
{
    private List<IDisposable> _disposables = new();

    void Start()
    {
        var prop = new ReactiveProperty<int>(0);
        _disposables.Add(prop);
    }

    void OnDestroy()
    {
        foreach (var d in _disposables)
            d.Dispose();
    }
}
```

### 4. Use SetWithoutNotify for Batch Updates

```csharp
// Avoid multiple notifications
model.Health.SetWithoutNotify(newHealth);
model.MaxHealth.SetWithoutNotify(newMaxHealth);
model.Shield.SetWithoutNotify(newShield);

// Single notification at end
model.Health.Notify();
```

---

## API Reference

### ReactiveProperty<T>

```csharp
ReactiveProperty()
ReactiveProperty(T initialValue)

T Value { get; set; }
void SetWithoutNotify(T value)
void Subscribe(Action<T> handler)
void SubscribeAndInvoke(Action<T> handler)
void Unsubscribe(Action<T> handler)
void Notify()
void Clear()
void Dispose()

static implicit operator T(ReactiveProperty<T> property)
```

### ReactiveCollection<T>

```csharp
int Count { get; }
T this[int index] { get; }

void Add(T item)
bool Remove(T item)
void RemoveAt(int index)
void Clear()

void OnAdd(Action<T> handler)
void OnRemove(Action<T> handler)
void OnClear(Action handler)

void Dispose()
```

### ComputedProperty<T>

```csharp
ComputedProperty(Func<T> compute, params IReadOnlyReactiveProperty<object>[] dependencies)

T Value { get; }
void Subscribe(Action<T> handler)
void Unsubscribe(Action<T> handler)
void Dispose()
```

### BindingScope

```csharp
void Bind<T>(IReadOnlyReactiveProperty<T> source, Action<T> target)
void BindTwoWay<T>(ReactiveProperty<T> property, Func<T> getter, Action<T> setter, UnityEvent<T> changeEvent)
void Dispose()
```

---

## Related Documentation

- [DI Container](DI.md) - Dependency injection
- [ECS System](ECS.md) - Entity Component System
- [Messaging](Messaging.md) - Event system
