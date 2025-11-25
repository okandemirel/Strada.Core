using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Central menu organization for all Strada editor tools.
    /// Provides quick access to diagnostics, tools, and documentation.
    /// </summary>
    public static class StradaEditorMenus
    {
        private const string MenuRoot = "Strada/";
        
        // Documentation
        [MenuItem(MenuRoot + "Documentation/Getting Started", priority = 0)]
        private static void OpenGettingStarted()
        {
            Application.OpenURL("https://strada-framework.dev/docs/getting-started");
        }

        [MenuItem(MenuRoot + "Documentation/API Reference", priority = 1)]
        private static void OpenAPIReference()
        {
            Application.OpenURL("https://strada-framework.dev/docs/api");
        }

        [MenuItem(MenuRoot + "Documentation/Examples", priority = 2)]
        private static void OpenExamples()
        {
            var examplesPath = "Packages/com.strada.core/Samples~";
            EditorUtility.RevealInFinder(examplesPath);
        }

        // Quick Actions
        [MenuItem(MenuRoot + "Quick Actions/Create InputModule", priority = 50)]
        private static void CreateInputModule()
        {
            Debug.Log("Create InputModule - Template generation would happen here");
        }

        [MenuItem(MenuRoot + "Quick Actions/Create PlayerModule", priority = 51)]
        private static void CreatePlayerModule()
        {
            Debug.Log("Create PlayerModule - Template generation would happen here");
        }

        [MenuItem(MenuRoot + "Quick Actions/Validate All Configs", priority = 52)]
        private static void ValidateAllConfigs()
        {
            var configs = AssetDatabase.FindAssets("t:ScriptableObject");
            int validatedCount = 0;

            foreach (var guid in configs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && asset.name.StartsWith("CD_"))
                {
                    var validateMethod = asset.GetType().GetMethod("Validate");
                    if (validateMethod != null)
                    {
                        validateMethod.Invoke(asset, null);
                        EditorUtility.SetDirty(asset);
                        validatedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Validated {validatedCount} config assets");
        }

        // Settings
        [MenuItem(MenuRoot + "Settings/Framework Settings", priority = 100)]
        private static void OpenSettings()
        {
            // Would open settings provider
            Debug.Log("Open Strada Settings");
        }

        [MenuItem(MenuRoot + "Settings/Performance Targets", priority = 101)]
        private static void OpenPerformanceTargets()
        {
            // Would show performance target configuration
            Debug.Log("Open Performance Targets");
        }

        // About
        [MenuItem(MenuRoot + "About Strada", priority = 200)]
        private static void ShowAbout()
        {
            EditorUtility.DisplayDialog(
                "Strada Framework",
                "Version: 1.0.0-alpha\n\n" +
                "Unified MVCS+ECS Framework\n\n" +
                "Performance: Matches Unity DOTS\n" +
                "Developer Experience: Better than all competitors\n" +
                "Architecture: Truly unified DI+MVCS+ECS\n\n" +
                "© 2025 Strada Framework Team",
                "OK");
        }

        // Help
        [MenuItem(MenuRoot + "Help/Report Issue", priority = 201)]
        private static void ReportIssue()
        {
            Application.OpenURL("https://github.com/strada-framework/strada-core/issues/new");
        }

        [MenuItem(MenuRoot + "Help/Join Discord", priority = 202)]
        private static void JoinDiscord()
        {
            Application.OpenURL("https://discord.gg/strada");
        }
    }
}
