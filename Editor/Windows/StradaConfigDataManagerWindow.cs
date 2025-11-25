using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Central management window for all CD_ (Config Data) ScriptableObjects.
    /// Provides quick access, creation, validation, and organization of configs.
    /// Implements the Quantum-inspired ScriptableObject pattern.
    /// </summary>
    public class StradaConfigDataManagerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private ConfigCategory _selectedCategory = ConfigCategory.All;
        private List<ConfigAsset> _cachedConfigs;
        private bool _needsRefresh = true;

        private enum ConfigCategory
        {
            All,
            Player,
            Enemy,
            Weapon,
            Level,
            Input,
            Audio,
            Other
        }

        private class ConfigAsset
        {
            public ScriptableObject Asset;
            public string Name;
            public string Path;
            public ConfigCategory Category;
        }

        [MenuItem("Strada/Tools/Config Data Manager", priority = 102)]
        public static void ShowWindow()
        {
            var window = GetWindow<StradaConfigDataManagerWindow>("Config Data Manager");
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _needsRefresh = true;
        }

        private void OnGUI()
        {
            if (_needsRefresh)
            {
                RefreshConfigList();
                _needsRefresh = false;
            }

            DrawToolbar();
            DrawConfigStats();
            EditorGUILayout.Space(5);
            DrawConfigList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search
            GUILayout.Label("Search:", GUILayout.Width(50));
            var newSearch = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (newSearch != _searchFilter)
            {
                _searchFilter = newSearch;
                _needsRefresh = true;
            }

            GUILayout.Space(10);

            // Category filter
            GUILayout.Label("Category:", GUILayout.Width(60));
            var newCategory = (ConfigCategory)EditorGUILayout.EnumPopup(_selectedCategory, EditorStyles.toolbarPopup, GUILayout.Width(100));
            if (newCategory != _selectedCategory)
            {
                _selectedCategory = newCategory;
                _needsRefresh = true;
            }

            GUILayout.FlexibleSpace();

            // Create new button
            if (GUILayout.Button("Create New CD_", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                ShowCreateMenu();
            }

            // Refresh button
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _needsRefresh = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigStats()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            var totalConfigs = _cachedConfigs?.Count ?? 0;
            var filteredCount = GetFilteredConfigs().Count;

            GUILayout.Label($"Total Configs: {totalConfigs}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Showing: {filteredCount}", EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var filteredConfigs = GetFilteredConfigs();

            if (filteredConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _cachedConfigs?.Count > 0 
                        ? "No configs match the current filter."
                        : "No CD_ configs found.\n\nCreate configs using Assets > Create > Strada menu or the 'Create New CD_' button above.",
                    MessageType.Info);
            }
            else
            {
                // Group by category
                var grouped = filteredConfigs.GroupBy(c => c.Category).OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    EditorGUILayout.LabelField(group.Key.ToString(), EditorStyles.boldLabel);
                    EditorGUILayout.Space(5);

                    foreach (var config in group.OrderBy(c => c.Name))
                    {
                        DrawConfigItem(config);
                    }

                    EditorGUILayout.Space(10);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigItem(ConfigAsset config)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Icon
            var icon = AssetPreview.GetMiniTypeThumbnail(typeof(ScriptableObject));
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

            // Name (clickable)
            if (GUILayout.Button(config.Name, EditorStyles.linkLabel))
            {
                Selection.activeObject = config.Asset;
                EditorGUIUtility.PingObject(config.Asset);
            }

            GUILayout.FlexibleSpace();

            // Path
            GUILayout.Label(config.Path, EditorStyles.miniLabel, GUILayout.Width(300));

            // Actions
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                Selection.activeObject = config.Asset;
            }

            if (GUILayout.Button("Validate", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                ValidateConfig(config);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshConfigList()
        {
            _cachedConfigs = new List<ConfigAsset>();

            // Find all ScriptableObjects with CD_ prefix
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && asset.name.StartsWith("CD_"))
                {
                    _cachedConfigs.Add(new ConfigAsset
                    {
                        Asset = asset,
                        Name = asset.name,
                        Path = path,
                        Category = DetermineCategory(asset.name)
                    });
                }
            }
        }

        private List<ConfigAsset> GetFilteredConfigs()
        {
            if (_cachedConfigs == null)
                return new List<ConfigAsset>();

            var filtered = _cachedConfigs.AsEnumerable();

            // Apply category filter
            if (_selectedCategory != ConfigCategory.All)
            {
                filtered = filtered.Where(c => c.Category == _selectedCategory);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                filtered = filtered.Where(c => 
                    c.Name.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return filtered.ToList();
        }

        private ConfigCategory DetermineCategory(string name)
        {
            var lowerName = name.ToLower();
            
            if (lowerName.Contains("player")) return ConfigCategory.Player;
            if (lowerName.Contains("enemy")) return ConfigCategory.Enemy;
            if (lowerName.Contains("weapon")) return ConfigCategory.Weapon;
            if (lowerName.Contains("level")) return ConfigCategory.Level;
            if (lowerName.Contains("input")) return ConfigCategory.Input;
            if (lowerName.Contains("audio") || lowerName.Contains("sound")) return ConfigCategory.Audio;
            
            return ConfigCategory.Other;
        }

        private void ValidateConfig(ConfigAsset config)
        {
            // Call Validate() method if it exists
            var validateMethod = config.Asset.GetType().GetMethod("Validate");
            if (validateMethod != null)
            {
                validateMethod.Invoke(config.Asset, null);
                EditorUtility.SetDirty(config.Asset);
                Debug.Log($"Validated: {config.Name}");
            }
            else
            {
                Debug.LogWarning($"No Validate() method found on {config.Name}");
            }
        }

        private void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Player Config"), false, () => CreateConfig("Player"));
            menu.AddItem(new GUIContent("Enemy Config"), false, () => CreateConfig("Enemy"));
            menu.AddItem(new GUIContent("Weapon Config"), false, () => CreateConfig("Weapon"));
            menu.AddItem(new GUIContent("Level Config"), false, () => CreateConfig("Level"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Custom Config..."), false, () => CreateConfig("Custom"));
            menu.ShowAsContext();
        }

        private void CreateConfig(string type)
        {
            Debug.Log($"Create {type} Config - Implement creation logic");
            // In full implementation, would create appropriate CD_ asset
        }
    }
}
