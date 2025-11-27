using Strada.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.HotReload
{
    /// <summary>
    /// Asset postprocessor that detects changes to CD_ config assets during Play Mode.
    /// Queues detected changes for processing by HotReloadManager.
    /// </summary>
    public class ConfigAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Called after assets have been imported, deleted, or moved.
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!Application.isPlaying || !HotReloadManager.IsEnabled)
                return;

            foreach (var assetPath in importedAssets)
            {
                ProcessAssetChange(assetPath);
            }

            foreach (var assetPath in movedAssets)
            {
                ProcessAssetChange(assetPath);
            }
        }
        
        private static void ProcessAssetChange(string assetPath)
        {
            if (!assetPath.EndsWith(".asset"))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (asset == null || !asset.name.StartsWith("CD_"))
                return;

            if (asset is ConfigData config)
            {
                HotReloadManager.QueueConfigChange(assetPath, config);
            }
        }
    }
    
    /// <summary>
    /// Modification processor that detects when CD_ assets are saved.
    /// Provides more immediate detection than OnPostprocessAllAssets.
    /// </summary>
    public class ConfigAssetModificationProcessor : AssetModificationProcessor
    {
        /// <summary>
        /// Called when assets are about to be saved.
        /// </summary>
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (!Application.isPlaying || !HotReloadManager.IsEnabled)
                return paths;

            foreach (var path in paths)
            {
                if (!path.EndsWith(".asset"))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && asset.name.StartsWith("CD_") && asset is ConfigData config)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (Application.isPlaying && HotReloadManager.IsEnabled)
                        {
                            HotReloadManager.QueueConfigChange(path, config);
                        }
                    };
                }
            }
            
            return paths;
        }
    }
}
