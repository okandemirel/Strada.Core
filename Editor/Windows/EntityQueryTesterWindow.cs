using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using Strada.Core.ECS.World;
using Strada.Core.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for testing ECS entity queries in real-time.
    /// Allows selecting up to four component types, running queries against the active World,
    /// and inspecting matching entities with their component values.
    /// Supports live mode with auto-refresh, query history, and save/load of named queries.
    /// </summary>
    public class EntityQueryTesterWindow : EditorWindow
    {
        private const int MaxSelectedComponents = 4;
        private const int MaxQueryHistory = 10;
        private const int MaxResultsPerPage = 100;
        private const float RefreshIntervalSeconds = 0.5f;

        private List<Type> _availableComponentTypes = new List<Type>();
        private List<Type> _filteredComponentTypes = new List<Type>();
        private bool _componentTypesCached;

        private List<Type> _selectedComponentTypes = new List<Type>();
        private string _componentSearchFilter = "";

        private bool _showComponentPicker;
        private Vector2 _componentPickerScrollPosition;

        private List<QueryResultEntry> _queryResults = new List<QueryResultEntry>();
        private Vector2 _resultsScrollPosition;
        private double _lastQueryTimeMs;
        private int _lastQueryMatchCount;

        private bool _liveMode;
        private double _lastLiveRefreshTime;

        private List<SavedQuery> _queryHistory = new List<SavedQuery>();
        private List<SavedQuery> _savedQueries = new List<SavedQuery>();
        private string _saveQueryName = "";
        private bool _showSaveDialog;
        private bool _showLoadDialog;
        private Vector2 _historyScrollPosition;

        private Dictionary<int, bool> _expandedResults = new Dictionary<int, bool>();

        private GUIStyle _headerStyle;
        private GUIStyle _resultRowStyle;
        private GUIStyle _componentBadgeStyle;
        private bool _stylesInitialized;

        /// <summary>
        /// Opens the Entity Query Tester window.
        /// </summary>
        [MenuItem("Strada/Tools/Query Tester", priority = 52)]
        public static void ShowWindow()
        {
            var window = GetWindow<EntityQueryTesterWindow>("Query Tester");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            CacheComponentTypes();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                CacheComponentTypes();
                _queryResults.Clear();
                _expandedResults.Clear();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _queryResults.Clear();
                _expandedResults.Clear();
                _liveMode = false;
            }

            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying || !_liveMode) return;

            if (EditorApplication.timeSinceStartup - _lastLiveRefreshTime > RefreshIntervalSeconds)
            {
                if (_selectedComponentTypes.Count > 0)
                {
                    ExecuteQuery();
                }

                _lastLiveRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 10, 5)
            };

            _resultRowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(5, 5, 2, 2)
            };

            _componentBadgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(6, 6, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            DrawToolbar();

            if (!Application.isPlaying)
            {
                DrawNotPlayingMessage();
                return;
            }

            if (World.Current == null)
            {
                DrawNoWorldMessage();
                return;
            }

            DrawComponentSelector();
            StradaEditorStyles.DrawSeparator();
            DrawResultsPanel();
            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying || _selectedComponentTypes.Count == 0);
            if (GUILayout.Button("Run Query", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                ExecuteQuery();
                AddToHistory();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(5);

            var prevLive = _liveMode;
            _liveMode = GUILayout.Toggle(_liveMode, "Live Mode", EditorStyles.toolbarButton, GUILayout.Width(75));
            if (_liveMode && !prevLive)
            {
                _lastLiveRefreshTime = EditorApplication.timeSinceStartup;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Clear Results", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _queryResults.Clear();
                _expandedResults.Clear();
                _lastQueryMatchCount = 0;
                _lastQueryTimeMs = 0;
            }

            if (GUILayout.Button("Clear Selection", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _selectedComponentTypes.Clear();
                _queryResults.Clear();
                _expandedResults.Clear();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save Query", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                _showSaveDialog = !_showSaveDialog;
                _showLoadDialog = false;
            }

            if (GUILayout.Button("Load Query", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                _showLoadDialog = !_showLoadDialog;
                _showSaveDialog = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNotPlayingMessage()
        {
            GUILayout.Space(50);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(450));
            EditorGUILayout.HelpBox(
                "ENTITY QUERY TESTER\n\n" +
                "Test ECS queries in real-time:\n" +
                "  - Select up to 4 component types to form a query\n" +
                "  - Run the query to find all matching entities\n" +
                "  - Inspect entity IDs and component values\n" +
                "  - Enable live mode for auto-refreshing results\n" +
                "  - Save and load named queries for reuse\n\n" +
                "Enter Play Mode to start testing queries.",
                MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enter Play Mode", GUILayout.Height(30), GUILayout.Width(150)))
            {
                EditorApplication.isPlaying = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoWorldMessage()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "No active ECS World found.\n\nCreate a World to begin testing queries.",
                MessageType.Warning,
                true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Query Components", StradaEditorStyles.SubHeaderStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{_selectedComponentTypes.Count}/{MaxSelectedComponents} selected",
                StradaEditorStyles.MiniLabelStyle, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            if (_selectedComponentTypes.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = _selectedComponentTypes.Count - 1; i >= 0; i--)
                {
                    var type = _selectedComponentTypes[i];
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = StradaEditorStyles.InfoColor;

                    if (GUILayout.Button($"{type.Name} x", _componentBadgeStyle, GUILayout.MinWidth(60)))
                    {
                        _selectedComponentTypes.RemoveAt(i);
                    }

                    GUI.backgroundColor = prevBg;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.BeginDisabledGroup(_selectedComponentTypes.Count >= MaxSelectedComponents);
            if (GUILayout.Button("+ Add Component Type", GUILayout.Height(22)))
            {
                _showComponentPicker = !_showComponentPicker;
                _componentSearchFilter = "";
                ApplyComponentFilter();
            }
            EditorGUI.EndDisabledGroup();

            if (_showComponentPicker)
            {
                DrawComponentPicker();
            }

            if (_showSaveDialog)
            {
                DrawSaveDialog();
            }

            if (_showLoadDialog)
            {
                DrawLoadDialog();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentPicker()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            var newFilter = EditorGUILayout.TextField(_componentSearchFilter);
            if (newFilter != _componentSearchFilter)
            {
                _componentSearchFilter = newFilter;
                ApplyComponentFilter();
            }

            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                _showComponentPicker = false;
            }
            EditorGUILayout.EndHorizontal();

            _componentPickerScrollPosition = EditorGUILayout.BeginScrollView(
                _componentPickerScrollPosition, GUILayout.MaxHeight(180));

            foreach (var type in _filteredComponentTypes)
            {
                bool alreadySelected = _selectedComponentTypes.Contains(type);
                EditorGUI.BeginDisabledGroup(alreadySelected);

                if (GUILayout.Button(FormatComponentTypeName(type), EditorStyles.miniButton))
                {
                    _selectedComponentTypes.Add(type);
                    if (_selectedComponentTypes.Count >= MaxSelectedComponents)
                    {
                        _showComponentPicker = false;
                    }
                }

                EditorGUI.EndDisabledGroup();
            }

            if (_filteredComponentTypes.Count == 0)
            {
                EditorGUILayout.LabelField("No matching component types found.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSaveDialog()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Save Query", EditorStyles.boldLabel);

            if (_selectedComponentTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("Select at least one component type before saving.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.Width(45));
                _saveQueryName = EditorGUILayout.TextField(_saveQueryName);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_saveQueryName));
                if (GUILayout.Button("Save", GUILayout.Width(50)))
                {
                    var saved = new SavedQuery
                    {
                        Name = _saveQueryName.Trim(),
                        ComponentTypeNames = _selectedComponentTypes.Select(t => t.AssemblyQualifiedName).ToList(),
                        Timestamp = DateTime.Now
                    };
                    _savedQueries.Add(saved);
                    _saveQueryName = "";
                    _showSaveDialog = false;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                _showSaveDialog = false;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLoadDialog()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Load Query", EditorStyles.boldLabel);

            if (_savedQueries.Count == 0 && _queryHistory.Count == 0)
            {
                EditorGUILayout.HelpBox("No saved queries or history available.", MessageType.Info);
            }
            else
            {
                if (_savedQueries.Count > 0)
                {
                    EditorGUILayout.LabelField("Saved Queries:", EditorStyles.miniLabel);
                    for (int i = _savedQueries.Count - 1; i >= 0; i--)
                    {
                        var query = _savedQueries[i];
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button(query.Name, EditorStyles.miniButton))
                        {
                            LoadQuery(query);
                            _showLoadDialog = false;
                        }

                        if (GUILayout.Button("x", GUILayout.Width(20)))
                        {
                            _savedQueries.RemoveAt(i);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (_queryHistory.Count > 0)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.LabelField("Recent History:", EditorStyles.miniLabel);
                    _historyScrollPosition = EditorGUILayout.BeginScrollView(
                        _historyScrollPosition, GUILayout.MaxHeight(120));

                    foreach (var query in _queryHistory)
                    {
                        string label = string.Join(", ", query.ComponentTypeNames.Select(GetShortTypeName));
                        if (GUILayout.Button(label, EditorStyles.miniButton))
                        {
                            LoadQuery(query);
                            _showLoadDialog = false;
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            if (GUILayout.Button("Close", GUILayout.Width(60)))
            {
                _showLoadDialog = false;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawResultsPanel()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Results", StradaEditorStyles.SubHeaderStyle);
            GUILayout.FlexibleSpace();
            if (_lastQueryMatchCount > 0)
            {
                EditorGUILayout.LabelField($"{_lastQueryMatchCount} entities matched | {_lastQueryTimeMs:F3} ms",
                    StradaEditorStyles.MiniLabelStyle, GUILayout.Width(200));
            }
            EditorGUILayout.EndHorizontal();

            _resultsScrollPosition = EditorGUILayout.BeginScrollView(_resultsScrollPosition);

            if (_queryResults.Count == 0)
            {
                if (_selectedComponentTypes.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Select component types above, then click 'Run Query' to find matching entities.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No results. Click 'Run Query' or enable Live Mode.",
                        MessageType.Info);
                }
            }
            else
            {
                int displayCount = Mathf.Min(_queryResults.Count, MaxResultsPerPage);
                for (int i = 0; i < displayCount; i++)
                {
                    DrawResultRow(_queryResults[i]);
                }

                if (_queryResults.Count > MaxResultsPerPage)
                {
                    EditorGUILayout.HelpBox(
                        $"Showing {MaxResultsPerPage} of {_queryResults.Count} results.",
                        MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawResultRow(QueryResultEntry entry)
        {
            if (!_expandedResults.ContainsKey(entry.EntityIndex))
            {
                _expandedResults[entry.EntityIndex] = false;
            }

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.5f);

            EditorGUILayout.BeginVertical(_resultRowStyle);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();

            _expandedResults[entry.EntityIndex] = EditorGUILayout.Foldout(
                _expandedResults[entry.EntityIndex],
                $"Entity [{entry.EntityIndex}]",
                true);

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"{entry.ComponentValues.Count} components",
                StradaEditorStyles.MiniLabelStyle, GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();

            if (_expandedResults[entry.EntityIndex])
            {
                EditorGUI.indentLevel++;
                foreach (var compEntry in entry.ComponentValues)
                {
                    DrawComponentValues(compEntry.Key, compEntry.Value);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentValues(Type componentType, object value)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var prevColor = GUI.contentColor;
            GUI.contentColor = StradaEditorStyles.InfoColor;
            EditorGUILayout.LabelField(componentType.Name, EditorStyles.boldLabel);
            GUI.contentColor = prevColor;

            if (value == null)
            {
                EditorGUILayout.LabelField("(null)", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                if (fields.Length == 0)
                {
                    EditorGUILayout.LabelField("(no public fields)", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    foreach (var field in fields)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(field.Name, GUILayout.Width(140));
                        var fieldValue = field.GetValue(value);
                        EditorGUILayout.SelectableLabel(
                            FormatFieldValue(fieldValue),
                            EditorStyles.textField,
                            GUILayout.Height(18));
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            StradaEditorStyles.DrawSeparator();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_liveMode)
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = StradaEditorStyles.SuccessColor;
                GUILayout.Label("LIVE", EditorStyles.toolbarButton, GUILayout.Width(40));
                GUI.contentColor = prevColor;
            }

            GUILayout.Label($"Component Types: {_availableComponentTypes.Count}", GUILayout.Width(140));
            GUILayout.Label($"Selected: {_selectedComponentTypes.Count}", GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            if (World.Current != null)
            {
                int entityCount = World.Current.EntityManager.EntityCount;
                GUILayout.Label($"World Entities: {entityCount}", GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Executes the current query against the active World's entity store.
        /// Finds all entities that have every selected component type.
        /// </summary>
        private void ExecuteQuery()
        {
            _queryResults.Clear();
            _expandedResults.Clear();

            if (World.Current == null || _selectedComponentTypes.Count == 0) return;

            var store = World.Current.EntityManager.Store;
            var entityManager = World.Current.EntityManager;

            var sw = Stopwatch.StartNew();

            try
            {
                var allEntities = entityManager.GetAllEntities().ToList();

                foreach (var entityIndex in allEntities)
                {
                    bool matchesAll = true;
                    foreach (var componentType in _selectedComponentTypes)
                    {
                        if (!store.HasComponent(entityIndex, componentType))
                        {
                            matchesAll = false;
                            break;
                        }
                    }

                    if (matchesAll)
                    {
                        var entry = new QueryResultEntry
                        {
                            EntityIndex = entityIndex,
                            ComponentValues = new Dictionary<Type, object>()
                        };

                        foreach (var componentType in _selectedComponentTypes)
                        {
                            var value = store.GetComponentBoxed(entityIndex, componentType);
                            entry.ComponentValues[componentType] = value;
                        }

                        _queryResults.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QueryTester] Query execution failed: {ex.Message}");
            }

            sw.Stop();
            _lastQueryTimeMs = sw.Elapsed.TotalMilliseconds;
            _lastQueryMatchCount = _queryResults.Count;
        }

        /// <summary>
        /// Adds the current component type selection to the query history ring buffer.
        /// </summary>
        private void AddToHistory()
        {
            if (_selectedComponentTypes.Count == 0) return;

            var historyEntry = new SavedQuery
            {
                Name = null,
                ComponentTypeNames = _selectedComponentTypes.Select(t => t.AssemblyQualifiedName).ToList(),
                Timestamp = DateTime.Now
            };

            bool alreadyExists = _queryHistory.Any(h =>
                h.ComponentTypeNames.Count == historyEntry.ComponentTypeNames.Count &&
                h.ComponentTypeNames.SequenceEqual(historyEntry.ComponentTypeNames));

            if (!alreadyExists)
            {
                _queryHistory.Insert(0, historyEntry);
                if (_queryHistory.Count > MaxQueryHistory)
                {
                    _queryHistory.RemoveAt(_queryHistory.Count - 1);
                }
            }
        }

        /// <summary>
        /// Loads a saved or history query by resolving type names back to Types.
        /// </summary>
        private void LoadQuery(SavedQuery query)
        {
            _selectedComponentTypes.Clear();

            foreach (var typeName in query.ComponentTypeNames)
            {
                var type = Type.GetType(typeName);
                if (type != null)
                {
                    _selectedComponentTypes.Add(type);
                }
                else
                {
                    Debug.LogWarning($"[QueryTester] Could not resolve type: {GetShortTypeName(typeName)}");
                }
            }

            _queryResults.Clear();
            _expandedResults.Clear();
        }

        /// <summary>
        /// Caches all available IComponent value types from loaded assemblies.
        /// </summary>
        private void CacheComponentTypes()
        {
            if (_componentTypesCached && _availableComponentTypes.Count > 0) return;

            _availableComponentTypes.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (IsValidComponentType(type))
                        {
                            _availableComponentTypes.Add(type);
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that cannot be reflected
                }
            }

            _availableComponentTypes = _availableComponentTypes.OrderBy(t => t.Name).ToList();
            _filteredComponentTypes = new List<Type>(_availableComponentTypes);
            _componentTypesCached = true;
        }

        /// <summary>
        /// Determines whether a type is a valid ECS component: must be an unmanaged value type
        /// implementing IComponent.
        /// </summary>
        private static bool IsValidComponentType(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface)
                return false;

            if (!typeof(IComponent).IsAssignableFrom(type))
                return false;

            if (!type.IsValueType)
                return false;

            return IsUnmanagedType(type);
        }

        /// <summary>
        /// Recursively checks whether a type qualifies as unmanaged.
        /// </summary>
        private static bool IsUnmanagedType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type.IsPointer)
                return true;

            if (!type.IsValueType)
                return false;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!IsUnmanagedType(field.FieldType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Applies the current search filter to the available component types.
        /// </summary>
        private void ApplyComponentFilter()
        {
            if (string.IsNullOrEmpty(_componentSearchFilter))
            {
                _filteredComponentTypes = new List<Type>(_availableComponentTypes);
            }
            else
            {
                _filteredComponentTypes = _availableComponentTypes
                    .Where(t => t.Name.IndexOf(_componentSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (t.Namespace != null && t.Namespace.IndexOf(_componentSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
            }
        }

        /// <summary>
        /// Formats a component type name with its namespace for display.
        /// </summary>
        private static string FormatComponentTypeName(Type type)
        {
            if (string.IsNullOrEmpty(type.Namespace))
                return type.Name;

            return $"{type.Name}  ({type.Namespace})";
        }

        /// <summary>
        /// Formats a field value for display, handling common Unity and primitive types.
        /// </summary>
        private static string FormatFieldValue(object value)
        {
            if (value == null) return "(null)";

            var type = value.GetType();

            if (type == typeof(float)) return ((float)value).ToString("F4");
            if (type == typeof(double)) return ((double)value).ToString("F4");
            if (type == typeof(Vector2)) return ((Vector2)value).ToString("F3");
            if (type == typeof(Vector3)) return ((Vector3)value).ToString("F3");
            if (type == typeof(Vector4)) return ((Vector4)value).ToString("F3");
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return $"({q.x:F3}, {q.y:F3}, {q.z:F3}, {q.w:F3})";
            }
            if (type == typeof(Color)) return ((Color)value).ToString();

            return value.ToString();
        }

        /// <summary>
        /// Extracts a short type name from an assembly-qualified type name.
        /// </summary>
        private static string GetShortTypeName(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName)) return "Unknown";

            int commaIndex = assemblyQualifiedName.IndexOf(',');
            string fullName = commaIndex > 0 ? assemblyQualifiedName.Substring(0, commaIndex) : assemblyQualifiedName;

            int dotIndex = fullName.LastIndexOf('.');
            return dotIndex >= 0 ? fullName.Substring(dotIndex + 1) : fullName;
        }

        /// <summary>
        /// Represents a single entity matching a query, along with its component values.
        /// </summary>
        private class QueryResultEntry
        {
            public int EntityIndex;
            public Dictionary<Type, object> ComponentValues;
        }

        /// <summary>
        /// Represents a saved or historical query as a list of component type names.
        /// </summary>
        private class SavedQuery
        {
            public string Name;
            public List<string> ComponentTypeNames;
            public DateTime Timestamp;
        }
    }
}
