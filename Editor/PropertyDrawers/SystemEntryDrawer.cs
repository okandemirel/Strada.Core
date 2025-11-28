using UnityEditor;
using UnityEngine;
using Strada.Core.Modules;
using Strada.Core.ECS.World;

namespace Strada.Core.Editor.PropertyDrawers
{
    /// <summary>
    /// Property drawer for SystemEntry that provides a compact, informative Inspector view.
    /// </summary>
    [CustomPropertyDrawer(typeof(SystemEntry))]
    public class SystemEntryDrawer : PropertyDrawer
    {
        private const float EnabledToggleWidth = 18f;
        private const float PhaseWidth = 85f;
        private const float OrderWidth = 40f;
        private const float Spacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var descriptionProp = property.FindPropertyRelative("_description");
            bool hasDescription = !string.IsNullOrEmpty(descriptionProp.stringValue);

            return EditorGUIUtility.singleLineHeight + (hasDescription ? EditorGUIUtility.singleLineHeight : 0);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var enabledProp = property.FindPropertyRelative("_enabled");
            var systemTypeProp = property.FindPropertyRelative("_systemType");
            var phaseProp = property.FindPropertyRelative("_phase");
            var orderProp = property.FindPropertyRelative("_order");
            var descriptionProp = property.FindPropertyRelative("_description");
            var categoryProp = property.FindPropertyRelative("_category");

            var lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var toggleRect = new Rect(lineRect.x, lineRect.y, EnabledToggleWidth, lineRect.height);
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            float remainingWidth = lineRect.width - EnabledToggleWidth - PhaseWidth - OrderWidth - Spacing * 3;
            var typeRect = new Rect(toggleRect.xMax + Spacing, lineRect.y, remainingWidth, lineRect.height);

            var assemblyQualifiedNameProp = systemTypeProp.FindPropertyRelative("_assemblyQualifiedName");
            var typeName = "(None)";
            if (!string.IsNullOrEmpty(assemblyQualifiedNameProp.stringValue))
            {
                var type = System.Type.GetType(assemblyQualifiedNameProp.stringValue);
                typeName = type?.Name ?? "(Invalid Type)";
            }

            var previousColor = GUI.color;
            if (!enabledProp.boolValue)
            {
                GUI.color = new Color(1, 1, 1, 0.5f);
            }

            if (EditorGUI.DropdownButton(typeRect, new GUIContent(typeName, GetTooltip(property)), FocusType.Keyboard))
            {
                ShowSystemTypeMenu(typeRect, assemblyQualifiedNameProp);
            }

            var phaseRect = new Rect(typeRect.xMax + Spacing, lineRect.y, PhaseWidth, lineRect.height);
            EditorGUI.PropertyField(phaseRect, phaseProp, GUIContent.none);

            var orderRect = new Rect(phaseRect.xMax + Spacing, lineRect.y, OrderWidth, lineRect.height);
            EditorGUI.PropertyField(orderRect, orderProp, GUIContent.none);

            GUI.color = previousColor;

            if (!string.IsNullOrEmpty(descriptionProp.stringValue))
            {
                var descRect = new Rect(
                    position.x + EnabledToggleWidth + Spacing,
                    position.y + EditorGUIUtility.singleLineHeight,
                    position.width - EnabledToggleWidth - Spacing,
                    EditorGUIUtility.singleLineHeight);

                var oldColor = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                EditorGUI.LabelField(descRect, descriptionProp.stringValue, EditorStyles.miniLabel);
                GUI.color = oldColor;
            }

            EditorGUI.EndProperty();
        }

        private string GetTooltip(SerializedProperty property)
        {
            var descriptionProp = property.FindPropertyRelative("_description");
            var categoryProp = property.FindPropertyRelative("_category");

            var tooltip = "";
            if (!string.IsNullOrEmpty(categoryProp.stringValue))
            {
                tooltip += $"Category: {categoryProp.stringValue}\n";
            }
            if (!string.IsNullOrEmpty(descriptionProp.stringValue))
            {
                tooltip += descriptionProp.stringValue;
            }

            return string.IsNullOrEmpty(tooltip) ? null : tooltip;
        }

        private void ShowSystemTypeMenu(Rect position, SerializedProperty assemblyQualifiedNameProp)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(None)"), string.IsNullOrEmpty(assemblyQualifiedNameProp.stringValue), () =>
            {
                assemblyQualifiedNameProp.stringValue = "";
                assemblyQualifiedNameProp.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            var systems = RuntimeSystemDiscovery.DiscoverSystems();
            var groupedSystems = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Modules.SystemInfo>>();

            foreach (var system in systems)
            {
                var key = string.IsNullOrEmpty(system.Category) ?
                    (string.IsNullOrEmpty(system.Module) ? "Other" : system.Module) :
                    system.Category;

                if (!groupedSystems.TryGetValue(key, out var list))
                {
                    list = new System.Collections.Generic.List<Modules.SystemInfo>();
                    groupedSystems[key] = list;
                }
                list.Add(system);
            }

            foreach (var group in groupedSystems)
            {
                foreach (var system in group.Value)
                {
                    var menuPath = $"{group.Key}/{system.Type.Name}";
                    var isSelected = assemblyQualifiedNameProp.stringValue == system.Type.AssemblyQualifiedName;
                    var tooltip = system.Description;

                    menu.AddItem(new GUIContent(menuPath, tooltip), isSelected, () =>
                    {
                        assemblyQualifiedNameProp.stringValue = system.Type.AssemblyQualifiedName;
                        assemblyQualifiedNameProp.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            menu.DropDown(position);
        }
    }

    /// <summary>
    /// Property drawer for ServiceEntry.
    /// </summary>
    [CustomPropertyDrawer(typeof(ServiceEntry))]
    public class ServiceEntryDrawer : PropertyDrawer
    {
        private const float EnabledToggleWidth = 18f;
        private const float LifetimeWidth = 80f;
        private const float Spacing = 4f;
        private const float ArrowWidth = 20f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var enabledProp = property.FindPropertyRelative("_enabled");
            var interfaceTypeProp = property.FindPropertyRelative("_interfaceType");
            var implementationTypeProp = property.FindPropertyRelative("_implementationType");
            var lifetimeProp = property.FindPropertyRelative("_lifetime");

            var toggleRect = new Rect(position.x, position.y, EnabledToggleWidth, position.height);
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            float typeWidth = (position.width - EnabledToggleWidth - ArrowWidth - LifetimeWidth - Spacing * 4) / 2;

            var interfaceRect = new Rect(toggleRect.xMax + Spacing, position.y, typeWidth, position.height);
            DrawTypeField(interfaceRect, interfaceTypeProp, enabledProp.boolValue);

            var arrowRect = new Rect(interfaceRect.xMax, position.y, ArrowWidth, position.height);
            EditorGUI.LabelField(arrowRect, "→", EditorStyles.centeredGreyMiniLabel);

            var implRect = new Rect(arrowRect.xMax, position.y, typeWidth, position.height);
            DrawTypeField(implRect, implementationTypeProp, enabledProp.boolValue);

            var lifetimeRect = new Rect(implRect.xMax + Spacing, position.y, LifetimeWidth, position.height);
            EditorGUI.PropertyField(lifetimeRect, lifetimeProp, GUIContent.none);

            EditorGUI.EndProperty();
        }

        private void DrawTypeField(Rect rect, SerializedProperty typeProp, bool enabled)
        {
            var assemblyQualifiedNameProp = typeProp.FindPropertyRelative("_assemblyQualifiedName");
            var typeName = "(None)";
            if (!string.IsNullOrEmpty(assemblyQualifiedNameProp.stringValue))
            {
                var type = System.Type.GetType(assemblyQualifiedNameProp.stringValue);
                typeName = type?.Name ?? "(Invalid)";
            }

            var previousColor = GUI.color;
            if (!enabled)
            {
                GUI.color = new Color(1, 1, 1, 0.5f);
            }

            if (EditorGUI.DropdownButton(rect, new GUIContent(typeName), FocusType.Keyboard))
            {
                ShowTypeMenu(rect, assemblyQualifiedNameProp, typeof(object));
            }

            GUI.color = previousColor;
        }

        private void ShowTypeMenu(Rect position, SerializedProperty property, System.Type baseType)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), string.IsNullOrEmpty(property.stringValue), () =>
            {
                property.stringValue = "";
                property.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            menu.DropDown(position);
        }
    }

    /// <summary>
    /// Property drawer for ModuleEntry.
    /// </summary>
    [CustomPropertyDrawer(typeof(ModuleEntry))]
    public class ModuleEntryDrawer : PropertyDrawer
    {
        private const float EnabledToggleWidth = 18f;
        private const float PriorityWidth = 50f;
        private const float Spacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var enabledProp = property.FindPropertyRelative("_enabled");
            var configProp = property.FindPropertyRelative("_config");

            var toggleRect = new Rect(position.x, position.y, EnabledToggleWidth, position.height);
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            var previousColor = GUI.color;
            if (!enabledProp.boolValue)
            {
                GUI.color = new Color(1, 1, 1, 0.5f);
            }

            float configWidth = position.width - EnabledToggleWidth - PriorityWidth - Spacing * 2;
            var configRect = new Rect(toggleRect.xMax + Spacing, position.y, configWidth, position.height);
            EditorGUI.PropertyField(configRect, configProp, GUIContent.none);

            var priorityRect = new Rect(configRect.xMax + Spacing, position.y, PriorityWidth, position.height);
            if (configProp.objectReferenceValue is ModuleConfig moduleConfig)
            {
                EditorGUI.LabelField(priorityRect, $"P: {moduleConfig.Priority}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUI.LabelField(priorityRect, "P: -", EditorStyles.miniLabel);
            }

            GUI.color = previousColor;

            EditorGUI.EndProperty();
        }
    }
}
