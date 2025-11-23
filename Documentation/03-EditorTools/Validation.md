# Validation System

Comprehensive asset, module, and build-time validation.

---

## Overview

Strada's validation system ensures code quality and catches errors before they reach builds:

- **Asset Validation** - ScriptableObject configs, naming conventions
- **Module Validation** - Structure, assembly definitions, installers
- **Build Validation** - Pre-build checks with optional blocking
- **Runtime Health Checks** - Performance monitoring during play mode

---

## Quick Start

### Validate All Assets

`Tools > Strada > Validate All Assets`

Runs comprehensive validation:
- All CD_* ScriptableObjects
- All module structures
- Assembly definitions
- Naming conventions

Results appear in a filterable report window.

### Validate Modules Only

`Tools > Strada > Validate Modules`

Checks only module structure:
- Folder organization
- Assembly definitions
- Module installers
- File naming

---

## Asset Validation

### ScriptableObject Validation

Automatically validates CD_* configs:

#### Naming Conventions

```csharp
// ✅ Good
CD_PlayerConfig.cs
CD_PlayerConfig.asset

// ❌ Bad
PlayerConfig.cs (missing CD_ prefix)
Config_Player.asset (wrong format)
```

#### Config Property

```csharp
[CreateAssetMenu(fileName = "CD_Player", menuName = "Strada/Configs/Player")]
public class CD_Player : ScriptableObject
{
    // ✅ Must have public Config field
    public PlayerConfig Config = new PlayerConfig();
}
```

#### IsValid() Method

```csharp
[Serializable]
public class PlayerConfig
{
    public float MaxHealth = 100f;
    public float Speed = 5f;

    // ✅ Implement validation logic
    public bool IsValid()
    {
        if (MaxHealth <= 0)
            return false;

        if (Speed <= 0)
            return false;

        return true;
    }
}
```

#### File Location

```csharp
// ✅ Good locations
Assets/Modules/Player/Data/UnityObjects/CD_Player.asset
Assets/Configs/CD_Player.asset

// ⚠️ Warning
Assets/CD_Player.asset (not in Data or Configs folder)
```

### Custom Validators

Create your own validators by extending `AssetValidator`:

```csharp
using Strada.Core.Editor.Validation;
using UnityEngine;

public class CustomConfigValidator : AssetValidator
{
    public override string ValidatorName => "Custom Config Validator";
    public override string Category => "Configuration";

    public override bool CanValidate(Object asset)
    {
        return asset is CD_MyConfig;
    }

    public override ValidationResult Validate(Object asset)
    {
        var result = new ValidationResult();
        var config = asset as CD_MyConfig;
        var path = AssetDatabase.GetAssetPath(asset);

        // Add your validation logic
        if (config.Config.SomeValue < 0)
        {
            result.AddError(
                "SomeValue cannot be negative",
                path,
                Category,
                "Set SomeValue to a positive number"
            );
        }

        return result;
    }
}
```

Register your validator:

```csharp
// In ValidationReportWindow.InitializeValidators()
_validators.Add(new CustomConfigValidator());
```

---

## Module Validation

### Module Structure

Expected folder structure:

```
Assets/Modules/MyModule/
├── Scripts/
│   ├── Data/
│   │   ├── ValueObjects/
│   │   └── UnityObjects/
│   ├── Models/
│   ├── Views/
│   ├── Controllers/
│   └── Services/
├── MyModuleInstaller.cs
└── Strada.Modules.MyModule.asmdef
```

Validation checks:
- ✅ `/Scripts` folder exists
- ✅ `/Data` subfolders present
- ⚠️ Recommended folders (Models, Views, etc.)

### Module Naming

```csharp
// ✅ Good
MyModule/            (PascalCase, no spaces)
PlayerSystem/
GameLogic/

// ❌ Bad
my module/           (lowercase, spaces)
player_system/       (underscores)
123Module/           (starts with number)

// ℹ️ Special
_Template/           (underscore prefix = template)
```

### Assembly Definitions

#### Naming Convention

```
Strada.Modules.MyModule.asmdef
```

Checks:
- ✅ Follows `Strada.Modules.{ModuleName}` pattern
- ✅ References `Strada.Core`
- ✅ Root namespace matches module name

#### Content Validation

```json
{
    "name": "Strada.Modules.MyModule",
    "rootNamespace": "Strada.Modules.MyModule",
    "references": [
        "Strada.Core"  // ✅ Required
    ]
}
```

### Module Installer

#### Naming Convention

```
MyModuleInstaller.cs
```

Pattern: `{ModuleName}ModuleInstaller.cs`

#### Required Implementation

```csharp
using Strada.Core.DI;
using Strada.Core.Modules;

public class MyModuleInstaller : IModuleInstaller
{
    // ✅ All three methods required

    public void Install(IContainerBuilder builder)
    {
        // Register dependencies
    }

    public void Initialize(IContainer container)
    {
        // Initialize after all modules loaded
    }

    public void Shutdown()
    {
        // Cleanup
    }
}
```

---

## Build Validation

### Enable Build Validation

`Tools > Strada > Build Validation > Enable Validation on Build`

Runs validation before every build.

### Build Blocking

`Tools > Strada > Build Validation > Enable Build Blocking on Errors`

Prevents builds if validation errors are found.

### Build Validation Flow

```
1. User triggers build
2. BuildValidator.OnPreprocessBuild() runs
3. Validate all modules
4. Validate all configs
5. If errors found:
   - Log detailed errors
   - Block build (if enabled)
   - Show fix suggestions
6. If only warnings:
   - Log warnings
   - Continue build
7. If all valid:
   - Log success
   - Continue build
```

### Console Output

```
[Strada] Running pre-build validation...

[Module Naming] Module name 'my module' contains spaces
Asset: Assets/Modules/my module/
Suggestion: Remove spaces from the module name

[Configuration] CD_Player.Config is null
Asset: Assets/Configs/CD_Player.asset
Suggestion: Initialize the Config field with a valid instance

[Strada] Validation complete: 2 errors, 0 warnings, 0 info
```

### Disable Build Validation

`Tools > Strada > Build Validation > Disable Validation on Build`

Skips validation during builds.

---

## Runtime Health Checks

### Open Health Check Window

`Window > Strada > Runtime Health Check`

### Health Checks

#### Frame Rate

```
Target: 60 FPS
Warning: < 50 FPS
Critical: < 30 FPS
```

**Status Colors**:
- 🟢 Healthy: ≥ 50 FPS
- 🟡 Warning: 30-50 FPS
- 🔴 Unhealthy: < 30 FPS

#### Memory Usage

```
Warning: > 256 MB
Critical: > 512 MB
```

Monitors:
- Total allocated memory
- Total reserved memory

#### GC Allocations

```
Warning: > 128 MB managed heap
```

Large managed heap can cause GC stutters.

#### DI Container

Checks:
- Container is initialized
- Container is operational

#### ECS Worlds

Checks:
- Active ECS worlds count
- World creation status

### Auto-Refresh

Health checks update automatically every 1 second during play mode.

---

## Validation Report Window

### Features

#### Filtering

- **All** - Show all messages
- **Errors** - Show only errors
- **Warnings** - Show only warnings
- **Info** - Show only informational messages

#### Search

Search messages and asset paths:

```
Search: "PlayerConfig"
```

Finds all messages containing "PlayerConfig".

#### Category Grouping

Messages are grouped by category:

- Module Structure
- Module Naming
- Configuration
- Assembly Definitions
- Module Installation

#### Clickable Asset Paths

Click any asset path to:
1. Ping the asset in Project window
2. Select the asset
3. Open in Inspector

#### Fix Suggestions

Every error includes an actionable fix suggestion:

```
Error: Module name 'player' should start with uppercase
Suggestion: Rename the module to start with an uppercase letter
```

---

## Validation Results API

### ValidationResult

```csharp
public class ValidationResult
{
    public List<ValidationMessage> Messages { get; }

    public int ErrorCount { get; }
    public int WarningCount { get; }
    public int InfoCount { get; }

    public bool IsValid { get; }      // No errors
    public bool HasWarnings { get; }  // Has warnings

    public void AddError(string message, string assetPath, string category, string fixSuggestion);
    public void AddWarning(string message, string assetPath, string category, string fixSuggestion);
    public void AddInfo(string message, string assetPath, string category);

    public void Merge(ValidationResult other);
    public void Clear();
}
```

### ValidationMessage

```csharp
public class ValidationMessage
{
    public Severity Severity { get; }  // Error, Warning, Info
    public string Message { get; }
    public string AssetPath { get; }
    public string Category { get; }
    public string FixSuggestion { get; }
}
```

---

## Custom Validation

### Create Validator

```csharp
using Strada.Core.Editor.Validation;
using UnityEngine;

public class WeaponConfigValidator : AssetValidator
{
    public override string ValidatorName => "Weapon Config Validator";
    public override string Category => "Gameplay";

    public override bool CanValidate(Object asset)
    {
        return asset is CD_Weapon;
    }

    public override ValidationResult Validate(Object asset)
    {
        var result = new ValidationResult();
        var weapon = asset as CD_Weapon;
        var path = AssetDatabase.GetAssetPath(asset);

        // Validate damage
        ValidateRange(
            result,
            weapon.Config.Damage,
            1f, 100f,
            "Damage",
            path
        );

        // Validate fire rate
        if (weapon.Config.FireRate <= 0)
        {
            result.AddError(
                "Fire rate must be positive",
                path,
                Category,
                "Set fire rate to a value greater than 0"
            );
        }

        // Check balance
        var dps = weapon.Config.Damage * weapon.Config.FireRate;
        if (dps > 50f)
        {
            result.AddWarning(
                $"DPS ({dps:F1}) is very high, may be unbalanced",
                path,
                Category,
                "Consider reducing damage or fire rate"
            );
        }

        return result;
    }
}
```

### Helper Methods

Available in `AssetValidator` base class:

```csharp
// Null check
ValidateNotNull(result, obj, "FieldName", assetPath);

// Empty string check
ValidateNotEmpty(result, value, "FieldName", assetPath);

// Range validation (hard)
ValidateRange(result, value, min, max, "FieldName", assetPath);

// Range validation (soft warning)
ValidateRecommendedRange(result, value, min, max, "FieldName", assetPath);
```

---

## Module Validator API

### Validate Single Module

```csharp
using Strada.Core.Editor.Validation;

var result = ModuleValidator.ValidateModule("Assets/Modules/MyModule");

if (!result.IsValid)
{
    foreach (var message in result.Messages)
    {
        Debug.LogError(message.Message);
    }
}
```

### Validate All Modules

```csharp
var result = ModuleValidator.ValidateAllModules();

Debug.Log($"Validated modules with {result.ErrorCount} errors");
```

---

## Best Practices

### ✅ Do

**Implement IsValid()**
```csharp
[Serializable]
public class MyConfig
{
    public bool IsValid()
    {
        // Add validation logic
        return true;
    }
}
```

**Use OnValidate()**
```csharp
public class CD_MyConfig : ScriptableObject
{
    private void OnValidate()
    {
        if (!Config.IsValid())
        {
            Debug.LogWarning($"Invalid config in {name}");
        }
    }
}
```

**Provide Fix Suggestions**
```csharp
result.AddError(
    "Damage is zero",
    path,
    "Gameplay",
    "Set Damage to a positive value (recommended: 10-50)"
);
```

**Run Validation Before Commits**
```
Tools > Strada > Validate All Assets
```

### ❌ Don't

**Skip Validation**
```csharp
// ❌ No IsValid() method
public class MyConfig
{
    public float Value;
}
```

**Ignore Warnings**
```
// ⚠️ Warnings often indicate real issues
```

**Disable Build Validation Without Reason**
```
// Only disable if you understand the risks
```

---

## Troubleshooting

### Validation Takes Too Long

**Problem**: Validation scans all assets.

**Solution**: Validate specific modules:
```
Tools > Strada > Validate Modules
```

### False Positives

**Problem**: Validator flags valid configs.

**Solution**: Customize validator logic or suppress specific checks.

### Build Blocked

**Problem**: Build validation blocks builds.

**Solution**: Fix errors or temporarily disable:
```
Tools > Strada > Build Validation > Disable Build Blocking
```

---

## Integration with CI/CD

### Command Line Validation

```csharp
[MenuItem("Strada/Validate for CI")]
public static void ValidateForCI()
{
    var result = ModuleValidator.ValidateAllModules();

    if (!result.IsValid)
    {
        Debug.LogError($"Validation failed: {result.ErrorCount} errors");
        EditorApplication.Exit(1);
    }

    Debug.Log("Validation passed!");
    EditorApplication.Exit(0);
}
```

### Pre-Commit Hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

unity -batchmode -quit -executeMethod Strada.Validation.ValidateForCI

if [ $? -ne 0 ]; then
    echo "Validation failed! Fix errors before committing."
    exit 1
fi
```

---

## Menu Reference

### Tools Menu

- `Tools > Strada > Validate All Assets` - Full validation
- `Tools > Strada > Validate Modules` - Module validation only

### Build Validation Menu

- `Tools > Strada > Build Validation > Enable Validation on Build`
- `Tools > Strada > Build Validation > Disable Validation on Build`
- `Tools > Strada > Build Validation > Enable Build Blocking on Errors`
- `Tools > Strada > Build Validation > Disable Build Blocking on Errors`

### Window Menu

- `Window > Strada > Runtime Health Check` - Performance monitoring

---

**Previous**: [Code Generation](CodeGeneration.md) | **Next**: [API Reference](../04-APIReference/DIContainer.md)
