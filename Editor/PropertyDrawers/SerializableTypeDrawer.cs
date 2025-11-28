using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Strada.Core.Modules;
using Strada.Core.ECS;

namespace Strada.Core.Editor.PropertyDrawers
{
    /// <summary>
    /// Property drawer for SerializableType that provides a searchable type dropdown.
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableType))]
    public class SerializableTypeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, List<Type>> _cachedTypes = new();
        private static bool _cacheInitialized;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var assemblyQualifiedNameProp = property.FindPropertyRelative("_assemblyQualifiedName");
            var currentTypeName = assemblyQualifiedNameProp.stringValue;
            var currentType = string.IsNullOrEmpty(currentTypeName) ? null : Type.GetType(currentTypeName);

            var baseType = GetBaseTypeConstraint(fieldInfo);

            var buttonRect = EditorGUI.PrefixLabel(position, label);

            var displayName = currentType?.Name ?? "(None)";
            if (currentType != null)
            {
                displayName = $"{currentType.Name} ({currentType.Namespace})";
            }

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayName), FocusType.Keyboard))
            {
                ShowTypeSelectionMenu(buttonRect, assemblyQualifiedNameProp, baseType);
            }

            EditorGUI.EndProperty();
        }

        private Type GetBaseTypeConstraint(FieldInfo field)
        {
            var constraintAttr = field?.GetCustomAttribute<TypeConstraintAttribute>();
            if (constraintAttr != null)
            {
                return constraintAttr.BaseType;
            }

            if (field?.Name?.Contains("system", StringComparison.OrdinalIgnoreCase) == true ||
                field?.Name?.Contains("System", StringComparison.Ordinal) == true)
            {
                return typeof(ISystem);
            }

            return typeof(object);
        }

        private void ShowTypeSelectionMenu(Rect position, SerializedProperty property, Type baseType)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), string.IsNullOrEmpty(property.stringValue), () =>
            {
                property.stringValue = "";
                property.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            var types = GetAssignableTypes(baseType);
            var groupedTypes = types
                .GroupBy(t => t.Namespace ?? "Global")
                .OrderBy(g => g.Key);

            foreach (var group in groupedTypes)
            {
                foreach (var type in group.OrderBy(t => t.Name))
                {
                    var menuPath = $"{group.Key}/{type.Name}";
                    var isSelected = property.stringValue == type.AssemblyQualifiedName;

                    menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                    {
                        property.stringValue = type.AssemblyQualifiedName;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            menu.DropDown(position);
        }

        private static List<Type> GetAssignableTypes(Type baseType)
        {
            if (!_cacheInitialized)
            {
                InitializeCache();
            }

            if (_cachedTypes.TryGetValue(baseType, out var cached))
            {
                return cached;
            }

            var types = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (ShouldSkipAssembly(assembly))
                    continue;

                foreach (var type in GetTypesFromAssembly(assembly))
                {
                    if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
                    {
                        types.Add(type);
                    }
                }
            }

            _cachedTypes[baseType] = types;
            return types;
        }

        private static void InitializeCache()
        {
            _cacheInitialized = true;
            GetAssignableTypes(typeof(ISystem));
        }

        private static bool ShouldSkipAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith("System", StringComparison.Ordinal) ||
                   name.StartsWith("Microsoft", StringComparison.Ordinal) ||
                   name.StartsWith("Unity.", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                   name.StartsWith("mscorlib", StringComparison.Ordinal) ||
                   name.StartsWith("netstandard", StringComparison.Ordinal) ||
                   name.StartsWith("Mono.", StringComparison.Ordinal);
        }

        private static Type[] GetTypesFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes();
        }

        /// <summary>
        /// Clears the type cache. Call this after assembly reload.
        /// </summary>
        public static void ClearCache()
        {
            _cachedTypes.Clear();
            _cacheInitialized = false;
        }
    }

    /// <summary>
    /// Attribute to constrain the types shown in SerializableType dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TypeConstraintAttribute : PropertyAttribute
    {
        public Type BaseType { get; }

        public TypeConstraintAttribute(Type baseType)
        {
            BaseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
        }
    }
}
