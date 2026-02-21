using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Strada.Core.Editor.Utilities
{
    /// <summary>
    /// Provides keyboard shortcuts for quickly opening Strada editor windows.
    /// All shortcuts use Alt+F-key combinations to avoid conflicts with Unity defaults.
    /// </summary>
    public static class StradaShortcutManager
    {
        [Shortcut("Strada/Open Dashboard", KeyCode.F1, ShortcutModifiers.Alt)]
        static void OpenDashboard() => Windows.StradaDashboardWindow.ShowWindow();

        [Shortcut("Strada/Open Entity Inspector", KeyCode.F2, ShortcutModifiers.Alt)]
        static void OpenEntityInspector() => Windows.StradaEntityInspectorWindow.ShowWindow();

        [Shortcut("Strada/Open Bus Debugger", KeyCode.F3, ShortcutModifiers.Alt)]
        static void OpenBusDebugger() => Windows.BusDebuggerWindow.ShowWindow();

        [Shortcut("Strada/Open System Profiler", KeyCode.F4, ShortcutModifiers.Alt)]
        static void OpenSystemProfiler() => Windows.SystemProfilerWindow.ShowWindow();

        [Shortcut("Strada/Open Time Machine", KeyCode.F5, ShortcutModifiers.Alt)]
        static void OpenTimeMachine() => Windows.TimeMachineWindow.ShowWindow();

        [Shortcut("Strada/Open Dependency Graph", KeyCode.F6, ShortcutModifiers.Alt)]
        static void OpenDependencyGraph() => Graph.DependencyGraphWindow.ShowWindow();

        [Shortcut("Strada/Open Module Graph", KeyCode.F7, ShortcutModifiers.Alt)]
        static void OpenModuleGraph() => Graph.ModuleGraphWindow.ShowWindow();

        [Shortcut("Strada/Open Config Manager", KeyCode.F8, ShortcutModifiers.Alt)]
        static void OpenConfigManager() => Windows.StradaConfigDataManagerWindow.ShowWindow();

        [Shortcut("Strada/Open Benchmark Runner", KeyCode.F9, ShortcutModifiers.Alt)]
        static void OpenBenchmarkRunner() => Windows.BenchmarkRunnerWindow.ShowWindow();
    }
}
