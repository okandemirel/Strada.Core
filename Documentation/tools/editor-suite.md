# Editor Tools Suite

Strada comes with a powerful suite of Editor tools designed to improve Developer Experience (DX) and enforce architectural patterns.

## 1. Module Generator
**Menu:** `Strada > Create Module...`

The Module Generator is the primary tool for expanding your project. It enforces the "Modular Architecture" by generating the correct folder structure, assembly definitions, and boilerplate code automatically.

### Features
*   **Auto-scaffolding:** Creates `Scripts/`, `Tests/`, `Data/` folders.
*   **Assembly Definition:** Generates `.asmdef` with correct references to `Strada.Core`.
*   **Boilerplate:** Generates `IModuleInstaller`, `Systems`, `Components`, and `Services` based on your selection.
*   **Test Ready:** Optionally generates a separate `.Tests.asmdef` and NUnit test fixture.

### Usage
1.  Open the window via `Strada > Create Module...`.
2.  Enter a **Module Name** (e.g., `Inventory`).
3.  Select the components you need (e.g., if it's a pure ECS module, uncheck "Controller").
4.  Click **Generate**.

---

## 2. Entity Debugger
**Menu:** `Strada > Debugger > Entity Inspector`

A visual debugger for the Entity Component System. Essential for debugging gameplay logic.

### Features
*   **Live View:** See all active entities in the World.
*   **Component Inspection:** Click an entity to see its components and their runtime values.
*   **Filtering:** Search entities by ID.
*   **Auto-Refresh:** Updates in real-time during Play Mode.

---

## 3. Container Diagnostics
**Menu:** `Strada > Debugger > Container Diagnostics`

(Coming Soon) Visualizes the Dependency Injection graph, showing registered services, their lifetimes (Singleton/Scoped/Transient), and resolution counts.

---

## 4. Performance Monitor
**Menu:** `Strada > Debugger > Performance Monitor`

(Coming Soon) A dedicated profiler overlay that tracks:
*   ECS System execution time.
*   DI Resolution time per frame.
*   Bridge synchronization overhead.
