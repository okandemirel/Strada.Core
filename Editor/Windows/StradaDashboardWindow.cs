using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.DI;
using Strada.Core.ECS.World;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using Strada.Core.Editor.Graph;
using Strada.Core.Editor.Profiling;
using UnityEditor;
using UnityEngine;
using EditorUpdatePhase = Strada.Core.Editor.DataProviders.Models.UpdatePhase;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Unified dashboard window aggregating all Strada runtime information.
    /// Provides tabbed interface with DI, ECS, Modules, Bus, and Performance tabs.
    /// Requirements: 1.1, 1.2, 1.4, 1.5
    /// </summary>
    public class StradaDashboardWindow : EditorWindow
    {
        private const int TabDI = 0;
        private const int TabECS = 1;
        private const int TabModules = 2;
        private const int TabBus = 3;
        private const int TabPerformance = 4;
        private const int TabArchitecture = 5;

        private readonly string[] _tabNames = { "DI Container", "ECS World", "Modules", "Bus Activity", "Performance", "Architecture" };

        private int _selectedTab;

        private bool _autoRefresh = true;
        private float _refreshInterval = 0.5f;
        private double _lastRefreshTime;

        private ContainerDataProvider _containerProvider;
        private WorldDataProvider _worldProvider;
        private ModuleDataProvider _moduleProvider;
        private BusDataProvider _busProvider;

        private DependencyGraphView _dependencyGraphView;
        private ModuleGraphView _moduleGraphView;

        private Vector2 _diScrollPosition;
        private Vector2 _ecsScrollPosition;
        private Vector2 _modulesScrollPosition;
        private Vector2 _busScrollPosition;
        private Vector2 _perfScrollPosition;

        private string _diSearchFilter = "";
        private Lifetime? _lifetimeFilter;
        private List<ServiceRegistrationInfo> _filteredRegistrations = new List<ServiceRegistrationInfo>();
        private ServiceNode _hoveredNode;

        private string _ecsSearchFilter = "";
        private List<int> _filteredEntityIds = new List<int>();
        private int _selectedEntityId = -1;
        private Vector2 _entityListScroll;
        private Vector2 _componentScroll;

        private string _moduleSearchFilter = "";
        private List<ModuleInfoData> _filteredModules = new List<ModuleInfoData>();
        private ModuleNode _hoveredModuleNode;

        private string _busTypeFilter = "";
        private MessageKind? _busKindFilter;
        private List<MessageLogEntry> _filteredMessages = new List<MessageLogEntry>();
        private int _selectedMessageIndex = -1;
        private Vector2 _messageListScroll;
        private Vector2 _messageDetailScroll;

        private SystemProfiler _profiler;
        private bool _isRecording;
        private float _warningThresholdMs = 1.0f;
        private float _criticalThresholdMs = 5.0f;

        private GUIStyle _headerStyle;
        private GUIStyle _statsBoxStyle;
        private GUIStyle _tabContentStyle;
        private bool _stylesInitialized;

        private readonly Color _singletonColor = new Color(0.4f, 0.8f, 0.4f);
        private readonly Color _transientColor = new Color(1.0f, 0.6f, 0.2f);
        private readonly Color _scopedColor = new Color(0.4f, 0.6f, 0.9f);
        private readonly Color _warningColor = new Color(1.0f, 0.85f, 0.4f);
        private readonly Color _criticalColor = new Color(1.0f, 0.4f, 0.4f);
        private readonly Color _normalColor = new Color(0.7f, 0.9f, 0.7f);

        public static void ShowWindow()
        {
            var window = GetWindow<StradaDashboardWindow>("Strada Dashboard");
            window.minSize = new Vector2(900, 600);
        }

        private void OnEnable()
        {
            _containerProvider = ContainerDataProvider.Instance;
            _worldProvider = WorldDataProvider.Instance;
            _moduleProvider = ModuleDataProvider.Instance;
            _busProvider = BusDataProvider.Instance;
            _profiler = new SystemProfiler();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            _containerProvider.OnDataChanged += OnContainerDataChanged;
            _worldProvider.OnDataChanged += OnWorldDataChanged;
            _moduleProvider.OnDataChanged += OnModuleDataChanged;
            _busProvider.OnDataChanged += OnBusDataChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_containerProvider != null)
                _containerProvider.OnDataChanged -= OnContainerDataChanged;
            if (_worldProvider != null)
                _worldProvider.OnDataChanged -= OnWorldDataChanged;
            if (_moduleProvider != null)
                _moduleProvider.OnDataChanged -= OnModuleDataChanged;
            if (_busProvider != null)
                _busProvider.OnDataChanged -= OnBusDataChanged;

            _profiler?.Dispose();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshAllData();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ClearAllData();
                _isRecording = false;
                _profiler?.StopRecording();
            }
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying || !_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                RefreshCurrentTab();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnContainerDataChanged() => Repaint();
        private void OnWorldDataChanged() => Repaint();
        private void OnModuleDataChanged() => Repaint();
        private void OnBusDataChanged() => Repaint();

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 10, 5)
            };

            _statsBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(5, 5, 5, 5)
            };

            _tabContentStyle = new GUIStyle()
            {
                padding = new RectOffset(5, 5, 5, 5)
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            DrawToolbar();
            DrawStatusBar();

            if (!Application.isPlaying)
            {
                DrawNotPlayingMessage();
                return;
            }

            var newTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                RefreshCurrentTab();
            }

            EditorGUILayout.Space(5);

            switch (_selectedTab)
            {
                case TabDI:
                    DrawDIContainerTab();
                    break;
                case TabECS:
                    DrawECSWorldTab();
                    break;
                case TabModules:
                    DrawModulesTab();
                    break;
                case TabBus:
                    DrawBusActivityTab();
                    break;
                case TabPerformance:
                    DrawPerformanceTab();
                    break;
                case TabArchitecture:
                    DrawArchitectureTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshAllData();
            }

            GUILayout.Space(10);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(85));

            if (_autoRefresh)
            {
                GUILayout.Label("Interval:", GUILayout.Width(50));
                _refreshInterval = EditorGUILayout.Slider(_refreshInterval, 0.1f, 2.0f, GUILayout.Width(100));
                GUILayout.Label("s", GUILayout.Width(15));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowSettingsMenu();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var playModeIcon = Application.isPlaying ? "●" : "○";
            var playModeColor = Application.isPlaying ? Color.green : Color.gray;
            var prevColor = GUI.contentColor;
            GUI.contentColor = playModeColor;
            GUILayout.Label(playModeIcon, GUILayout.Width(15));
            GUI.contentColor = prevColor;
            GUILayout.Label(Application.isPlaying ? "Play Mode" : "Edit Mode", GUILayout.Width(70));

            GUILayout.Space(20);

            if (Application.isPlaying)
            {
                DrawQuickStats();
            }

            GUILayout.FlexibleSpace();

            if (_lastRefreshTime > 0)
            {
                var elapsed = EditorApplication.timeSinceStartup - _lastRefreshTime;
                GUILayout.Label($"Last refresh: {elapsed:F1}s ago", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickStats()
        {
            if (_containerProvider.IsAvailable)
            {
                var snapshot = _containerProvider.GetData();
                if (snapshot != null)
                {
                    GUILayout.Label($"Services: {snapshot.RegistrationCount}", EditorStyles.miniLabel);
                    GUILayout.Space(10);
                }
            }

            if (_worldProvider.IsAvailable)
            {
                var snapshot = _worldProvider.GetData();
                if (snapshot != null)
                {
                    GUILayout.Label($"Entities: {snapshot.EntityCount}", EditorStyles.miniLabel);
                    GUILayout.Space(10);
                }
            }

            if (_moduleProvider.IsAvailable)
            {
                var snapshot = _moduleProvider.GetData();
                if (snapshot != null)
                {
                    GUILayout.Label($"Modules: {snapshot.ModuleCount}", EditorStyles.miniLabel);
                    GUILayout.Space(10);
                }
            }

            if (_busProvider.IsAvailable)
            {
                var entries = _busProvider.GetLogEntries();
                GUILayout.Label($"Messages: {entries.Count}", EditorStyles.miniLabel);
            }
        }

        private void DrawNotPlayingMessage()
        {
            GUILayout.Space(50);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(500));
            EditorGUILayout.HelpBox(
                "STRADA DASHBOARD\n\n" +
                "Unified view of the Strada Framework runtime state:\n\n" +
                "• DI Container - Service registrations and dependency graph\n" +
                "• ECS World - Entity inspection and component editing\n" +
                "• Modules - Module dependencies and initialization order\n" +
                "• Bus Activity - Message logging and debugging\n" +
                "• Performance - System profiling and benchmarks\n\n" +
                "Enter Play Mode to view runtime data.",
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

        private void ShowSettingsMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reset Layout"), false, ResetLayout);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open DI Graph Window"), false, () => DependencyGraphWindow.ShowWindow());
            menu.AddItem(new GUIContent("Open Module Graph Window"), false, () => ModuleGraphWindow.ShowWindow());
            menu.AddItem(new GUIContent("Open Entity Inspector"), false, () => StradaEntityInspectorWindow.ShowWindow());
            menu.AddItem(new GUIContent("Open Bus Debugger"), false, () => BusDebuggerWindow.ShowWindow());
            menu.AddItem(new GUIContent("Open System Profiler"), false, () => SystemProfilerWindow.ShowWindow());
            menu.ShowAsContext();
        }

        private void ResetLayout()
        {
            _selectedTab = 0;
            _diSearchFilter = "";
            _ecsSearchFilter = "";
            _moduleSearchFilter = "";
            _busTypeFilter = "";
            _selectedEntityId = -1;
            _selectedMessageIndex = -1;
            RefreshAllData();
        }

        private void DrawDIContainerTab()
        {
            if (!_containerProvider.IsAvailable)
            {
                EditorGUILayout.HelpBox("DI Container not available. Ensure GameBootstrapper is initialized.", MessageType.Warning);
                return;
            }

            DrawDIStatsPanel();

            DrawDIFilterBar();

            EditorGUILayout.BeginHorizontal();

            DrawDIRegistrationList();

            DrawDIQuickInfo();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDIStatsPanel()
        {
            var snapshot = _containerProvider.GetData();
            if (snapshot == null) return;

            EditorGUILayout.BeginHorizontal(_statsBoxStyle);

            GUILayout.Label("Registrations:", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.Label($"{snapshot.RegistrationCount}", GUILayout.Width(40));

            GUILayout.Space(20);

            var prevColor = GUI.contentColor;

            GUI.contentColor = _singletonColor;
            GUILayout.Label("Singleton:", GUILayout.Width(60));
            GUILayout.Label($"{snapshot.SingletonCount}", GUILayout.Width(30));

            GUI.contentColor = _transientColor;
            GUILayout.Label("Transient:", GUILayout.Width(60));
            GUILayout.Label($"{snapshot.TransientCount}", GUILayout.Width(30));

            GUI.contentColor = _scopedColor;
            GUILayout.Label("Scoped:", GUILayout.Width(50));
            GUILayout.Label($"{snapshot.ScopedCount}", GUILayout.Width(30));

            GUI.contentColor = prevColor;

            GUILayout.FlexibleSpace();

            if (_containerProvider.HasCircularDependency(out var cycle))
            {
                GUI.contentColor = _criticalColor;
                GUILayout.Label("⚠ Circular Dependency Detected!", EditorStyles.boldLabel);
                GUI.contentColor = prevColor;
            }

            if (GUILayout.Button("Open Graph", GUILayout.Width(80)))
            {
                DependencyGraphWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDIFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(45));
            var newSearch = EditorGUILayout.TextField(_diSearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (newSearch != _diSearchFilter)
            {
                _diSearchFilter = newSearch;
                RefreshDIRegistrations();
            }

            GUILayout.Space(10);

            GUILayout.Label("Lifetime:", GUILayout.Width(55));
            var lifetimeOptions = new[] { "All", "Singleton", "Transient", "Scoped" };
            var currentIndex = _lifetimeFilter.HasValue ? (int)_lifetimeFilter.Value + 1 : 0;
            var newIndex = EditorGUILayout.Popup(currentIndex, lifetimeOptions, EditorStyles.toolbarPopup, GUILayout.Width(80));

            var newFilter = newIndex == 0 ? (Lifetime?)null : (Lifetime)(newIndex - 1);
            if (newFilter != _lifetimeFilter)
            {
                _lifetimeFilter = newFilter;
                RefreshDIRegistrations();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Showing: {_filteredRegistrations.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDIRegistrationList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));

            EditorGUILayout.LabelField("Service Registrations", _headerStyle);

            _diScrollPosition = EditorGUILayout.BeginScrollView(_diScrollPosition);

            if (_filteredRegistrations.Count == 0)
            {
                EditorGUILayout.HelpBox("No registrations match the filter criteria.", MessageType.Info);
            }
            else
            {
                foreach (var reg in _filteredRegistrations)
                {
                    DrawRegistrationItem(reg);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRegistrationItem(ServiceRegistrationInfo reg)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var lifetimeColor = GetLifetimeColor(reg.Lifetime);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = lifetimeColor;
            GUILayout.Label(reg.Lifetime.ToString().Substring(0, 1), EditorStyles.miniButton, GUILayout.Width(20));
            GUI.backgroundColor = prevBg;

            var typeName = reg.ServiceType.Name;
            if (GUILayout.Button(typeName, EditorStyles.label, GUILayout.Width(180)))
            {
                NavigateToSource(reg.ServiceType);
            }

            if (reg.ImplementationType != reg.ServiceType)
            {
                GUILayout.Label("→", GUILayout.Width(20));
                GUILayout.Label(reg.ImplementationType.Name, EditorStyles.miniLabel, GUILayout.Width(150));
            }

            GUILayout.FlexibleSpace();

            if (reg.HasInstance)
            {
                GUILayout.Label("●", GUILayout.Width(15));
            }

            if (reg.Dependencies.Length > 0)
            {
                GUILayout.Label($"[{reg.Dependencies.Length}]", EditorStyles.miniLabel, GUILayout.Width(25));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDIQuickInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Quick Info", _headerStyle);

            EditorGUILayout.HelpBox(
                "Click on a service to navigate to its source.\n\n" +
                "Legend:\n" +
                "S = Singleton (green)\n" +
                "T = Transient (orange)\n" +
                "C = Scoped (blue)\n" +
                "● = Has instance\n" +
                "[n] = Dependency count",
                MessageType.None);

            GUILayout.Space(10);

            if (GUILayout.Button("View Full Dependency Graph"))
            {
                DependencyGraphWindow.ShowWindow();
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetLifetimeColor(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => _singletonColor,
                Lifetime.Transient => _transientColor,
                Lifetime.Scoped => _scopedColor,
                _ => Color.gray
            };
        }

        private void RefreshDIRegistrations()
        {
            _filteredRegistrations.Clear();
            var registrations = _containerProvider.GetRegistrations();

            foreach (var reg in registrations)
            {
                if (_lifetimeFilter.HasValue && reg.Lifetime != _lifetimeFilter.Value)
                    continue;

                if (!string.IsNullOrEmpty(_diSearchFilter))
                {
                    var searchLower = _diSearchFilter.ToLowerInvariant();
                    var typeName = reg.ServiceType.Name.ToLowerInvariant();
                    var implName = reg.ImplementationType.Name.ToLowerInvariant();

                    if (!typeName.Contains(searchLower) && !implName.Contains(searchLower))
                        continue;
                }

                _filteredRegistrations.Add(reg);
            }
        }

        private void DrawECSWorldTab()
        {
            if (!_worldProvider.IsAvailable)
            {
                EditorGUILayout.HelpBox("ECS World not available. Create a World using World.Create().", MessageType.Warning);
                return;
            }

            DrawECSStatsPanel();

            DrawECSFilterBar();

            EditorGUILayout.BeginHorizontal();

            DrawEntityList();

            DrawComponentDetails();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawECSStatsPanel()
        {
            var snapshot = _worldProvider.GetData();
            if (snapshot == null) return;

            EditorGUILayout.BeginHorizontal(_statsBoxStyle);

            GUILayout.Label("Entities:", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label($"{snapshot.EntityCount}", GUILayout.Width(50));

            GUILayout.Space(20);

            GUILayout.Label("Component Types:", EditorStyles.boldLabel, GUILayout.Width(110));
            GUILayout.Label($"{snapshot.ComponentTypeCount}", GUILayout.Width(40));

            GUILayout.Space(20);

            GUILayout.Label("Systems:", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label($"{snapshot.SystemCount}", GUILayout.Width(40));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Full Inspector", GUILayout.Width(120)))
            {
                StradaEntityInspectorWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawECSFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(45));
            var newSearch = EditorGUILayout.TextField(_ecsSearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (newSearch != _ecsSearchFilter)
            {
                _ecsSearchFilter = newSearch;
                RefreshEntityList();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Showing: {_filteredEntityIds.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntityList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            EditorGUILayout.LabelField("Entities", _headerStyle);

            _entityListScroll = EditorGUILayout.BeginScrollView(_entityListScroll);

            if (_filteredEntityIds.Count == 0)
            {
                EditorGUILayout.HelpBox("No entities in the world.", MessageType.Info);
            }
            else
            {
                foreach (var entityId in _filteredEntityIds.Take(100))
                {
                    var isSelected = entityId == _selectedEntityId;
                    var style = isSelected ? EditorStyles.selectionRect : EditorStyles.label;

                    EditorGUILayout.BeginHorizontal(style);

                    if (GUILayout.Button($"Entity [{entityId}]", EditorStyles.label))
                    {
                        _selectedEntityId = entityId;
                    }

                    var componentCount = GetEntityComponentCount(entityId);
                    GUILayout.Label($"[{componentCount}]", EditorStyles.miniLabel, GUILayout.Width(30));

                    EditorGUILayout.EndHorizontal();
                }

                if (_filteredEntityIds.Count > 100)
                {
                    EditorGUILayout.HelpBox($"Showing 100 of {_filteredEntityIds.Count} entities.", MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentDetails()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Components", _headerStyle);

            if (_selectedEntityId < 0)
            {
                EditorGUILayout.HelpBox("Select an entity to view its components.", MessageType.Info);
            }
            else if (!_worldProvider.EntityExists(_selectedEntityId))
            {
                EditorGUILayout.HelpBox("Selected entity has been destroyed.", MessageType.Warning);
                _selectedEntityId = -1;
            }
            else
            {
                _componentScroll = EditorGUILayout.BeginScrollView(_componentScroll);

                var components = _worldProvider.GetEntityComponents(_selectedEntityId).ToList();

                if (components.Count == 0)
                {
                    EditorGUILayout.HelpBox("Entity has no components.", MessageType.Info);
                }
                else
                {
                    foreach (var component in components)
                    {
                        DrawComponentSummary(component);
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentSummary(ComponentInfo component)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(component.ComponentType.Name, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            foreach (var field in component.Fields.Take(5)) // Show first 5 fields
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(field.Name, GUILayout.Width(100));
                GUILayout.Label(field.Value?.ToString() ?? "null", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (component.Fields.Count > 5)
            {
                EditorGUILayout.LabelField($"... and {component.Fields.Count - 5} more fields", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void RefreshEntityList()
        {
            _filteredEntityIds.Clear();
            var entityIds = _worldProvider.GetEntityIds().ToList();

            if (string.IsNullOrEmpty(_ecsSearchFilter))
            {
                _filteredEntityIds.AddRange(entityIds);
            }
            else
            {
                foreach (var id in entityIds)
                {
                    if (id.ToString().Contains(_ecsSearchFilter))
                    {
                        _filteredEntityIds.Add(id);
                    }
                    else
                    {
                        var components = _worldProvider.GetEntityComponents(id);
                        if (components.Any(c => c.ComponentType.Name.IndexOf(_ecsSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            _filteredEntityIds.Add(id);
                        }
                    }
                }
            }

            if (_selectedEntityId >= 0 && !_filteredEntityIds.Contains(_selectedEntityId))
            {
                _selectedEntityId = -1;
            }
        }

        private int GetEntityComponentCount(int entityId)
        {
            if (!_worldProvider.IsAvailable) return 0;
            return World.Current?.EntityManager?.Store?.GetEntityComponentCount(entityId) ?? 0;
        }

        private void DrawModulesTab()
        {
            if (!_moduleProvider.IsAvailable)
            {
                EditorGUILayout.HelpBox("Module registry not available. Ensure GameBootstrapper is initialized.", MessageType.Warning);
                return;
            }

            DrawModulesStatsPanel();

            DrawModulesFilterBar();

            EditorGUILayout.BeginHorizontal();

            DrawModuleList();

            DrawModuleDetails();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModulesStatsPanel()
        {
            var snapshot = _moduleProvider.GetData();
            if (snapshot == null) return;

            EditorGUILayout.BeginHorizontal(_statsBoxStyle);

            GUILayout.Label("Modules:", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label($"{snapshot.ModuleCount}", GUILayout.Width(40));

            GUILayout.FlexibleSpace();

            if (snapshot.HasCircularDependency)
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = _criticalColor;
                GUILayout.Label("⚠ Circular Dependency Detected!", EditorStyles.boldLabel);
                GUI.contentColor = prevColor;
            }

            if (GUILayout.Button("Validate All", GUILayout.Width(80)))
            {
                ValidateModules();
            }

            if (GUILayout.Button("Open Graph", GUILayout.Width(80)))
            {
                ModuleGraphWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModulesFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(45));
            var newSearch = EditorGUILayout.TextField(_moduleSearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (newSearch != _moduleSearchFilter)
            {
                _moduleSearchFilter = newSearch;
                RefreshModuleList();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Showing: {_filteredModules.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));

            EditorGUILayout.LabelField("Registered Modules", _headerStyle);

            _modulesScrollPosition = EditorGUILayout.BeginScrollView(_modulesScrollPosition);

            if (_filteredModules.Count == 0)
            {
                EditorGUILayout.HelpBox("No modules registered.", MessageType.Info);
            }
            else
            {
                foreach (var module in _filteredModules.OrderBy(m => m.Priority))
                {
                    DrawModuleItem(module);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleItem(ModuleInfoData module)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label($"[{module.Priority}]", EditorStyles.miniLabel, GUILayout.Width(35));

            GUILayout.Label(module.Name, EditorStyles.boldLabel, GUILayout.Width(180));

            if (module.Dependencies.Count > 0)
            {
                GUILayout.Label($"Deps: {module.Dependencies.Count}", EditorStyles.miniLabel, GUILayout.Width(60));
            }

            GUILayout.FlexibleSpace();

            if (module.IsInitialized)
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = _singletonColor;
                GUILayout.Label("✓", GUILayout.Width(20));
                GUI.contentColor = prevColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleDetails()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Initialization Order", _headerStyle);

            var modules = _moduleProvider.GetModules();
            var sortedModules = modules.OrderBy(m => m.Priority).ToList();

            for (int i = 0; i < sortedModules.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"#{i + 1}", EditorStyles.miniLabel, GUILayout.Width(25));
                GUILayout.Label(sortedModules[i].Name);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Validation Status", _headerStyle);

            var validation = _moduleProvider.ValidateModules();
            if (validation.IsValid)
            {
                EditorGUILayout.HelpBox("All modules validated successfully.", MessageType.Info);
            }
            else
            {
                foreach (var issue in validation.Issues)
                {
                    var msgType = issue.Severity == ValidationSeverity.Error ? MessageType.Error :
                                  issue.Severity == ValidationSeverity.Warning ? MessageType.Warning :
                                  MessageType.Info;
                    EditorGUILayout.HelpBox(issue.Message, msgType);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshModuleList()
        {
            _filteredModules.Clear();
            var modules = _moduleProvider.GetModules();

            foreach (var module in modules)
            {
                if (!string.IsNullOrEmpty(_moduleSearchFilter))
                {
                    var searchLower = _moduleSearchFilter.ToLowerInvariant();
                    if (!module.Name.ToLowerInvariant().Contains(searchLower))
                        continue;
                }

                _filteredModules.Add(module);
            }
        }

        private void ValidateModules()
        {
            var result = _moduleProvider.ValidateModules();
            if (result.IsValid)
            {
                EditorUtility.DisplayDialog("Validation Passed", "All modules validated successfully.", "OK");
            }
            else
            {
                var message = string.Join("\n", result.Issues.Select(i => $"[{i.Severity}] {i.Message}"));
                EditorUtility.DisplayDialog("Validation Failed", message, "OK");
            }
        }

        private void DrawBusActivityTab()
        {
            if (!_busProvider.IsAvailable)
            {
                EditorGUILayout.HelpBox("MessageBus not available. Ensure a World with a Bus is created.", MessageType.Warning);
                return;
            }

            DrawBusControlsPanel();

            DrawBusFilterBar();

            EditorGUILayout.BeginHorizontal();

            DrawMessageList();

            DrawMessageDetails();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBusControlsPanel()
        {
            EditorGUILayout.BeginHorizontal(_statsBoxStyle);

            var isLogging = _busProvider.IsLogging;
            var logIcon = isLogging ? "●" : "○";
            var logColor = isLogging ? Color.green : Color.gray;
            var prevColor = GUI.contentColor;
            GUI.contentColor = logColor;

            if (GUILayout.Button($"{logIcon} Log", GUILayout.Width(50)))
            {
                if (isLogging)
                    _busProvider.StopLogging();
                else
                    _busProvider.StartLogging();
            }

            GUI.contentColor = prevColor;

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _busProvider.ClearLog();
                _filteredMessages.Clear();
                _selectedMessageIndex = -1;
            }

            GUILayout.Space(20);

            var entries = _busProvider.GetLogEntries();
            GUILayout.Label($"Total: {entries.Count}", EditorStyles.miniLabel);

            var events = entries.Count(e => e.Kind == MessageKind.Event);
            var commands = entries.Count(e => e.Kind == MessageKind.Command);
            var queries = entries.Count(e => e.Kind == MessageKind.Query);

            GUILayout.Label($"Events: {events}", EditorStyles.miniLabel);
            GUILayout.Label($"Commands: {commands}", EditorStyles.miniLabel);
            GUILayout.Label($"Queries: {queries}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Full Debugger", GUILayout.Width(120)))
            {
                BusDebuggerWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBusFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Type:", GUILayout.Width(35));
            var newTypeFilter = EditorGUILayout.TextField(_busTypeFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
            if (newTypeFilter != _busTypeFilter)
            {
                _busTypeFilter = newTypeFilter;
                RefreshMessageList();
            }

            GUILayout.Space(10);

            GUILayout.Label("Kind:", GUILayout.Width(35));
            var kindOptions = new[] { "All", "Event", "Command", "Query" };
            var currentIndex = _busKindFilter.HasValue ? (int)_busKindFilter.Value + 1 : 0;
            var newIndex = EditorGUILayout.Popup(currentIndex, kindOptions, EditorStyles.toolbarPopup, GUILayout.Width(80));

            var newKindFilter = newIndex == 0 ? (MessageKind?)null : (MessageKind)(newIndex - 1);
            if (newKindFilter != _busKindFilter)
            {
                _busKindFilter = newKindFilter;
                RefreshMessageList();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Showing: {_filteredMessages.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMessageList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));

            EditorGUILayout.LabelField("Message Log", _headerStyle);

            _messageListScroll = EditorGUILayout.BeginScrollView(_messageListScroll);

            if (_filteredMessages.Count == 0)
            {
                if (_busProvider.IsLogging)
                {
                    EditorGUILayout.HelpBox("Waiting for messages...", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Logging is disabled. Click 'Log' to start.", MessageType.Info);
                }
            }
            else
            {
                for (int i = 0; i < Math.Min(_filteredMessages.Count, 100); i++)
                {
                    DrawMessageListItem(i, _filteredMessages[i]);
                }

                if (_filteredMessages.Count > 100)
                {
                    EditorGUILayout.HelpBox($"Showing 100 of {_filteredMessages.Count} messages.", MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawMessageListItem(int index, MessageLogEntry entry)
        {
            var isSelected = index == _selectedMessageIndex;
            var style = isSelected ? EditorStyles.selectionRect : EditorStyles.label;

            EditorGUILayout.BeginHorizontal(style);

            if (entry.Kind == MessageKind.Command && !entry.HasHandler)
            {
                var prevColor = GUI.contentColor;
                GUI.contentColor = _warningColor;
                GUILayout.Label("⚠", GUILayout.Width(18));
                GUI.contentColor = prevColor;
            }
            else
            {
                GUILayout.Space(18);
            }

            GUILayout.Label(entry.Timestamp.ToString("HH:mm:ss"), EditorStyles.miniLabel, GUILayout.Width(60));

            var kindColor = GetMessageKindColor(entry.Kind);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = kindColor;
            GUILayout.Label(GetMessageKindLabel(entry.Kind), EditorStyles.miniButton, GUILayout.Width(35));
            GUI.backgroundColor = prevBg;

            if (GUILayout.Button(entry.MessageType?.Name ?? "Unknown", EditorStyles.label))
            {
                _selectedMessageIndex = index;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMessageDetails()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Message Details", _headerStyle);

            if (_selectedMessageIndex < 0 || _selectedMessageIndex >= _filteredMessages.Count)
            {
                EditorGUILayout.HelpBox("Select a message to view details.", MessageType.Info);
            }
            else
            {
                var entry = _filteredMessages[_selectedMessageIndex];

                _messageDetailScroll = EditorGUILayout.BeginScrollView(_messageDetailScroll);

                EditorGUILayout.LabelField("Type:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(entry.MessageType?.FullName ?? "Unknown", EditorStyles.textField, GUILayout.Height(18));

                EditorGUILayout.LabelField("Kind:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.Kind.ToString());

                EditorGUILayout.LabelField("Timestamp:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                if (entry.Kind == MessageKind.Event)
                {
                    EditorGUILayout.LabelField("Subscribers:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(entry.SubscriberCount.ToString());
                }

                if (entry.Kind == MessageKind.Command)
                {
                    EditorGUILayout.LabelField("Has Handler:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(entry.HasHandler ? "Yes" : "No - Command will not be processed!");
                }

                GUILayout.Space(10);

                EditorGUILayout.LabelField("Payload:", EditorStyles.boldLabel);
                if (entry.Payload != null)
                {
                    DrawPayloadSummary(entry.Payload);
                }
                else
                {
                    EditorGUILayout.LabelField("(null)");
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPayloadSummary(object payload)
        {
            var type = payload.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields.Take(10))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(field.Name, GUILayout.Width(100));
                GUILayout.Label(field.GetValue(payload)?.ToString() ?? "null", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (fields.Length > 10)
            {
                EditorGUILayout.LabelField($"... and {fields.Length - 10} more fields", EditorStyles.miniLabel);
            }
        }

        private Color GetMessageKindColor(MessageKind kind)
        {
            return kind switch
            {
                MessageKind.Event => new Color(0.4f, 0.7f, 0.4f),
                MessageKind.Command => new Color(0.5f, 0.6f, 0.9f),
                MessageKind.Query => new Color(0.9f, 0.7f, 0.4f),
                _ => Color.gray
            };
        }

        private string GetMessageKindLabel(MessageKind kind)
        {
            return kind switch
            {
                MessageKind.Event => "EVT",
                MessageKind.Command => "CMD",
                MessageKind.Query => "QRY",
                _ => "???"
            };
        }

        private void RefreshMessageList()
        {
            var filter = new MessageFilter
            {
                TypePattern = string.IsNullOrEmpty(_busTypeFilter) ? null : _busTypeFilter,
                Kind = _busKindFilter,
                MaxResults = 1000
            };

            _filteredMessages = _busProvider.GetLogEntries(filter).ToList();

            if (_selectedMessageIndex >= _filteredMessages.Count)
            {
                _selectedMessageIndex = -1;
            }
        }

        private void DrawPerformanceTab()
        {
            DrawPerformanceControlsPanel();

            EditorGUILayout.BeginHorizontal();

            DrawSystemProfilerPanel();

            DrawPerformanceStatsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPerformanceControlsPanel()
        {
            EditorGUILayout.BeginHorizontal(_statsBoxStyle);

            var recordIcon = _isRecording ? "●" : "○";
            var recordColor = _isRecording ? Color.red : Color.gray;
            var prevColor = GUI.contentColor;
            GUI.contentColor = recordColor;

            if (GUILayout.Button($"{recordIcon} Record", GUILayout.Width(70)))
            {
                if (_isRecording)
                {
                    _profiler.StopRecording();
                    _isRecording = false;
                }
                else
                {
                    _profiler.StartRecording();
                    _isRecording = true;
                }
            }

            GUI.contentColor = prevColor;

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _profiler.Clear();
            }

            GUILayout.Space(20);

            GUILayout.Label("Warning:", GUILayout.Width(55));
            _warningThresholdMs = EditorGUILayout.FloatField(_warningThresholdMs, GUILayout.Width(40));
            GUILayout.Label("ms", GUILayout.Width(20));

            GUILayout.Space(10);

            GUILayout.Label("Critical:", GUILayout.Width(50));
            _criticalThresholdMs = EditorGUILayout.FloatField(_criticalThresholdMs, GUILayout.Width(40));
            GUILayout.Label("ms", GUILayout.Width(20));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Full Profiler", GUILayout.Width(120)))
            {
                SystemProfilerWindow.ShowWindow();
            }

            if (GUILayout.Button("Open Benchmarks", GUILayout.Width(110)))
            {
                BenchmarkRunnerWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemProfilerPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));

            EditorGUILayout.LabelField("System Execution Times", _headerStyle);

            _perfScrollPosition = EditorGUILayout.BeginScrollView(_perfScrollPosition);

            var metricsByPhase = _profiler.GetMetricsByPhase();
            var hasData = false;

            foreach (EditorUpdatePhase phase in Enum.GetValues(typeof(EditorUpdatePhase)))
            {
                var phaseMetrics = metricsByPhase[phase];
                if (phaseMetrics.Count == 0) continue;

                hasData = true;

                EditorGUILayout.LabelField(phase.ToString(), EditorStyles.boldLabel);

                foreach (var metrics in phaseMetrics.OrderByDescending(m => m.LastExecutionMs).Take(10))
                {
                    DrawSystemMetricsRow(metrics);
                }

                GUILayout.Space(5);
            }

            if (!hasData)
            {
                if (_isRecording)
                {
                    EditorGUILayout.HelpBox("Recording... Waiting for system execution data.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Click 'Record' to start capturing system execution times.", MessageType.Info);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSystemMetricsRow(SystemMetrics metrics)
        {
            var thresholdColor = GetThresholdColor(metrics.LastExecutionMs);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(metrics.SystemType.Name, GUILayout.Width(180));

            var barRect = GUILayoutUtility.GetRect(100, 16);
            DrawTimingBar(barRect, metrics.LastExecutionMs);

            var prevColor = GUI.contentColor;
            GUI.contentColor = thresholdColor;
            GUILayout.Label($"{metrics.LastExecutionMs:F3} ms", GUILayout.Width(70));
            GUI.contentColor = prevColor;

            GUILayout.Label($"({metrics.SampleCount})", EditorStyles.miniLabel, GUILayout.Width(40));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimingBar(Rect rect, double executionTimeMs)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            float maxMs = _criticalThresholdMs * 2;
            float ratio = Mathf.Clamp01((float)(executionTimeMs / maxMs));

            var barRect = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
            EditorGUI.DrawRect(barRect, GetThresholdColor(executionTimeMs));

            float warningX = rect.x + rect.width * (_warningThresholdMs / maxMs);
            float criticalX = rect.x + rect.width * (_criticalThresholdMs / maxMs);

            EditorGUI.DrawRect(new Rect(warningX, rect.y, 1, rect.height), _warningColor);
            EditorGUI.DrawRect(new Rect(criticalX, rect.y, 1, rect.height), _criticalColor);
        }

        private void DrawPerformanceStatsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Memory Stats", _headerStyle);

            var totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            var reservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            var unusedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Allocated:", GUILayout.Width(80));
            GUILayout.Label(FormatBytes(totalMemory));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Reserved:", GUILayout.Width(80));
            GUILayout.Label(FormatBytes(reservedMemory));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Unused:", GUILayout.Width(80));
            GUILayout.Label(FormatBytes(unusedMemory));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Legend", _headerStyle);

            EditorGUILayout.BeginHorizontal();
            var rect = GUILayoutUtility.GetRect(15, 15);
            EditorGUI.DrawRect(rect, _normalColor);
            GUILayout.Label($"Normal (< {_warningThresholdMs}ms)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            rect = GUILayoutUtility.GetRect(15, 15);
            EditorGUI.DrawRect(rect, _warningColor);
            GUILayout.Label($"Warning ({_warningThresholdMs}-{_criticalThresholdMs}ms)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            rect = GUILayoutUtility.GetRect(15, 15);
            EditorGUI.DrawRect(rect, _criticalColor);
            GUILayout.Label($"Critical (> {_criticalThresholdMs}ms)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private Color GetThresholdColor(double executionTimeMs)
        {
            if (executionTimeMs >= _criticalThresholdMs) return _criticalColor;
            if (executionTimeMs >= _warningThresholdMs) return _warningColor;
            return _normalColor;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void RefreshAllData()
        {
            _containerProvider?.Refresh();
            _worldProvider?.Refresh();
            _moduleProvider?.Refresh();
            _busProvider?.Refresh();

            RefreshDIRegistrations();
            RefreshEntityList();
            RefreshModuleList();
            RefreshMessageList();

            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private void RefreshCurrentTab()
        {
            switch (_selectedTab)
            {
                case TabDI:
                    _containerProvider?.Refresh();
                    RefreshDIRegistrations();
                    break;
                case TabECS:
                    _worldProvider?.Refresh();
                    RefreshEntityList();
                    break;
                case TabModules:
                    _moduleProvider?.Refresh();
                    RefreshModuleList();
                    break;
                case TabBus:
                    _busProvider?.Refresh();
                    RefreshMessageList();
                    break;
                case TabPerformance:
                    break;
            }
        }

        private void ClearAllData()
        {
            _filteredRegistrations.Clear();
            _filteredEntityIds.Clear();
            _filteredModules.Clear();
            _filteredMessages.Clear();
            _selectedEntityId = -1;
            _selectedMessageIndex = -1;
            _hoveredNode = null;
            _hoveredModuleNode = null;
        }

        private void NavigateToSource(Type type)
        {
            var guids = AssetDatabase.FindAssets($"t:Script {type.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }

            var searchGuids = AssetDatabase.FindAssets(type.Name);
            foreach (var guid in searchGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cs"))
                {
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
                    return;
                }
            }

            Debug.LogWarning($"[StradaDashboard] Could not find source file for type: {type.FullName}");
        }

        /// <summary>
        /// Highlights dependencies and dependents for a service node.
        /// Called when hovering over a node in the DI graph.
        /// Requirements: 1.4
        /// </summary>
        public void HighlightDependencies(ServiceNode node)
        {
            _hoveredNode = node;
            _dependencyGraphView?.HighlightNodeDependencies(node);
            Repaint();
        }

        /// <summary>
        /// Clears dependency highlighting.
        /// </summary>
        public void ClearDependencyHighlighting()
        {
            _hoveredNode = null;
            _dependencyGraphView?.ClearHighlights();
            Repaint();
        }

        /// <summary>
        /// Highlights dependencies and dependents for a module node.
        /// </summary>
        public void HighlightModuleDependencies(ModuleNode node)
        {
            _hoveredModuleNode = node;
            _moduleGraphView?.HighlightNodeDependencies(node);
            Repaint();
        }

        /// <summary>
        /// Clears module dependency highlighting.
        /// </summary>
        public void ClearModuleDependencyHighlighting()
        {
            _hoveredModuleNode = null;
            _moduleGraphView?.ClearHighlights();
            Repaint();
        }
        private void DrawArchitectureTab()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Architecture Graph", _headerStyle);
            
            if (GUILayout.Button("Open Full Graph Window", GUILayout.Height(30)))
            {
                ModuleGraphWindow.ShowWindow();
            }

            EditorGUILayout.HelpBox(
                "The Architecture Graph visualizes the dependencies between Modules.\n" +
                "This helps identify circular dependencies and understand the initialization order.\n\n" +
                "Click the button above to open the interactive graph view.", 
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Module Dependencies (Static Analysis)", EditorStyles.boldLabel);

            var modules = _moduleProvider.GetModules();
            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox("No modules found. Enter Play Mode to see runtime modules.", MessageType.Info);
            }
            else
            {
                foreach (var module in modules)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(module.Name, EditorStyles.boldLabel);
                    
                    if (module.Dependencies.Count > 0)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var dep in module.Dependencies)
                        {
                            EditorGUILayout.LabelField($"→ {dep}", EditorStyles.miniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No dependencies", EditorStyles.centeredGreyMiniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
