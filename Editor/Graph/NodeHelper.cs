using System;
using UnityEditor;

namespace Strada.Core.Editor.Graph
{
    internal static class NodeHelper
    {
        public static bool CanNavigateToSource(Type type)
        {
            if (type == null) return false;
            var guids = AssetDatabase.FindAssets($"t:Script {type.Name}");
            return guids.Length > 0;
        }

        public static void NavigateToSource(Type type)
        {
            if (type == null) return;

            var guids = AssetDatabase.FindAssets($"t:Script {type.Name}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }
        }
    }
}
