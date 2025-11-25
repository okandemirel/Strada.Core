using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using StradaWorld = Strada.Core.Core.World;

namespace Strada.Core.Editor.Windows
{
    public class StradaLiveInspectorWindow : EditorWindow
    {
        private Vector2 _entityListScroll;
        private Vector2 _componentScroll;
        private string _searchFilter = "";
        private int _selectedEntityId = -1;
        private bool _autoRefresh = true;
        private float _refreshRate = 0.1f;
        private double _lastRefresh;

        private List<EntityInfo> _entities = new();
        private Dictionary<Type, bool> _componentFoldouts = new();
        private Dictionary<Type, ComponentFieldCache> _fieldCaches = new();

        private GUIStyle _headerStyle;
        private GUIStyle _componentHeaderStyle;
        private GUIStyle _selectedStyle;
        private GUIStyle _fieldLabelStyle;
        private bool _stylesInit;

        private readonly Color _singletonColor = new(0.8f, 0.6f, 0.2f);
        private readonly Color _componentColor = new(0.3f, 0.6f, 0.9f);
        private readonly Color _valueTypeColor = new(0.6f, 0.8f, 0.4f);

        [MenuItem("Strada/Inspector/Live Inspector", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<StradaLiveInspectorWindow>("Live Inspector");
            window.minSize = new Vector2(900, 600);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _componentHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
            _selectedStyle = new GUIStyle("SelectionRect");
            _fieldLabelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };

            _stylesInit = true;
        }

        private void OnGUI()
        {
            InitStyles();

            if (!Application.isPlaying)
            {
                DrawPlayModeMessage();
                return;
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawEntityPanel();
            DrawComponentPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayModeMessage()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            EditorGUILayout.HelpBox(
                "STRADA LIVE INSPECTOR\n\n" +
                "Real-time entity and component inspection.\n" +
                "Enter Play Mode to begin debugging.",
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
            GUILayout.FlexibleSpace();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Filter:", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
                _searchFilter = "";

            GUILayout.Space(20);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Live", EditorStyles.toolbarButton, GUILayout.Width(50));

            GUILayout.Label("Rate:", GUILayout.Width(35));
            _refreshRate = EditorGUILayout.Slider(_refreshRate, 0.05f, 1f, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            var em = GetEntityManager();
            if (em != null)
            {
                DrawStatBadge("Entities", em.EntityCount.ToString(), _componentColor);
                var types = em.Store?.GetComponentTypes();
                DrawStatBadge("Types", types != null ? System.Linq.Enumerable.Count(types).ToString() : "0", _valueTypeColor);
            }

            if (GUILayout.Button("↻ Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                RefreshEntities();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatBadge(string label, string value, Color color)
        {
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label($"{label}: {value}", EditorStyles.toolbarButton, GUILayout.Width(90));
            GUI.backgroundColor = prevColor;
        }

        private void DrawEntityPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(280));

            EditorGUILayout.LabelField("Entities", _headerStyle);
            EditorGUILayout.Space(5);

            _entityListScroll = EditorGUILayout.BeginScrollView(_entityListScroll);

            foreach (var entity in _entities)
            {
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    bool match = entity.Id.ToString().Contains(_searchFilter) ||
                                 entity.ComponentNames.ToLower().Contains(_searchFilter.ToLower());
                    if (!match) continue;
                }

                DrawEntityRow(entity);
            }

            if (_entities.Count == 0)
            {
                EditorGUILayout.HelpBox("No entities in world.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntityRow(EntityInfo entity)
        {
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));

            if (entity.Id == _selectedEntityId)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 0.7f, 0.4f));
            }

            if (GUILayout.Button(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(24)))
            {
                _selectedEntityId = entity.Id;
            }

            var labelRect = new Rect(rect.x + 10, rect.y + 3, 80, 18);
            var badgeRect = new Rect(rect.x + 95, rect.y + 3, 30, 18);
            var nameRect = new Rect(rect.x + 130, rect.y + 3, rect.width - 140, 18);

            GUI.Label(labelRect, $"Entity {entity.Id}", EditorStyles.boldLabel);

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _componentColor;
            GUI.Label(badgeRect, entity.ComponentCount.ToString(), EditorStyles.miniButton);
            GUI.backgroundColor = prevColor;

            GUI.Label(nameRect, entity.ComponentNames, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawComponentPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Components", _headerStyle);
            EditorGUILayout.Space(5);

            if (_selectedEntityId < 0)
            {
                EditorGUILayout.HelpBox("Select an entity to inspect components.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _componentScroll = EditorGUILayout.BeginScrollView(_componentScroll);

            var em = GetEntityManager();
            if (em == null || !em.Exists(new Entity(_selectedEntityId, 0)))
            {
                EditorGUILayout.HelpBox("Entity no longer exists.", MessageType.Warning);
                _selectedEntityId = -1;
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"Entity {_selectedEntityId}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var componentTypes = em.Store.GetComponentTypes();
            if (componentTypes != null)
            {
                foreach (var type in componentTypes)
                {
                    if (em.Store.HasComponent(_selectedEntityId, type))
                    {
                        DrawComponentEditor(em, type);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentEditor(EntityManager em, Type componentType)
        {
            if (!_componentFoldouts.TryGetValue(componentType, out var foldout))
                foldout = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var headerRect = EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _componentColor;

            _componentFoldouts[componentType] = EditorGUILayout.Foldout(foldout, componentType.Name, true, _componentHeaderStyle);

            GUI.backgroundColor = prevColor;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                RemoveComponentRuntime(em, componentType);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (_componentFoldouts[componentType])
            {
                EditorGUI.indentLevel++;

                var cache = GetFieldCache(componentType);
                var component = em.Store.GetComponentBoxed(_selectedEntityId, componentType);

                if (component != null)
                {
                    EditorGUI.BeginChangeCheck();

                    foreach (var field in cache.Fields)
                    {
                        DrawField(field, component);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        em.Store.SetComponentBoxed(_selectedEntityId, componentType, component);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawField(FieldInfo field, object component)
        {
            var value = field.GetValue(component);
            var fieldType = field.FieldType;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(field.Name, _fieldLabelStyle, GUILayout.Width(120));

            object newValue = value;

            if (fieldType == typeof(int))
                newValue = EditorGUILayout.IntField((int)(value ?? 0));
            else if (fieldType == typeof(float))
                newValue = EditorGUILayout.FloatField((float)(value ?? 0f));
            else if (fieldType == typeof(bool))
                newValue = EditorGUILayout.Toggle((bool)(value ?? false));
            else if (fieldType == typeof(string))
                newValue = EditorGUILayout.TextField((string)(value ?? ""));
            else if (fieldType == typeof(Vector2))
                newValue = EditorGUILayout.Vector2Field(GUIContent.none, (Vector2)(value ?? Vector2.zero));
            else if (fieldType == typeof(Vector3))
                newValue = EditorGUILayout.Vector3Field(GUIContent.none, (Vector3)(value ?? Vector3.zero));
            else if (fieldType == typeof(Vector4))
                newValue = EditorGUILayout.Vector4Field(GUIContent.none, (Vector4)(value ?? Vector4.zero));
            else if (fieldType == typeof(Quaternion))
            {
                var q = (Quaternion)(value ?? Quaternion.identity);
                var euler = EditorGUILayout.Vector3Field(GUIContent.none, q.eulerAngles);
                newValue = Quaternion.Euler(euler);
            }
            else if (fieldType == typeof(Color))
                newValue = EditorGUILayout.ColorField((Color)(value ?? Color.white));
            else if (fieldType.IsEnum)
                newValue = EditorGUILayout.EnumPopup((Enum)value);
            else
                EditorGUILayout.LabelField(value?.ToString() ?? "null");

            EditorGUILayout.EndHorizontal();

            if (!Equals(value, newValue))
                field.SetValue(component, newValue);
        }

        private void RemoveComponentRuntime(EntityManager em, Type componentType)
        {
            var method = typeof(EntityManager).GetMethod("RemoveComponent");
            if (method != null)
            {
                var generic = method.MakeGenericMethod(componentType);
                generic.Invoke(em, new object[] { new Entity(_selectedEntityId, 0) });
            }
        }

        private ComponentFieldCache GetFieldCache(Type type)
        {
            if (_fieldCaches.TryGetValue(type, out var cache))
                return cache;

            cache = new ComponentFieldCache
            {
                Fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            };
            _fieldCaches[type] = cache;
            return cache;
        }

        private void RefreshEntities()
        {
            _entities.Clear();
            var em = GetEntityManager();
            if (em == null) return;

            foreach (var entityId in em.GetAllEntities())
            {
                var info = new EntityInfo { Id = entityId };
                var types = em.Store.GetComponentTypes();

                var names = new List<string>();
                int count = 0;

                if (types != null)
                {
                    foreach (var type in types)
                    {
                        if (em.Store.HasComponent(entityId, type))
                        {
                            names.Add(type.Name);
                            count++;
                        }
                    }
                }

                info.ComponentCount = count;
                info.ComponentNames = count > 0 ? string.Join(", ", names) : "(empty)";
                _entities.Add(info);
            }
        }

        private EntityManager GetEntityManager()
        {
            return StradaWorld.Current?.EntityManager;
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying) return;

            if (_autoRefresh && EditorApplication.timeSinceStartup - _lastRefresh > _refreshRate)
            {
                RefreshEntities();
                _lastRefresh = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                RefreshEntities();
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _entities.Clear();
                _selectedEntityId = -1;
            }
        }

        private struct EntityInfo
        {
            public int Id;
            public int ComponentCount;
            public string ComponentNames;
        }

        private class ComponentFieldCache
        {
            public FieldInfo[] Fields;
        }
    }
}
