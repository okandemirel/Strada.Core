# Quick Start Guide

Get up and running with Strada in 5 minutes!

---

## What You'll Build

A simple module that demonstrates:
- Dependency Injection
- MVCS architecture
- ScriptableObject configuration
- Editor tools integration

---

## Step 1: Create Bootstrap (1 minute)

Create a new C# script called `GameBootstrap.cs`:

```csharp
using Strada.Core;
using Strada.Core.Modules;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private BootstrapConfig _config;

    private StradaApplication _app;

    private void Awake()
    {
        _app = new StradaApplication();

        // Register your modules here
        _app.RegisterModule<GreetingModule>();

        _app.Initialize();
    }

    private void OnDestroy()
    {
        _app?.Shutdown();
    }
}
```

Attach this script to a GameObject in your scene.

---

## Step 2: Create Configuration (1 minute)

Use the wizard to create a config:

1. Right-click in Project window
2. Select `Assets > Create > Strada > New ScriptableObject Config`
3. Name it "Greeting"
4. Add a String field
5. Click "Generate Config"

This creates:
- `CD_Greeting.cs` - ScriptableObject
- `GreetingConfig.cs` - Value object

Create an instance:
1. Right-click in Project
2. `Create > Strada > Configs > Greeting`
3. Set the string to "Hello, Strada!"

---

## Step 3: Create Module (2 minutes)

Use the module wizard:

1. Right-click in Project
2. `Assets > Create > Strada > New Module`
3. Enter "Greeting" as module name
4. Select "Service" component
5. Click through steps
6. Click "Generate Module"

This creates a complete module structure at `Assets/Modules/Greeting/`

---

## Step 4: Implement Service (1 minute)

Edit `Assets/Modules/Greeting/Scripts/Services/IGreetingService.cs`:

```csharp
namespace Strada.Modules.Greeting
{
    public interface IGreetingService
    {
        void ShowGreeting();
    }
}
```

Create implementation `GreetingService.cs`:

```csharp
using UnityEngine;

namespace Strada.Modules.Greeting
{
    public class GreetingService : IGreetingService
    {
        private readonly CD_Greeting _config;

        public GreetingService(CD_Greeting config)
        {
            _config = config;
        }

        public void ShowGreeting()
        {
            Debug.Log(_config.Config.StringValue);
        }
    }
}
```

---

## Step 5: Register in Module Installer

Edit `GreetingModuleInstaller.cs`:

```csharp
using Strada.Core.DI;
using Strada.Core.Modules;
using UnityEngine;

namespace Strada.Modules.Greeting
{
    public class GreetingModuleInstaller : IModuleInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // Load config
            var config = Resources.Load<CD_Greeting>("CD_Greeting");

            // Register as singleton
            builder.RegisterInstance(config);

            // Register service
            builder.Register<IGreetingService, GreetingService>()
                   .WithLifetime(Lifetime.Singleton);
        }

        public void Initialize(IContainer container)
        {
            // Get and call service
            var service = container.Resolve<IGreetingService>();
            service.ShowGreeting();
        }

        public void Shutdown()
        {
            Debug.Log("[Greeting] Shutting down...");
        }
    }
}
```

---

## Step 6: Test It!

1. Press Play in Unity
2. You should see "Hello, Strada!" in the console
3. Open `Window > Strada > DI Container Inspector` to see your registrations
4. Open `Window > Strada > Module Graph` to see your module

---

## 🎉 Success!

You've just:
✅ Created a Strada module
✅ Used dependency injection
✅ Loaded ScriptableObject configuration
✅ Explored editor tools

---

## Next Steps

### Learn More
- **[Core Concepts](CoreConcepts.md)** - Understand the architecture
- **[First Module Tutorial](FirstModule.md)** - Deeper dive into modules
- **[Dependency Injection](../02-Architecture/DependencyInjection.md)** - Master the DI container

### Add Features
- Add a Controller to handle user input
- Add a View to display UI
- Add an Event to communicate between modules
- Add ECS for performance-critical logic

### Explore Tools
- **Module Graph**: `Window > Strada > Module Graph`
- **Validation**: `Tools > Strada > Validate All Assets`
- **Diagnostics**: `Window > Strada > DI Container Inspector`

---

## Common Issues

### "Module not found"
Make sure you registered the module in `GameBootstrap`:
```csharp
_app.RegisterModule<GreetingModule>();
```

### "Config is null"
Ensure the config is in a Resources folder or loaded via AssetDatabase.

### "Service not resolved"
Check that the service is registered in the module installer's `Install()` method.

---

**Next**: [Core Concepts](CoreConcepts.md) →
