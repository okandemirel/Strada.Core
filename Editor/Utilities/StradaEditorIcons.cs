using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Centralized icon management for Strada editor tools.
    /// Caches Unity built-in icons and provides easy access throughout the editor UI.
    /// </summary>
    public static class StradaEditorIcons
    {
        private static readonly Dictionary<string, GUIContent> IconCache = new Dictionary<string, GUIContent>();

        #region Common Icons

        public static GUIContent InfoIcon => GetIcon("console.infoicon", "Info");
        public static GUIContent WarningIcon => GetIcon("console.warnicon", "Warning");
        public static GUIContent ErrorIcon => GetIcon("console.erroricon", "Error");
        public static GUIContent SuccessIcon => GetIcon("TestPassed", "Success");

        public static GUIContent SettingsIcon => GetIcon("Settings", "Settings");
        public static GUIContent RefreshIcon => GetIcon("d_Refresh", "Refresh");
        public static GUIContent SearchIcon => GetIcon("d_Search Icon", "Search");
        public static GUIContent HelpIcon => GetIcon("_Help", "Help");

        public static GUIContent AddIcon => GetIcon("d_Toolbar Plus", "Add");
        public static GUIContent RemoveIcon => GetIcon("d_Toolbar Minus", "Remove");
        public static GUIContent DeleteIcon => GetIcon("d_TreeEditor.Trash", "Delete");

        public static GUIContent EditIcon => GetIcon("d_editicon.sml", "Edit");
        public static GUIContent ViewIcon => GetIcon("d_ViewToolOrbit", "View");
        public static GUIContent CloseIcon => GetIcon("d_winbtn_win_close", "Close");

        #endregion

        #region Strada-Specific Icons

        public static GUIContent ModuleIcon => GetIcon("d_Prefab Icon", "Module");
        public static GUIContent ComponentIcon => GetIcon("d_cs Script Icon", "Component");
        public static GUIContent ServiceIcon => GetIcon("d_CloudConnect", "Service");
        public static GUIContent ModelIcon => GetIcon("d_ScriptableObject Icon", "Model");
        public static GUIContent ViewIcon2 => GetIcon("d_SceneAsset Icon", "View");
        public static GUIContent ControllerIcon => GetIcon("d_Animation Icon", "Controller");

        public static GUIContent DIContainerIcon => GetIcon("d_Package Manager", "DI Container");
        public static GUIContent WorldIcon => GetIcon("d_SceneAsset Icon", "ECS World");
        public static GUIContent EntityIcon => GetIcon("d_GameObject Icon", "Entity");
        public static GUIContent SystemIcon => GetIcon("d_Settings", "System");

        public static GUIContent ConfigDataIcon => GetIcon("d_ScriptableObject Icon", "Config Data");
        public static GUIContent BakerIcon => GetIcon("d_BuildSettings.Editor.Small", "Baker");

        #endregion

        #region Lifetime Icons

        public static GUIContent SingletonIcon => GetIcon("d_winbtn_mac_max", "Singleton");
        public static GUIContent TransientIcon => GetIcon("d_Refresh", "Transient");
        public static GUIContent ScopedIcon => GetIcon("d_SceneAsset Icon", "Scoped");

        #endregion

        #region Status Icons

        public static GUIContent LoadingIcon => GetIcon("d_WaitSpin00", "Loading");
        public static GUIContent LoadedIcon => GetIcon("d_Valid", "Loaded");
        public static GUIContent FailedIcon => GetIcon("d_console.erroricon.sml", "Failed");

        public static GUIContent ValidIcon => GetIcon("d_Valid", "Valid");
        public static GUIContent InvalidIcon => GetIcon("d_Invalid", "Invalid");

        #endregion

        #region Performance Icons

        public static GUIContent PerformanceIcon => GetIcon("d_Profiler.CPU", "Performance");
        public static GUIContent MemoryIcon => GetIcon("d_Profiler.Memory", "Memory");
        public static GUIContent TimeIcon => GetIcon("d_UnityEditor.AnimationWindow", "Time");

        #endregion

        #region Arrow Icons

        public static GUIContent ArrowRightIcon => GetIcon("d_forward", "Arrow Right");
        public static GUIContent ArrowLeftIcon => GetIcon("d_back", "Arrow Left");
        public static GUIContent ArrowUpIcon => GetIcon("d_scrollup", "Arrow Up");
        public static GUIContent ArrowDownIcon => GetIcon("d_scrolldown", "Arrow Down");

        #endregion

        #region Core Methods

        /// <summary>
        /// Gets a cached icon from Unity's built-in icons.
        /// </summary>
        /// <param name="iconName">The name of the Unity built-in icon</param>
        /// <param name="tooltip">Optional tooltip text</param>
        /// <returns>GUIContent with the icon and tooltip</returns>
        public static GUIContent GetIcon(string iconName, string tooltip = "")
        {
            var cacheKey = $"{iconName}|{tooltip}";

            if (!IconCache.TryGetValue(cacheKey, out var content))
            {
                var icon = EditorGUIUtility.IconContent(iconName);
                content = new GUIContent(icon.image, tooltip);
                IconCache[cacheKey] = content;
            }

            return content;
        }

        /// <summary>
        /// Creates a GUIContent with text and icon.
        /// </summary>
        public static GUIContent CreateContent(string text, string iconName, string tooltip = "")
        {
            var icon = GetIcon(iconName, tooltip);
            return new GUIContent(text, icon.image, tooltip);
        }

        /// <summary>
        /// Creates a GUIContent with text and a specific icon texture.
        /// </summary>
        public static GUIContent CreateContent(string text, Texture icon, string tooltip = "")
        {
            return new GUIContent(text, icon, tooltip);
        }

        /// <summary>
        /// Gets an icon for a specific lifetime type.
        /// </summary>
        public static GUIContent GetLifetimeIcon(string lifetime)
        {
            return lifetime switch
            {
                "Singleton" => SingletonIcon,
                "Transient" => TransientIcon,
                "Scoped" => ScopedIcon,
                _ => ComponentIcon
            };
        }

        /// <summary>
        /// Gets a colored icon for validation status.
        /// </summary>
        public static GUIContent GetValidationIcon(bool isValid, bool hasWarnings = false)
        {
            if (!isValid) return InvalidIcon;
            if (hasWarnings) return WarningIcon;
            return ValidIcon;
        }

        /// <summary>
        /// Clears the icon cache. Useful when switching between Pro/Personal skins.
        /// </summary>
        public static void ClearCache()
        {
            IconCache.Clear();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Draws an icon with optional label.
        /// </summary>
        public static void DrawIcon(GUIContent icon, float size = 16f)
        {
            if (icon?.image == null) return;

            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            GUI.DrawTexture(rect, icon.image);
        }

        /// <summary>
        /// Draws an icon button.
        /// </summary>
        public static bool DrawIconButton(GUIContent icon, float size = 24f, string tooltip = "")
        {
            var content = new GUIContent(icon.image, tooltip);
            return GUILayout.Button(content, GUILayout.Width(size), GUILayout.Height(size));
        }

        /// <summary>
        /// Draws an icon with a label next to it.
        /// </summary>
        public static void DrawIconWithLabel(GUIContent icon, string label, float iconSize = 16f)
        {
            EditorGUILayout.BeginHorizontal();
            DrawIcon(icon, iconSize);
            GUILayout.Space(4f);
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
