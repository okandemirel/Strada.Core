using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.ECS;
using StradaWorld = Strada.Core.Core.World;

namespace Strada.Core.Editor.Windows
{
    public class StradaEntityDebuggerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private Vector2 _detailScrollPosition;
        private string _searchFilter = "";
        private int _selectedEntityIndex = -1;
        private bool _showComponents = true;
        private bool _autoRefresh = true;
        private float _refreshInterval = 0.5f;
        private double _lastRefreshTime;

        private GUIStyle _headerStyle;
        private GUIStyle _entityStyle;
        private GUIStyle _selectedEntityStyle;
        private bool _stylesInitialized;

        private List<int> _entityIndices = new();

        // Menu item moved to StradaEditorMenus.cs
        public static void ShowWindow()
        {
            var window = GetWindow<StradaEntityDebuggerWindow>("Entity Debugger");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };

            _entityStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(10, 0, 2, 2)
            };

            _selectedEntityStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(10, 0, 2, 2),
                normal = { background = CreateColorTexture(new Color(0.2f, 0.4f, 0.8f, 0.3f)) }
            };

            _stylesInitialized = true;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Entity Debugger is only available in Play Mode.\n\nEnter Play Mode to inspect entities.",
                    MessageType.Info);
                return;
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawEntityList();
            DrawEntityDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            GUILayout.Space(10);

            _showComponents = GUILayout.Toggle(_showComponents, "Components", EditorStyles.toolbarButton, GUILayout.Width(80));
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            var entityManager = GetActiveEntityManager();
            if (entityManager != null)
            {
                GUILayout.Label($"Entities: {entityManager.EntityCount}", EditorStyles.toolbarButton);
                var componentCount = entityManager.Store?.GetComponentTypes()?.Count() ?? 0;
                GUILayout.Label($"Components: {componentCount}", EditorStyles.toolbarButton);
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshEntityList();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntityList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));

            EditorGUILayout.LabelField("Entities", _headerStyle);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var entityManager = GetActiveEntityManager();
            if (entityManager != null)
            {
                foreach (var entityIndex in _entityIndices)
                {
                    var style = entityIndex == _selectedEntityIndex ? _selectedEntityStyle : _entityStyle;

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button($"Entity [{entityIndex}]", style, GUILayout.ExpandWidth(true)))
                    {
                        _selectedEntityIndex = entityIndex;
                    }

                    var componentCount = entityManager.Store.GetEntityComponentCount(entityIndex);
                    GUILayout.Label($"[{componentCount}]", GUILayout.Width(30));

                    EditorGUILayout.EndHorizontal();
                }

                if (_entityIndices.Count == 0)
                {
                    EditorGUILayout.HelpBox("No entities found.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No active World found.\nCreate a World using World.Create().", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntityDetails()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("Entity Details", _headerStyle);

            if (_selectedEntityIndex < 0)
            {
                EditorGUILayout.HelpBox("Select an entity from the list to inspect its components.", MessageType.Info);
            }
            else
            {
                _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

                EditorGUILayout.LabelField($"Entity Index: {_selectedEntityIndex}", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);

                var entityManager = GetActiveEntityManager();
                if (entityManager != null && _showComponents)
                {
                    EditorGUILayout.LabelField("Components:", EditorStyles.boldLabel);

                    foreach (var componentType in entityManager.Store.GetComponentTypes())
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField(componentType.Name, EditorStyles.boldLabel);
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshEntityList()
        {
            _entityIndices.Clear();
            var entityManager = GetActiveEntityManager();

            if (entityManager != null)
            {
                foreach (var index in entityManager.GetAllEntities())
                {
                    if (string.IsNullOrEmpty(_searchFilter) || index.ToString().Contains(_searchFilter))
                    {
                        _entityIndices.Add(index);
                    }
                }
            }
        }

        private EntityManager GetActiveEntityManager()
        {
            return StradaWorld.Current?.EntityManager;
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying) return;

            if (_autoRefresh && EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                RefreshEntityList();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshEntityList();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _entityIndices.Clear();
                _selectedEntityIndex = -1;
            }
        }
    }
}
