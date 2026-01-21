using System.IO;
using System.Linq;
using UnityEditor;

namespace Strada.Core.Editor.CodeGen
{
    /// <summary>
    /// Automatically triggers code generation when relevant files change.
    /// Monitors ModuleConfig assets and [StradaSystem] classes for changes.
    /// </summary>
    public class CodeGenPostprocessor : AssetPostprocessor
    {
        private static bool _isProcessing;
        private static double _lastProcessTime;
        private const double DEBOUNCE_SECONDS = 0.5;

        /// <summary>
        /// Called when assets are imported, deleted, or moved.
        /// </summary>
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_isProcessing || !StradaCodeGenSettings.AutoRegenEnabled)
                return;

            if (EditorApplication.timeSinceStartup - _lastProcessTime < DEBOUNCE_SECONDS)
                return;

            var allChangedPaths = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .ToArray();

            bool shouldRegenerate = false;
            bool moduleConfigChanged = false;
            bool systemClassChanged = false;

            foreach (var path in allChangedPaths)
            {
                if (path.EndsWith(".asset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Modules.ModuleConfig>(path);
                    if (asset != null)
                    {
                        moduleConfigChanged = true;
                        shouldRegenerate = true;

                        Validation.ModuleValidationService.InvalidateCache(asset);
                    }
                }

                if (path.EndsWith(".cs") && !path.Contains("Generated"))
                {
                    if (path.StartsWith("Assets/"))
                    {
                        var content = File.ReadAllText(path);
                        if (content.Contains("[StradaSystem") || content.Contains("[Strada.Core"))
                        {
                            systemClassChanged = true;
                            shouldRegenerate = true;
                        }
                    }
                }
            }

            if (shouldRegenerate)
            {
                EditorApplication.delayCall += () => TriggerCodeGeneration(moduleConfigChanged, systemClassChanged);
            }
        }

        private static void TriggerCodeGeneration(bool moduleConfigChanged, bool systemClassChanged)
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            _lastProcessTime = EditorApplication.timeSinceStartup;

            try
            {
                if (StradaCodeGenSettings.VerboseLogging)
                {
                    UnityEngine.Debug.Log("[Strada] Auto-regenerating code...");
                    if (moduleConfigChanged)
                        UnityEngine.Debug.Log("  - ModuleConfig change detected");
                    if (systemClassChanged)
                        UnityEngine.Debug.Log("  - System class change detected");
                }

                if (moduleConfigChanged)
                {
                    ModuleInitializerGenerator.GenerateModuleRegistry();
                }

                if (systemClassChanged || moduleConfigChanged)
                {
                    SystemRegistryGenerator.GenerateSystemRegistry();
                }

                if (StradaCodeGenSettings.VerboseLogging)
                {
                    UnityEngine.Debug.Log("[Strada] Code generation complete.");
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Settings for code generation behavior.
    /// </summary>
    public static class StradaCodeGenSettings
    {
        private const string AUTO_REGEN_KEY = "Strada.CodeGen.AutoRegen";
        private const string VERBOSE_LOGGING_KEY = "Strada.CodeGen.VerboseLogging";

        /// <summary>
        /// Gets or sets whether auto-regeneration is enabled.
        /// </summary>
        public static bool AutoRegenEnabled
        {
            get => EditorPrefs.GetBool(AUTO_REGEN_KEY, false);
            set => EditorPrefs.SetBool(AUTO_REGEN_KEY, value);
        }

        /// <summary>
        /// Gets or sets whether verbose logging is enabled.
        /// </summary>
        public static bool VerboseLogging
        {
            get => EditorPrefs.GetBool(VERBOSE_LOGGING_KEY, false);
            set => EditorPrefs.SetBool(VERBOSE_LOGGING_KEY, value);
        }

        [MenuItem("Strada/Settings/Enable Auto Code Generation")]
        private static void EnableAutoRegen()
        {
            AutoRegenEnabled = true;
            UnityEngine.Debug.Log("[Strada] Auto code generation enabled.");
        }

        [MenuItem("Strada/Settings/Enable Auto Code Generation", true)]
        private static bool EnableAutoRegenValidate()
        {
            Menu.SetChecked("Strada/Settings/Enable Auto Code Generation", AutoRegenEnabled);
            return !AutoRegenEnabled;
        }

        [MenuItem("Strada/Settings/Disable Auto Code Generation")]
        private static void DisableAutoRegen()
        {
            AutoRegenEnabled = false;
            UnityEngine.Debug.Log("[Strada] Auto code generation disabled.");
        }

        [MenuItem("Strada/Settings/Disable Auto Code Generation", true)]
        private static bool DisableAutoRegenValidate()
        {
            return AutoRegenEnabled;
        }

        [MenuItem("Strada/Settings/Toggle Verbose Logging")]
        private static void ToggleVerboseLogging()
        {
            VerboseLogging = !VerboseLogging;
            UnityEngine.Debug.Log($"[Strada] Verbose logging {(VerboseLogging ? "enabled" : "disabled")}.");
        }

        [MenuItem("Strada/Settings/Toggle Verbose Logging", true)]
        private static bool ToggleVerboseLoggingValidate()
        {
            Menu.SetChecked("Strada/Settings/Toggle Verbose Logging", VerboseLogging);
            return true;
        }
    }
}
