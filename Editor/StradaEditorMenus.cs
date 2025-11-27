using UnityEditor;
using UnityEngine;
using Strada.Core.Editor.Benchmarking;
using Strada.Core.Editor.Graph;
using Strada.Core.Editor.HotReload;
using Strada.Core.Editor.Windows;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Central menu organization for all Strada editor tools.
    /// Provides quick access to diagnostics, tools, and documentation.
    /// Organized by category: Dashboard, Debugger, Tools, Diagnostics.
    /// </summary>
    public static class StradaEditorMenus
    {
        private const string MenuRoot = "Strada/";
        
        // Priority ranges:
        // 0-49: Dashboard and main windows
        // 50-99: Tools (Module Generator, Config Manager, etc.)
        // 100-149: Debugger windows
        // 150-199: Diagnostics and validation
        // 200-299: Documentation
        // 300-399: Settings and configuration
        // 1000+: About and help

        #region Dashboard (Priority 0-49)

        [MenuItem(MenuRoot + "Dashboard %#&d", priority = 0)]
        private static void OpenDashboard()
        {
            StradaDashboardWindow.ShowWindow();
        }

        #endregion

        #region Tools (Priority 50-99)

        [MenuItem(MenuRoot + "Tools/Create Module... %#n", priority = 50)]
        private static void OpenModuleGenerator()
        {
            StradaModuleGeneratorWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Tools/Config Data Manager %#c", priority = 51)]
        private static void OpenConfigDataManager()
        {
            StradaConfigDataManagerWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Tools/Validate All Configs", priority = 60)]
        private static void ValidateAllConfigs()
        {
            var configs = AssetDatabase.FindAssets("t:ScriptableObject");
            int validatedCount = 0;
            int errorCount = 0;

            foreach (var guid in configs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && asset.name.StartsWith("CD_"))
                {
                    var validateMethod = asset.GetType().GetMethod("Validate");
                    if (validateMethod != null)
                    {
                        try
                        {
                            var result = validateMethod.Invoke(asset, null);
                            if (result is bool boolResult && !boolResult)
                            {
                                errorCount++;
                                Debug.LogWarning($"[Strada] Validation failed for: {asset.name}");
                            }
                            EditorUtility.SetDirty(asset);
                            validatedCount++;
                        }
                        catch (System.Exception ex)
                        {
                            errorCount++;
                            Debug.LogError($"[Strada] Validation error for {asset.name}: {ex.Message}");
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            
            if (errorCount > 0)
            {
                Debug.LogWarning($"[Strada] Validated {validatedCount} configs with {errorCount} error(s)");
            }
            else
            {
                Debug.Log($"[Strada] Successfully validated {validatedCount} config assets");
            }
        }

        [MenuItem(MenuRoot + "Tools/Clean Generated Code", priority = 61)]
        private static void CleanGeneratedCode()
        {
            var generatedPath = "Assets/Strada.Generated";
            if (System.IO.Directory.Exists(generatedPath))
            {
                if (EditorUtility.DisplayDialog("Clean Generated Code",
                    $"This will delete all files in:\n{generatedPath}\n\nAre you sure?",
                    "Delete", "Cancel"))
                {
                    System.IO.Directory.Delete(generatedPath, true);
                    AssetDatabase.Refresh();
                    Debug.Log("[Strada] Generated code cleaned");
                }
            }
            else
            {
                Debug.Log("[Strada] No generated code folder found");
            }
        }

        #endregion

        #region Debugger (Priority 100-149)

        [MenuItem(MenuRoot + "Debugger/Dependency Graph %#d", priority = 100)]
        private static void OpenDependencyGraph()
        {
            DependencyGraphWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/Module Graph %#m", priority = 101)]
        private static void OpenModuleGraph()
        {
            ModuleGraphWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/Entity Inspector %#e", priority = 110)]
        private static void OpenEntityInspector()
        {
            StradaEntityInspectorWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/System Profiler %#p", priority = 120)]
        private static void OpenSystemProfiler()
        {
            SystemProfilerWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/Bus Debugger %#b", priority = 121)]
        private static void OpenBusDebugger()
        {
            BusDebuggerWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/Benchmark Runner", priority = 130)]
        private static void OpenBenchmarkRunner()
        {
            BenchmarkRunnerWindow.ShowWindow();
        }

        #endregion

        #region Diagnostics (Priority 150-199)

        [MenuItem(MenuRoot + "Diagnostics/Validate Architecture", priority = 160)]
        private static void ValidateArchitecture()
        {
            Debug.Log("[Strada] Architecture validation started...");
            
            // Run architecture validation on all scripts
            var scripts = AssetDatabase.FindAssets("t:MonoScript");
            int issueCount = 0;
            
            foreach (var guid in scripts)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Strada") || path.Contains("Modules"))
                {
                    // Basic validation - check for common issues
                    var content = System.IO.File.ReadAllText(path);
                    
                    // Check for Controller accessing ECS directly
                    if (path.Contains("Controller") && 
                        (content.Contains("World.Current") || content.Contains("EntityManager")))
                    {
                        Debug.LogWarning($"[Strada] Architecture Warning: Controller may be accessing ECS directly: {path}");
                        issueCount++;
                    }
                }
            }
            
            if (issueCount > 0)
            {
                Debug.LogWarning($"[Strada] Architecture validation found {issueCount} potential issue(s)");
            }
            else
            {
                Debug.Log("[Strada] Architecture validation passed - no issues found");
            }
        }

        #endregion

        #region Hot Reload (Priority 300-349)

        [MenuItem(MenuRoot + "Settings/Hot Reload/Enable Hot Reload", priority = 310)]
        private static void ToggleHotReload()
        {
            HotReloadManager.IsEnabled = !HotReloadManager.IsEnabled;
            Debug.Log($"[Strada] Hot Reload {(HotReloadManager.IsEnabled ? "enabled" : "disabled")}");
        }

        [MenuItem(MenuRoot + "Settings/Hot Reload/Enable Hot Reload", true)]
        private static bool ToggleHotReloadValidate()
        {
            Menu.SetChecked(MenuRoot + "Settings/Hot Reload/Enable Hot Reload", HotReloadManager.IsEnabled);
            return true;
        }

        [MenuItem(MenuRoot + "Settings/Hot Reload/Show Notifications", priority = 311)]
        private static void ToggleHotReloadNotifications()
        {
            HotReloadManager.NotificationsEnabled = !HotReloadManager.NotificationsEnabled;
        }

        [MenuItem(MenuRoot + "Settings/Hot Reload/Show Notifications", true)]
        private static bool ToggleHotReloadNotificationsValidate()
        {
            Menu.SetChecked(MenuRoot + "Settings/Hot Reload/Show Notifications", HotReloadManager.NotificationsEnabled);
            return true;
        }

        [MenuItem(MenuRoot + "Settings/Hot Reload/Open Settings...", priority = 320)]
        private static void OpenHotReloadSettings()
        {
            SettingsService.OpenProjectSettings("Project/Strada/Hot Reload");
        }

        #endregion

        #region Documentation (Priority 200-299)

        [MenuItem(MenuRoot + "Documentation/Getting Started", priority = 200)]
        private static void OpenGettingStarted()
        {
            Application.OpenURL("https://strada-framework.dev/docs/getting-started");
        }

        [MenuItem(MenuRoot + "Documentation/API Reference", priority = 201)]
        private static void OpenAPIReference()
        {
            Application.OpenURL("https://strada-framework.dev/docs/api");
        }

        [MenuItem(MenuRoot + "Documentation/Architecture Guide", priority = 202)]
        private static void OpenArchitectureGuide()
        {
            Application.OpenURL("https://strada-framework.dev/docs/architecture");
        }

        [MenuItem(MenuRoot + "Documentation/Examples", priority = 210)]
        private static void OpenExamples()
        {
            var examplesPath = "Packages/com.strada.core/Samples~";
            if (System.IO.Directory.Exists(examplesPath))
            {
                EditorUtility.RevealInFinder(examplesPath);
            }
            else
            {
                Application.OpenURL("https://strada-framework.dev/docs/examples");
            }
        }

        #endregion

        #region About and Help (Priority 1000+)

        [MenuItem(MenuRoot + "About Strada", priority = 1000)]
        private static void ShowAbout()
        {
            EditorUtility.DisplayDialog(
                "Strada Framework",
                "Version: 1.0.0-alpha\n\n" +
                "Unified MVCS+ECS Framework\n\n" +
                "Performance: World Class\n" +
                "Developer Experience: Modular & Clean\n" +
                "Architecture: Truly unified DI+MVCS+ECS\n\n" +
                "© 2025 Strada Framework Team",
                "OK");
        }

        [MenuItem(MenuRoot + "Help/Report Issue", priority = 1001)]
        private static void ReportIssue()
        {
            Application.OpenURL("https://github.com/strada-framework/strada-core/issues/new");
        }

        [MenuItem(MenuRoot + "Help/Join Discord", priority = 1002)]
        private static void JoinDiscord()
        {
            Application.OpenURL("https://discord.gg/strada");
        }

        [MenuItem(MenuRoot + "Help/Check for Updates", priority = 1003)]
        private static void CheckForUpdates()
        {
            Debug.Log("[Strada] Checking for updates...");
            Application.OpenURL("https://strada-framework.dev/releases");
        }

        #endregion

        #region Keyboard Shortcuts Reference

        // Keyboard shortcuts summary:
        // Ctrl+Shift+Alt+D - Dashboard
        // Ctrl+Shift+N - Create Module
        // Ctrl+Shift+C - Config Data Manager
        // Ctrl+Shift+D - Dependency Graph
        // Ctrl+Shift+M - Module Graph
        // Ctrl+Shift+E - Entity Inspector
        // Ctrl+Shift+P - System Profiler
        // Ctrl+Shift+B - Bus Debugger

        #endregion
    }
}
