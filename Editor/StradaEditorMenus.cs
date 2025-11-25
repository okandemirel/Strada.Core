using UnityEditor;
using UnityEngine;
using Strada.Core.Editor.Windows;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Central menu organization for all Strada editor tools.
    /// Provides quick access to diagnostics, tools, and documentation.
    /// </summary>
    public static class StradaEditorMenus
    {
        private const string MenuRoot = "Strada/";
        
        // Tools
        [MenuItem(MenuRoot + "Create Module...", priority = 50)]
        private static void OpenModuleGenerator()
        {
            StradaModuleGeneratorWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Debugger/Entity Inspector", priority = 100)]
        private static void OpenEntityDebugger()
        {
            StradaEntityDebuggerWindow.ShowWindow();
        }

        // Documentation
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

        [MenuItem(MenuRoot + "Documentation/Examples", priority = 202)]
        private static void OpenExamples()
        {
            var examplesPath = "Packages/com.strada.core/Samples~";
            EditorUtility.RevealInFinder(examplesPath);
        }

        // Quick Actions
        [MenuItem(MenuRoot + "Tools/Validate All Configs", priority = 300)]
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
            Debug.Log($"[Strada] Validated {validatedCount} config assets");
        }

        // About
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

        // Help
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
    }
}
