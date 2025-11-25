using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(Bridge.ReactiveProperty<>), true)]
    public class ReactivePropertyDrawer : PropertyDrawer
    {
        private const float LiveIndicatorWidth = 8f;
        private const float ButtonWidth = 60f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var valueProperty = property.FindPropertyRelative("_value");
            if (valueProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Cannot find _value field");
                EditorGUI.EndProperty();
                return;
            }

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var indicatorRect = new Rect(position.x + EditorGUIUtility.labelWidth + 2, position.y + 4, LiveIndicatorWidth, position.height - 8);
            var valueRect = new Rect(indicatorRect.xMax + 4, position.y, position.width - EditorGUIUtility.labelWidth - LiveIndicatorWidth - ButtonWidth - 12, position.height);
            var buttonRect = new Rect(valueRect.xMax + 4, position.y, ButtonWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            bool isPlaying = Application.isPlaying;
            var indicatorColor = isPlaying ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            EditorGUI.DrawRect(indicatorRect, indicatorColor);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(valueRect, valueProperty, GUIContent.none);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed && isPlaying)
            {
                property.serializedObject.ApplyModifiedProperties();
                TryNotifyProperty(property);
            }

            GUI.enabled = isPlaying;
            if (GUI.Button(buttonRect, "Notify"))
            {
                TryNotifyProperty(property);
            }
            GUI.enabled = true;

            EditorGUI.EndProperty();
        }

        private void TryNotifyProperty(SerializedProperty property)
        {
            var targetObject = property.serializedObject.targetObject;
            var path = property.propertyPath;

            var field = GetFieldByPath(targetObject.GetType(), path);
            if (field == null) return;

            var reactiveProperty = field.GetValue(targetObject);
            if (reactiveProperty == null) return;

            var notifyMethod = reactiveProperty.GetType().GetMethod("Notify", BindingFlags.Public | BindingFlags.Instance);
            notifyMethod?.Invoke(reactiveProperty, null);
        }

        private FieldInfo GetFieldByPath(Type type, string path)
        {
            var parts = path.Split('.');
            FieldInfo field = null;
            var currentType = type;

            foreach (var part in parts)
            {
                if (part == "Array" || part.StartsWith("data[")) continue;

                field = currentType.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return null;

                currentType = field.FieldType;
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(Bridge.ReactiveProperty<>))
                    return field;
            }

            return field;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("_value");
            if (valueProperty == null) return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(valueProperty);
        }
    }

    [CustomPropertyDrawer(typeof(Bridge.ReactiveCollection<>), true)]
    public class ReactiveCollectionDrawer : PropertyDrawer
    {
        private bool _foldout = true;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var itemsProperty = property.FindPropertyRelative("_items");
            if (itemsProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Cannot find _items field");
                EditorGUI.EndProperty();
                return;
            }

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var countBadge = $" ({itemsProperty.arraySize})";

            var foldoutLabel = new GUIContent(label.text + countBadge);
            _foldout = EditorGUI.Foldout(headerRect, _foldout, foldoutLabel, true);

            if (_foldout)
            {
                EditorGUI.indentLevel++;
                var yOffset = EditorGUIUtility.singleLineHeight + 2;

                for (int i = 0; i < itemsProperty.arraySize; i++)
                {
                    var element = itemsProperty.GetArrayElementAtIndex(i);
                    var elementHeight = EditorGUI.GetPropertyHeight(element);
                    var elementRect = new Rect(position.x, position.y + yOffset, position.width - 20, elementHeight);
                    var removeRect = new Rect(position.xMax - 18, position.y + yOffset, 18, EditorGUIUtility.singleLineHeight);

                    EditorGUI.PropertyField(elementRect, element, new GUIContent($"[{i}]"));

                    if (GUI.Button(removeRect, "×"))
                    {
                        itemsProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    yOffset += elementHeight + 2;
                }

                var addButtonRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(addButtonRect, "+ Add Element"))
                {
                    itemsProperty.InsertArrayElementAtIndex(itemsProperty.arraySize);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!_foldout) return EditorGUIUtility.singleLineHeight;

            var itemsProperty = property.FindPropertyRelative("_items");
            if (itemsProperty == null) return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + 4;
            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                height += EditorGUI.GetPropertyHeight(itemsProperty.GetArrayElementAtIndex(i)) + 2;
            }
            height += EditorGUIUtility.singleLineHeight + 4;
            return height;
        }
    }
}
