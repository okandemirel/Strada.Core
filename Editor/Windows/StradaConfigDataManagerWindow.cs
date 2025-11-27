using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.Data;

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
        private string _typeFilter = "";
        private ConfigCategory _selectedCategory = ConfigCategory.All;
        private List<ConfigAsset> _cachedConfigs;
        private bool _needsRefresh = true;
        private HashSet<ConfigAsset> _selectedConfigs = new HashSet<ConfigAsset>();
        private Dictionary<ConfigCategory, bool> _categoryFoldouts = new Dictionary<ConfigCategory, bool>();
        private Dictionary<ConfigAsset, ValidationResult> _validationResults = new Dictionary<ConfigAsset, ValidationResult>();
        private bool _showValidationErrors = false;
        private bool _selectAll = false;

        // Creation wizard state
        private bool _showCreationWizard = false;
        private string _newConfigName = "";
        private ConfigCategory _newConfigCategory = ConfigCategory.Other;
        private int _selectedConfigTypeIndex = 0;
        private string[] _availableConfigTypes;
        private Type[] _configTypeList;

        public enum ConfigCategory
        {
            All,
            Player,
            Enemy,
            Weapon,
            Level,
            Input,
            Audio,
            UI,
            Game,
            Other
        }

        public class ConfigAsset
        {
            public ScriptableObject Asset;
            public string Name;
            public string Path;
            public ConfigCategory Category;
            public Type AssetType;
            public bool HasValidateMethod;
        }

        public class ValidationResult
        {
            public bool IsValid;
            public string ErrorMessage;
            public DateTime ValidatedAt;
        }

        public static void ShowWindow()
        {
            var window = GetWindow<StradaConfigDataManagerWindow>("Config Data Manager");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _needsRefresh = true;
            InitializeCategoryFoldouts();
            CacheConfigTypes();
        }

        private void InitializeCategoryFoldouts()
        {
            foreach (ConfigCategory category in Enum.GetValues(typeof(ConfigCategory)))
            {
                if (category != ConfigCategory.All && !_categoryFoldouts.ContainsKey(category))
                {
                    _categoryFoldouts[category] = true;
                }
            }
        }

        private void CacheConfigTypes()
        {
            var configDataType = typeof(ConfigData);
            var types = new List<Type>();
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && configDataType.IsAssignableFrom(type))
                        {
                            types.Add(type);
                        }
                    }
                }
                catch { /* Skip assemblies that can't be loaded */ }
            }

            _configTypeList = types.ToArray();
            _availableConfigTypes = types.Select(t => t.Name).ToArray();
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

            if (_showCreationWizard)
            {
                DrawCreationWizard();
            }
            else
            {
                DrawBulkOperationsBar();
                DrawConfigList();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search by name
            GUILayout.Label("Name:", GUILayout.Width(40));
            var newSearch = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
            if (newSearch != _searchFilter)
            {
                _searchFilter = newSearch;
            }

            GUILayout.Space(5);

            // Search by type
            GUILayout.Label("Type:", GUILayout.Width(35));
            var newTypeFilter = EditorGUILayout.TextField(_typeFilter, EditorStyles.toolbarSearchField, GUILayout.Width(120));
            if (newTypeFilter != _typeFilter)
            {
                _typeFilter = newTypeFilter;
            }

            GUILayout.Space(10);

            // Category filter
            GUILayout.Label("Category:", GUILayout.Width(60));
            var newCategory = (ConfigCategory)EditorGUILayout.EnumPopup(_selectedCategory, EditorStyles.toolbarPopup, GUILayout.Width(100));
            if (newCategory != _selectedCategory)
            {
                _selectedCategory = newCategory;
            }

            GUILayout.FlexibleSpace();

            // Show validation errors toggle
            var newShowErrors = GUILayout.Toggle(_showValidationErrors, "Show Errors Only", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (newShowErrors != _showValidationErrors)
            {
                _showValidationErrors = newShowErrors;
            }

            GUILayout.Space(5);

            // Create new button
            if (GUILayout.Button(_showCreationWizard ? "Cancel" : "Create New", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _showCreationWizard = !_showCreationWizard;
                if (_showCreationWizard)
                {
                    _newConfigName = "";
                    _newConfigCategory = ConfigCategory.Other;
                }
            }

            // Refresh button
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _needsRefresh = true;
                _validationResults.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigStats()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            var totalConfigs = _cachedConfigs?.Count ?? 0;
            var filteredCount = GetFilteredConfigs().Count;
            var selectedCount = _selectedConfigs.Count;
            var errorCount = _validationResults.Count(v => !v.Value.IsValid);

            GUILayout.Label($"Total: {totalConfigs}", EditorStyles.boldLabel);
            GUILayout.Space(20);
            GUILayout.Label($"Showing: {filteredCount}", EditorStyles.boldLabel);
            GUILayout.Space(20);
            GUILayout.Label($"Selected: {selectedCount}", EditorStyles.boldLabel);
            
            if (errorCount > 0)
            {
                GUILayout.Space(20);
                var prevColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label($"Errors: {errorCount}", EditorStyles.boldLabel);
                GUI.color = prevColor;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBulkOperationsBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Select all toggle
            var newSelectAll = EditorGUILayout.Toggle(_selectAll, GUILayout.Width(20));
            if (newSelectAll != _selectAll)
            {
                _selectAll = newSelectAll;
                if (_selectAll)
                {
                    foreach (var config in GetFilteredConfigs())
                    {
                        _selectedConfigs.Add(config);
                    }
                }
                else
                {
                    _selectedConfigs.Clear();
                }
            }
            GUILayout.Label("Select All", GUILayout.Width(70));

            GUILayout.Space(20);

            // Bulk operations
            GUI.enabled = _selectedConfigs.Count > 0;

            if (GUILayout.Button($"Validate Selected ({_selectedConfigs.Count})", GUILayout.Width(150)))
            {
                ValidateSelectedConfigs();
            }

            if (GUILayout.Button("Clear Selection", GUILayout.Width(100)))
            {
                _selectedConfigs.Clear();
                _selectAll = false;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Validate All", GUILayout.Width(100)))
            {
                ValidateAllConfigs();
            }

            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }


        private void DrawCreationWizard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Create New Config", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Config name
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(80));
            _newConfigName = EditorGUILayout.TextField(_newConfigName);
            EditorGUILayout.EndHorizontal();

            // Auto-add CD_ prefix preview
            if (!string.IsNullOrEmpty(_newConfigName))
            {
                var finalName = _newConfigName.StartsWith("CD_") ? _newConfigName : $"CD_{_newConfigName}";
                EditorGUILayout.HelpBox($"Asset will be created as: {finalName}", MessageType.Info);
            }

            EditorGUILayout.Space(5);

            // Category selection
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Category:", GUILayout.Width(80));
            _newConfigCategory = (ConfigCategory)EditorGUILayout.EnumPopup(_newConfigCategory);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Config type selection
            if (_availableConfigTypes != null && _availableConfigTypes.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Type:", GUILayout.Width(80));
                _selectedConfigTypeIndex = EditorGUILayout.Popup(_selectedConfigTypeIndex, _availableConfigTypes);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("No ConfigData types found. Create a class inheriting from ConfigData first.", MessageType.Warning);
            }

            EditorGUILayout.Space(15);

            // Create button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrEmpty(_newConfigName) && _configTypeList != null && _configTypeList.Length > 0;
            if (GUILayout.Button("Create Config", GUILayout.Width(120), GUILayout.Height(30)))
            {
                CreateNewConfig();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(30)))
            {
                _showCreationWizard = false;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateNewConfig()
        {
            if (_configTypeList == null || _selectedConfigTypeIndex >= _configTypeList.Length)
                return;

            var configType = _configTypeList[_selectedConfigTypeIndex];
            var finalName = _newConfigName.StartsWith("CD_") ? _newConfigName : $"CD_{_newConfigName}";

            // Get save path
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Config",
                finalName,
                "asset",
                "Choose location for the new config asset");

            if (string.IsNullOrEmpty(path))
                return;

            // Create the asset
            var instance = ScriptableObject.CreateInstance(configType);
            instance.name = finalName;

            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the new asset
            Selection.activeObject = instance;
            EditorGUIUtility.PingObject(instance);

            // Refresh the list and close wizard
            _showCreationWizard = false;
            _needsRefresh = true;

            Debug.Log($"Created config: {finalName} at {path}");
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
                        : "No CD_ configs found.\n\nCreate configs using the 'Create New' button above or Assets > Create > Strada menu.",
                    MessageType.Info);
            }
            else
            {
                // Group by category with collapsible sections
                var grouped = filteredConfigs.GroupBy(c => c.Category).OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    DrawCategoryGroup(group.Key, group.ToList());
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCategoryGroup(ConfigCategory category, List<ConfigAsset> configs)
        {
            if (!_categoryFoldouts.ContainsKey(category))
            {
                _categoryFoldouts[category] = true;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Category header with foldout
            EditorGUILayout.BeginHorizontal();
            
            _categoryFoldouts[category] = EditorGUILayout.Foldout(_categoryFoldouts[category], "", true);
            
            var categoryIcon = GetCategoryIcon(category);
            GUILayout.Label(categoryIcon, GUILayout.Width(20), GUILayout.Height(20));
            
            EditorGUILayout.LabelField($"{category} ({configs.Count})", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();

            // Category-level actions
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                foreach (var config in configs)
                {
                    _selectedConfigs.Add(config);
                }
            }

            if (GUILayout.Button("Validate", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                foreach (var config in configs)
                {
                    ValidateConfig(config);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Draw configs if expanded
            if (_categoryFoldouts[category])
            {
                EditorGUI.indentLevel++;
                foreach (var config in configs.OrderBy(c => c.Name))
                {
                    DrawConfigItem(config);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }


        private GUIContent GetCategoryIcon(ConfigCategory category)
        {
            // Use built-in Unity icons based on category
            string iconName = category switch
            {
                ConfigCategory.Player => "d_UnityEditor.AnimationWindow",
                ConfigCategory.Enemy => "d_UnityEditor.SceneHierarchyWindow",
                ConfigCategory.Weapon => "d_Toolbar Minus",
                ConfigCategory.Level => "d_SceneAsset Icon",
                ConfigCategory.Input => "d_UnityEditor.GameView",
                ConfigCategory.Audio => "d_AudioSource Icon",
                ConfigCategory.UI => "d_RectTransform Icon",
                ConfigCategory.Game => "d_UnityEditor.ConsoleWindow",
                _ => "d_ScriptableObject Icon"
            };
            
            return EditorGUIUtility.IconContent(iconName);
        }

        private void DrawConfigItem(ConfigAsset config)
        {
            var hasError = _validationResults.TryGetValue(config, out var result) && !result.IsValid;
            
            // Skip if showing errors only and this config has no error
            if (_showValidationErrors && !hasError)
                return;

            var bgColor = GUI.backgroundColor;
            if (hasError)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.3f);
            }
            else if (_selectedConfigs.Contains(config))
            {
                GUI.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.3f);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = bgColor;

            // Selection checkbox
            var isSelected = _selectedConfigs.Contains(config);
            var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            if (newSelected != isSelected)
            {
                if (newSelected)
                    _selectedConfigs.Add(config);
                else
                    _selectedConfigs.Remove(config);
            }

            // Error/Valid icon
            if (hasError)
            {
                var errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
                errorIcon.tooltip = result.ErrorMessage;
                GUILayout.Label(errorIcon, GUILayout.Width(20), GUILayout.Height(20));
            }
            else if (_validationResults.ContainsKey(config))
            {
                var validIcon = EditorGUIUtility.IconContent("d_greenLight");
                validIcon.tooltip = "Validation passed";
                GUILayout.Label(validIcon, GUILayout.Width(20), GUILayout.Height(20));
            }
            else
            {
                // Type icon
                var icon = AssetPreview.GetMiniTypeThumbnail(config.AssetType ?? typeof(ScriptableObject));
                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            }

            // Name (clickable)
            var nameStyle = new GUIStyle(EditorStyles.linkLabel);
            if (hasError)
            {
                nameStyle.normal.textColor = Color.red;
            }
            
            if (GUILayout.Button(config.Name, nameStyle))
            {
                Selection.activeObject = config.Asset;
                EditorGUIUtility.PingObject(config.Asset);
            }

            GUILayout.FlexibleSpace();

            // Type name
            GUILayout.Label(config.AssetType?.Name ?? "Unknown", EditorStyles.miniLabel, GUILayout.Width(150));

            // Path
            GUILayout.Label(TruncatePath(config.Path, 40), EditorStyles.miniLabel, GUILayout.Width(250));

            // Actions
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                Selection.activeObject = config.Asset;
            }

            GUI.enabled = config.HasValidateMethod;
            if (GUILayout.Button("Validate", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                ValidateConfig(config);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Show error message if present
            if (hasError && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(result.ErrorMessage, MessageType.Error);
                EditorGUI.indentLevel--;
            }
        }

        private string TruncatePath(string path, int maxLength)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
                return path;
            
            return "..." + path.Substring(path.Length - maxLength + 3);
        }

        /// <summary>
        /// Refreshes the cached config list by scanning for all CD_ prefixed ScriptableObjects.
        /// </summary>
        public void RefreshConfigList()
        {
            _cachedConfigs = DiscoverConfigs();
        }

        /// <summary>
        /// Discovers all CD_ prefixed ScriptableObjects in the project.
        /// </summary>
        /// <returns>List of discovered config assets</returns>
        public static List<ConfigAsset> DiscoverConfigs()
        {
            var configs = new List<ConfigAsset>();

            // Find all ScriptableObjects with CD_ prefix
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && asset.name.StartsWith("CD_"))
                {
                    var assetType = asset.GetType();
                    var hasValidate = assetType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance) != null;

                    configs.Add(new ConfigAsset
                    {
                        Asset = asset,
                        Name = asset.name,
                        Path = path,
                        Category = DetermineCategory(asset.name, assetType),
                        AssetType = assetType,
                        HasValidateMethod = hasValidate
                    });
                }
            }

            return configs;
        }

        /// <summary>
        /// Gets the filtered list of configs based on current search and category filters.
        /// </summary>
        public List<ConfigAsset> GetFilteredConfigs()
        {
            return FilterConfigs(_cachedConfigs, _searchFilter, _typeFilter, _selectedCategory);
        }

        /// <summary>
        /// Filters configs by name, type, and category.
        /// </summary>
        public static List<ConfigAsset> FilterConfigs(
            List<ConfigAsset> configs, 
            string nameFilter, 
            string typeFilter, 
            ConfigCategory categoryFilter)
        {
            if (configs == null)
                return new List<ConfigAsset>();

            var filtered = configs.AsEnumerable();

            // Apply category filter
            if (categoryFilter != ConfigCategory.All)
            {
                filtered = filtered.Where(c => c.Category == categoryFilter);
            }

            // Apply name search filter
            if (!string.IsNullOrEmpty(nameFilter))
            {
                filtered = filtered.Where(c => 
                    c.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Apply type search filter
            if (!string.IsNullOrEmpty(typeFilter))
            {
                filtered = filtered.Where(c => 
                    c.AssetType != null && 
                    c.AssetType.Name.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return filtered.ToList();
        }


        /// <summary>
        /// Determines the category of a config based on its name and type.
        /// </summary>
        public static ConfigCategory DetermineCategory(string name, Type assetType = null)
        {
            var lowerName = name.ToLower();
            var typeName = assetType?.Name.ToLower() ?? "";
            
            // Check name first
            if (lowerName.Contains("player")) return ConfigCategory.Player;
            if (lowerName.Contains("enemy") || lowerName.Contains("ai")) return ConfigCategory.Enemy;
            if (lowerName.Contains("weapon") || lowerName.Contains("gun") || lowerName.Contains("sword")) return ConfigCategory.Weapon;
            if (lowerName.Contains("level") || lowerName.Contains("stage") || lowerName.Contains("map")) return ConfigCategory.Level;
            if (lowerName.Contains("input") || lowerName.Contains("control")) return ConfigCategory.Input;
            if (lowerName.Contains("audio") || lowerName.Contains("sound") || lowerName.Contains("music")) return ConfigCategory.Audio;
            if (lowerName.Contains("ui") || lowerName.Contains("menu") || lowerName.Contains("hud")) return ConfigCategory.UI;
            if (lowerName.Contains("game") || lowerName.Contains("settings") || lowerName.Contains("config")) return ConfigCategory.Game;
            
            // Check type name as fallback
            if (typeName.Contains("player")) return ConfigCategory.Player;
            if (typeName.Contains("enemy")) return ConfigCategory.Enemy;
            if (typeName.Contains("weapon")) return ConfigCategory.Weapon;
            if (typeName.Contains("level")) return ConfigCategory.Level;
            if (typeName.Contains("input")) return ConfigCategory.Input;
            if (typeName.Contains("audio")) return ConfigCategory.Audio;
            if (typeName.Contains("ui")) return ConfigCategory.UI;
            if (typeName.Contains("game")) return ConfigCategory.Game;
            
            return ConfigCategory.Other;
        }

        /// <summary>
        /// Validates a single config and stores the result.
        /// </summary>
        public ValidationResult ValidateConfig(ConfigAsset config)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                ErrorMessage = null,
                ValidatedAt = DateTime.Now
            };

            if (config.Asset == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Asset is null or has been destroyed";
                _validationResults[config] = result;
                return result;
            }

            // Try to call Validate() method if it exists
            var validateMethod = config.Asset.GetType().GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance);
            if (validateMethod != null)
            {
                try
                {
                    var returnType = validateMethod.ReturnType;
                    
                    if (returnType == typeof(bool))
                    {
                        // Validate() returns bool
                        result.IsValid = (bool)validateMethod.Invoke(config.Asset, null);
                        if (!result.IsValid)
                        {
                            result.ErrorMessage = "Validation failed";
                        }
                    }
                    else if (returnType == typeof(string))
                    {
                        // Validate() returns error string (null = valid)
                        var errorMsg = (string)validateMethod.Invoke(config.Asset, null);
                        result.IsValid = string.IsNullOrEmpty(errorMsg);
                        result.ErrorMessage = errorMsg;
                    }
                    else if (returnType == typeof(void))
                    {
                        // Validate() returns void - assume valid if no exception
                        validateMethod.Invoke(config.Asset, null);
                        result.IsValid = true;
                    }
                    else
                    {
                        // Unknown return type - just call it
                        validateMethod.Invoke(config.Asset, null);
                        result.IsValid = true;
                    }

                    EditorUtility.SetDirty(config.Asset);
                }
                catch (Exception ex)
                {
                    result.IsValid = false;
                    result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
                }
            }
            else
            {
                // No Validate method - consider valid by default
                result.IsValid = true;
                result.ErrorMessage = null;
            }

            _validationResults[config] = result;
            return result;
        }

        /// <summary>
        /// Validates all selected configs.
        /// </summary>
        public void ValidateSelectedConfigs()
        {
            var configs = _selectedConfigs.ToList();
            ValidateConfigs(configs);
        }

        /// <summary>
        /// Validates all discovered configs.
        /// </summary>
        public void ValidateAllConfigs()
        {
            if (_cachedConfigs == null)
                return;

            ValidateConfigs(_cachedConfigs);
        }

        /// <summary>
        /// Validates a list of configs and displays progress.
        /// </summary>
        public void ValidateConfigs(List<ConfigAsset> configs)
        {
            var total = configs.Count;
            var current = 0;
            var errors = 0;

            try
            {
                foreach (var config in configs)
                {
                    current++;
                    EditorUtility.DisplayProgressBar(
                        "Validating Configs",
                        $"Validating {config.Name} ({current}/{total})",
                        (float)current / total);

                    var result = ValidateConfig(config);
                    if (!result.IsValid)
                    {
                        errors++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (errors > 0)
            {
                Debug.LogWarning($"Validation complete: {errors} error(s) found in {total} configs");
                _showValidationErrors = true;
            }
            else
            {
                Debug.Log($"Validation complete: All {total} configs passed");
            }

            Repaint();
        }

        /// <summary>
        /// Gets the cached configs list.
        /// </summary>
        public List<ConfigAsset> GetCachedConfigs()
        {
            return _cachedConfigs;
        }

        /// <summary>
        /// Gets the validation results dictionary.
        /// </summary>
        public Dictionary<ConfigAsset, ValidationResult> GetValidationResults()
        {
            return _validationResults;
        }
    }
}
