using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for inspecting ECS worlds, entities, components, and systems at runtime.
    /// </summary>
    public class ECSWorldInspectorWindow : EditorWindow
    {
        private Vector2 _worldListScroll;
        private Vector2 _entityListScroll;
        private Vector2 _detailsScroll;

        private string _worldFilter = "";
        private string _entityFilter = "";
        private bool _autoRefresh = true;

        private List<WorldInfo> _worlds = new List<WorldInfo>();
        private WorldInfo _selectedWorld;
        private EntityInfo _selectedEntity;

        [MenuItem("Window/Strada/ECS World Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<ECSWorldInspectorWindow>("ECS Worlds");
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshWorlds();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_autoRefresh && Application.isPlaying)
            {
                RefreshWorlds();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!Application.isPlaying)
            {
                StradaEditorGUI.DrawHelpBox("Enter Play Mode to inspect ECS worlds.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            DrawWorldsList();
            DrawEntitiesList();
            DrawDetailsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshWorlds();
            }

            GUILayout.Space(10);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Worlds: {_worlds.Count}", EditorStyles.miniLabel);

            if (_selectedWorld != null)
            {
                GUILayout.Label($"Entities: {_selectedWorld.EntityCount}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWorldsList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            StradaEditorGUI.DrawSubHeader("Worlds", StradaEditorIcons.WorldIcon);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(40));
            _worldFilter = EditorGUILayout.TextField(_worldFilter);
            EditorGUILayout.EndHorizontal();

            _worldListScroll = EditorGUILayout.BeginScrollView(_worldListScroll);

            foreach (var world in GetFilteredWorlds())
            {
                DrawWorldItem(world);
            }

            if (_worlds.Count == 0)
            {
                GUILayout.Label("No ECS worlds found.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawWorldItem(WorldInfo world)
        {
            var isSelected = _selectedWorld == world;
            var backgroundColor = isSelected
                ? StradaEditorStyles.PrimaryColor
                : GUI.backgroundColor;

            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(StradaEditorIcons.WorldIcon, GUILayout.Width(16), GUILayout.Height(16));

            if (GUILayout.Button(world.Name, EditorStyles.label))
            {
                _selectedWorld = world;
                _selectedEntity = null;
                RefreshEntities();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"{world.EntityCount} entities", EditorStyles.miniLabel);
            GUILayout.Label($"{world.SystemCount} systems", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawEntitiesList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));

            StradaEditorGUI.DrawSubHeader("Entities", StradaEditorIcons.EntityIcon);

            if (_selectedWorld == null)
            {
                GUILayout.Label("Select a world to view entities.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(40));
            _entityFilter = EditorGUILayout.TextField(_entityFilter);
            EditorGUILayout.EndHorizontal();

            _entityListScroll = EditorGUILayout.BeginScrollView(_entityListScroll);

            foreach (var entity in GetFilteredEntities())
            {
                DrawEntityItem(entity);
            }

            if (_selectedWorld.Entities.Count == 0)
            {
                GUILayout.Label("No entities in this world.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntityItem(EntityInfo entity)
        {
            var isSelected = _selectedEntity == entity;
            var backgroundColor = isSelected
                ? StradaEditorStyles.PrimaryColor
                : GUI.backgroundColor;

            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(StradaEditorIcons.EntityIcon, GUILayout.Width(16), GUILayout.Height(16));

            if (GUILayout.Button($"Entity {entity.Index}", EditorStyles.label))
            {
                _selectedEntity = entity;
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"{entity.ComponentCount} components", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawDetailsPanel()
        {
            EditorGUILayout.BeginVertical();

            StradaEditorGUI.DrawSubHeader("Details", StradaEditorIcons.ViewIcon);

            if (_selectedEntity == null)
            {
                GUILayout.Label("Select an entity to view details.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            DrawEntityDetails(_selectedEntity);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntityDetails(EntityInfo entity)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            StradaEditorGUI.DrawLabelWithIcon($"Entity {entity.Index}", StradaEditorIcons.EntityIcon);
            StradaEditorGUI.Space();

            StradaEditorGUI.DrawReadOnlyProperty("Index", entity.Index.ToString());
            StradaEditorGUI.DrawReadOnlyProperty("Version", entity.Version.ToString());
            StradaEditorGUI.DrawReadOnlyProperty("Component Count", entity.ComponentCount.ToString());

            StradaEditorGUI.Space();
            StradaEditorGUI.DrawSubHeader("Components", StradaEditorIcons.ComponentIcon);

            foreach (var component in entity.Components)
            {
                GUILayout.Label($"• {component}", EditorStyles.miniLabel);
            }

            if (entity.Components.Count == 0)
            {
                GUILayout.Label("(no components)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshWorlds()
        {
            _worlds.Clear();

            if (!Application.isPlaying)
                return;

            foreach (var world in World.All)
            {
                var entityQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Entity>());
                var entityCount = entityQuery.CalculateEntityCount();

                var worldInfo = new WorldInfo
                {
                    Name = world.Name,
                    EntityCount = entityCount,
                    SystemCount = world.Systems.Count,
                    Entities = new List<EntityInfo>()
                };

                _worlds.Add(worldInfo);
            }
        }

        private void RefreshEntities()
        {
            if (_selectedWorld == null)
                return;

            _selectedWorld.Entities.Clear();

            var world = World.All.FirstOrDefault(w => w.Name == _selectedWorld.Name);
            if (world == null || !world.IsCreated)
                return;

            var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Entity>());
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var entityInfo = new EntityInfo
                {
                    Index = entity.Index,
                    Version = entity.Version,
                    ComponentCount = world.EntityManager.GetComponentCount(entity),
                    Components = new List<string>()
                };

                var types = world.EntityManager.GetComponentTypes(entity);
                foreach (var type in types)
                {
                    entityInfo.Components.Add(type.GetManagedType()?.Name ?? "Unknown");
                }
                types.Dispose();

                _selectedWorld.Entities.Add(entityInfo);
            }

            entities.Dispose();
        }

        private List<WorldInfo> GetFilteredWorlds()
        {
            if (string.IsNullOrEmpty(_worldFilter))
                return _worlds;

            return _worlds.Where(w =>
                w.Name.IndexOf(_worldFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private List<EntityInfo> GetFilteredEntities()
        {
            if (_selectedWorld == null)
                return new List<EntityInfo>();

            if (string.IsNullOrEmpty(_entityFilter))
                return _selectedWorld.Entities;

            return _selectedWorld.Entities.Where(e =>
                e.Components.Any(c => c.IndexOf(_entityFilter, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private class WorldInfo
        {
            public string Name { get; set; }
            public int EntityCount { get; set; }
            public int SystemCount { get; set; }
            public List<EntityInfo> Entities { get; set; }
        }

        private class EntityInfo
        {
            public int Index { get; set; }
            public int Version { get; set; }
            public int ComponentCount { get; set; }
            public List<string> Components { get; set; }
        }
    }
}
