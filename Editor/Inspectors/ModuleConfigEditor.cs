using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Strada.Core.Modules;

namespace Strada.Core.Editor.Inspectors
{
    /// <summary>
    /// Custom editor for ModuleConfig ScriptableObjects.
    /// Provides a Quantum 3-style interface with system discovery and reorderable lists.
    /// </summary>
    [CustomEditor(typeof(ModuleConfig), true)]
    public class ModuleConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _moduleNameProp;
        private SerializedProperty _priorityProp;
        private SerializedProperty _enabledProp;
        private SerializedProperty _systemsProp;
        private SerializedProperty _servicesProp;
        private SerializedProperty _dependenciesProp;

        private ReorderableList _systemsList;
        private ReorderableList _servicesList;
        private ReorderableList _dependenciesList;

        private bool _showSystems = true;
        private bool _showServices = true;
        private bool _showDependencies = true;

        private static readonly Color EnabledColor = new Color(0.3f, 0.8f, 0.3f, 1f);
        private static readonly Color DisabledColor = new Color(0.8f, 0.3f, 0.3f, 1f);

        private void OnEnable()
        {
            _moduleNameProp = serializedObject.FindProperty("_moduleName");
            _priorityProp = serializedObject.FindProperty("_priority");
            _enabledProp = serializedObject.FindProperty("_enabled");
            _systemsProp = serializedObject.FindProperty("_systems");
            _servicesProp = serializedObject.FindProperty("_services");
            _dependenciesProp = serializedObject.FindProperty("_dependencies");

            SetupSystemsList();
            SetupServicesList();
            SetupDependenciesList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBasicSettings();
            EditorGUILayout.Space(10);

            DrawSystemsSection();
            EditorGUILayout.Space(10);

            DrawServicesSection();
            EditorGUILayout.Space(10);

            DrawDependenciesSection();
            EditorGUILayout.Space(10);

            DrawCustomConfiguration();

            serializedObject.ApplyModifiedProperties();
        }

        private new void DrawHeader()
        {
            var moduleConfig = target as ModuleConfig;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var statusColor = moduleConfig.Enabled ? EnabledColor : DisabledColor;
            var previousColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Box("", GUILayout.Width(10), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            GUI.color = previousColor;

            EditorGUILayout.LabelField(moduleConfig.ModuleName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField($"Priority: {moduleConfig.Priority}", EditorStyles.miniLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBasicSettings()
        {
            EditorGUILayout.LabelField("Module Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_moduleNameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(_priorityProp, new GUIContent("Priority", "Lower values initialize first"));
            EditorGUILayout.PropertyField(_enabledProp, new GUIContent("Enabled"));

            EditorGUI.indentLevel--;
        }

        private void DrawSystemsSection()
        {
            EditorGUILayout.BeginHorizontal();
            _showSystems = EditorGUILayout.Foldout(_showSystems, $"Systems ({_systemsProp.arraySize})", true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Discover", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                DiscoverSystems();
            }

            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                _systemsProp.arraySize++;
            }

            EditorGUILayout.EndHorizontal();

            if (_showSystems)
            {
                _systemsList.DoLayoutList();
            }
        }

        private void DrawServicesSection()
        {
            EditorGUILayout.BeginHorizontal();
            _showServices = EditorGUILayout.Foldout(_showServices, $"Services ({_servicesProp.arraySize})", true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                _servicesProp.arraySize++;
            }

            EditorGUILayout.EndHorizontal();

            if (_showServices)
            {
                _servicesList.DoLayoutList();
            }
        }

        private void DrawDependenciesSection()
        {
            EditorGUILayout.BeginHorizontal();
            _showDependencies = EditorGUILayout.Foldout(_showDependencies, $"Dependencies ({_dependenciesProp.arraySize})", true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(25)))
            {
                _dependenciesProp.arraySize++;
            }

            EditorGUILayout.EndHorizontal();

            if (_showDependencies)
            {
                _dependenciesList.DoLayoutList();
            }
        }

        private void DrawCustomConfiguration()
        {
            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script reference

            var knownProperties = new HashSet<string>
            {
                "_moduleName", "_priority", "_enabled", "_systems", "_services", "_dependencies", "m_Script"
            };

            bool hasCustomProperties = false;
            while (iterator.NextVisible(false))
            {
                if (!knownProperties.Contains(iterator.name))
                {
                    if (!hasCustomProperties)
                    {
                        EditorGUILayout.LabelField("Custom Configuration", EditorStyles.boldLabel);
                        hasCustomProperties = true;
                    }
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
        }

        private void SetupSystemsList()
        {
            _systemsList = new ReorderableList(serializedObject, _systemsProp, true, true, false, true);

            _systemsList.drawHeaderCallback = rect =>
            {
                var col1 = new Rect(rect.x, rect.y, 18, rect.height);
                var col2 = new Rect(col1.xMax + 4, rect.y, rect.width - 180, rect.height);
                var col3 = new Rect(col2.xMax + 4, rect.y, 85, rect.height);
                var col4 = new Rect(col3.xMax + 4, rect.y, 40, rect.height);

                EditorGUI.LabelField(col1, "");
                EditorGUI.LabelField(col2, "System Type");
                EditorGUI.LabelField(col3, "Phase");
                EditorGUI.LabelField(col4, "Order");
            };

            _systemsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = _systemsProp.GetArrayElementAtIndex(index);
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            };

            _systemsList.elementHeightCallback = index =>
            {
                var element = _systemsProp.GetArrayElementAtIndex(index);
                var descProp = element.FindPropertyRelative("_description");
                bool hasDescription = !string.IsNullOrEmpty(descProp.stringValue);
                return EditorGUIUtility.singleLineHeight + (hasDescription ? EditorGUIUtility.singleLineHeight : 0) + 4;
            };
        }

        private void SetupServicesList()
        {
            _servicesList = new ReorderableList(serializedObject, _servicesProp, true, true, false, true);

            _servicesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Interface → Implementation [Lifetime]");
            };

            _servicesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = _servicesProp.GetArrayElementAtIndex(index);
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            };
        }

        private void SetupDependenciesList()
        {
            _dependenciesList = new ReorderableList(serializedObject, _dependenciesProp, true, true, false, true);

            _dependenciesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Module Dependencies");
            };

            _dependenciesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = _dependenciesProp.GetArrayElementAtIndex(index);
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            };
        }

        private void DiscoverSystems()
        {
            var moduleConfig = target as ModuleConfig;
            var moduleName = moduleConfig.ModuleName;

            RuntimeSystemDiscovery.Refresh();

            var discoveredSystems = RuntimeSystemDiscovery.DiscoverSystems(moduleName).ToList();

            if (discoveredSystems.Count == 0)
            {
                discoveredSystems = RuntimeSystemDiscovery.DiscoverSystems().ToList();
            }

            if (discoveredSystems.Count == 0)
            {
                EditorUtility.DisplayDialog("No Systems Found",
                    "No systems were discovered. Make sure your systems:\n\n" +
                    "1. Inherit from SystemBase\n" +
                    "2. Are marked with [StradaSystem] attribute\n" +
                    "3. Are in a loaded assembly",
                    "OK");
                return;
            }

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Add All Discovered Systems"), false, () =>
            {
                AddDiscoveredSystems(discoveredSystems);
            });

            menu.AddSeparator("");

            foreach (var system in discoveredSystems)
            {
                var systemCopy = system;
                var existing = HasSystem(system.Type);
                var menuLabel = existing ?
                    $"✓ {system.Type.Name} (already added)" :
                    system.Type.Name;

                if (!existing)
                {
                    menu.AddItem(new GUIContent(menuLabel), false, () =>
                    {
                        AddDiscoveredSystem(systemCopy);
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(menuLabel));
                }
            }

            menu.ShowAsContext();
        }

        private bool HasSystem(System.Type type)
        {
            for (int i = 0; i < _systemsProp.arraySize; i++)
            {
                var element = _systemsProp.GetArrayElementAtIndex(i);
                var typeProp = element.FindPropertyRelative("_systemType");
                var assemblyQualifiedName = typeProp.FindPropertyRelative("_assemblyQualifiedName").stringValue;
                if (assemblyQualifiedName == type.AssemblyQualifiedName)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddDiscoveredSystems(List<Strada.Core.Modules.SystemInfo> systems)
        {
            int added = 0;
            foreach (var system in systems)
            {
                if (!HasSystem(system.Type))
                {
                    AddDiscoveredSystem(system);
                    added++;
                }
            }

            EditorUtility.DisplayDialog("Systems Added",
                $"Added {added} systems to the module configuration.",
                "OK");
        }

        private void AddDiscoveredSystem(Strada.Core.Modules.SystemInfo system)
        {
            _systemsProp.arraySize++;
            var newElement = _systemsProp.GetArrayElementAtIndex(_systemsProp.arraySize - 1);

            var typeProp = newElement.FindPropertyRelative("_systemType");
            typeProp.FindPropertyRelative("_assemblyQualifiedName").stringValue = system.Type.AssemblyQualifiedName;

            newElement.FindPropertyRelative("_phase").enumValueIndex = (int)system.Phase;
            newElement.FindPropertyRelative("_order").intValue = system.Order;
            newElement.FindPropertyRelative("_category").stringValue = system.Category;
            newElement.FindPropertyRelative("_description").stringValue = system.Description;
            newElement.FindPropertyRelative("_enabled").boolValue = true;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
