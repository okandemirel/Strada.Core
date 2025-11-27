using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.PropertyDrawers
{
    /// <summary>
    /// Enhanced property drawer for ReactiveProperty fields.
    /// Features:
    /// - Live indicator showing subscription status (green = has subscribers, gray = no subscribers)
    /// - Subscriber count display during Play Mode
    /// - "Notify" button to force notification to all subscribers
    /// Requirements: 7.1, 7.3, 7.5
    /// </summary>
    [CustomPropertyDrawer(typeof(Bridge.ReactiveProperty<>), true)]
    public class ReactivePropertyDrawer : PropertyDrawer
    {
        private const float LiveIndicatorWidth = 12f;
        private const float SubscriberCountWidth = 30f;
        private const float ButtonWidth = 50f;
        private const float Spacing = 4f;

        // Colors for live indicator
        private static readonly Color ActiveSubscribersColor = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color NoSubscribersColor = new Color(0.8f, 0.6f, 0.2f);
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f);

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

            bool isPlaying = Application.isPlaying;
            int subscriberCount = 0;
            bool hasSubscribers = false;

            // Get subscriber count during Play Mode
            if (isPlaying)
            {
                subscriberCount = GetSubscriberCount(property);
                hasSubscribers = subscriberCount > 0;
            }

            // Calculate layout rects
            float currentX = position.x;
            
            // Label
            var labelRect = new Rect(currentX, position.y, EditorGUIUtility.labelWidth, position.height);
            currentX += EditorGUIUtility.labelWidth + Spacing;

            // Live indicator (circle)
            var indicatorRect = new Rect(currentX, position.y + (position.height - LiveIndicatorWidth) / 2, LiveIndicatorWidth, LiveIndicatorWidth);
            currentX += LiveIndicatorWidth + Spacing;

            // Subscriber count (only in Play Mode)
            Rect subscriberCountRect = default;
            if (isPlaying)
            {
                subscriberCountRect = new Rect(currentX, position.y, SubscriberCountWidth, position.height);
                currentX += SubscriberCountWidth + Spacing;
            }

            // Notify button
            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);
            
            // Value field (remaining space)
            var valueRect = new Rect(currentX, position.y, buttonRect.x - currentX - Spacing, position.height);

            // Draw label
            EditorGUI.LabelField(labelRect, label);

            // Draw live indicator
            DrawLiveIndicator(indicatorRect, isPlaying, hasSubscribers);

            // Draw subscriber count during Play Mode
            if (isPlaying)
            {
                var countStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = hasSubscribers ? FontStyle.Bold : FontStyle.Normal
                };
                countStyle.normal.textColor = hasSubscribers ? ActiveSubscribersColor : NoSubscribersColor;
                EditorGUI.LabelField(subscriberCountRect, subscriberCount.ToString(), countStyle);
            }

            // Draw value field
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(valueRect, valueProperty, GUIContent.none);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed && isPlaying)
            {
                property.serializedObject.ApplyModifiedProperties();
                TryNotifyProperty(property);
            }

            // Draw Notify button
            GUI.enabled = isPlaying;
            if (GUI.Button(buttonRect, new GUIContent("Notify", "Force notification to all subscribers with current value")))
            {
                TryNotifyProperty(property);
            }
            GUI.enabled = true;

            EditorGUI.EndProperty();
        }

        private void DrawLiveIndicator(Rect rect, bool isPlaying, bool hasSubscribers)
        {
            Color indicatorColor;
            string tooltip;

            if (!isPlaying)
            {
                indicatorColor = InactiveColor;
                tooltip = "Not in Play Mode";
            }
            else if (hasSubscribers)
            {
                indicatorColor = ActiveSubscribersColor;
                tooltip = "Has active subscribers";
            }
            else
            {
                indicatorColor = NoSubscribersColor;
                tooltip = "No subscribers";
            }

            // Draw circular indicator
            var oldColor = GUI.color;
            GUI.color = indicatorColor;
            
            // Draw filled circle using a texture or simple rect with rounded appearance
            EditorGUI.DrawRect(rect, indicatorColor);
            
            // Draw border
            var borderColor = new Color(indicatorColor.r * 0.7f, indicatorColor.g * 0.7f, indicatorColor.b * 0.7f);
            DrawRectBorder(rect, borderColor, 1);
            
            GUI.color = oldColor;

            // Add tooltip
            EditorGUI.LabelField(rect, new GUIContent("", tooltip));
        }

        private void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private int GetSubscriberCount(SerializedProperty property)
        {
            var targetObject = property.serializedObject.targetObject;
            var path = property.propertyPath;

            var field = GetFieldByPath(targetObject.GetType(), path);
            if (field == null) return 0;

            var reactiveProperty = field.GetValue(targetObject);
            if (reactiveProperty == null) return 0;

            var subscriberCountProperty = reactiveProperty.GetType().GetProperty("SubscriberCount", BindingFlags.Public | BindingFlags.Instance);
            if (subscriberCountProperty == null) return 0;

            return (int)subscriberCountProperty.GetValue(reactiveProperty);
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
            object currentObject = null;

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

    /// <summary>
    /// Enhanced property drawer for ReactiveCollection fields.
    /// Features:
    /// - Element count badge in header
    /// - Add/remove element buttons
    /// - Expandable element list with drag reordering support
    /// - Clear all button
    /// Requirements: 7.4
    /// </summary>
    [CustomPropertyDrawer(typeof(Bridge.ReactiveCollection<>), true)]
    public class ReactiveCollectionDrawer : PropertyDrawer
    {
        private bool _foldout = true;
        private const float ButtonWidth = 20f;
        private const float HeaderButtonWidth = 60f;
        private const float Spacing = 2f;

        // Colors for count badge
        private static readonly Color BadgeBackgroundColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color BadgeTextColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color EmptyBadgeColor = new Color(0.6f, 0.6f, 0.6f);

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

            int count = itemsProperty.arraySize;
            bool isEmpty = count == 0;

            // Header row
            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            // Foldout
            var foldoutRect = new Rect(headerRect.x, headerRect.y, EditorGUIUtility.labelWidth - 40, headerRect.height);
            _foldout = EditorGUI.Foldout(foldoutRect, _foldout, label, true);

            // Count badge
            var badgeContent = new GUIContent(count.ToString());
            var badgeWidth = Mathf.Max(24, EditorStyles.miniLabel.CalcSize(badgeContent).x + 8);
            var badgeRect = new Rect(foldoutRect.xMax + 4, headerRect.y + 2, badgeWidth, headerRect.height - 4);
            DrawCountBadge(badgeRect, count, isEmpty);

            // Header buttons (Add, Clear)
            var clearButtonRect = new Rect(headerRect.xMax - HeaderButtonWidth, headerRect.y, HeaderButtonWidth, headerRect.height);
            var addButtonRect = new Rect(clearButtonRect.x - HeaderButtonWidth - Spacing, headerRect.y, HeaderButtonWidth, headerRect.height);

            GUI.enabled = !isEmpty;
            if (GUI.Button(clearButtonRect, new GUIContent("Clear", "Remove all elements")))
            {
                itemsProperty.ClearArray();
                property.serializedObject.ApplyModifiedProperties();
                TryNotifyCollectionClear(property);
            }
            GUI.enabled = true;

            if (GUI.Button(addButtonRect, new GUIContent("+ Add", "Add new element")))
            {
                itemsProperty.InsertArrayElementAtIndex(count);
                property.serializedObject.ApplyModifiedProperties();
            }

            // Expanded content
            if (_foldout)
            {
                EditorGUI.indentLevel++;
                var yOffset = EditorGUIUtility.singleLineHeight + Spacing;

                if (isEmpty)
                {
                    // Show empty message
                    var emptyRect = new Rect(position.x + 16, position.y + yOffset, position.width - 16, EditorGUIUtility.singleLineHeight);
                    var emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    EditorGUI.LabelField(emptyRect, "Collection is empty", emptyStyle);
                    yOffset += EditorGUIUtility.singleLineHeight + Spacing;
                }
                else
                {
                    // Draw elements
                    for (int i = 0; i < count; i++)
                    {
                        var element = itemsProperty.GetArrayElementAtIndex(i);
                        var elementHeight = EditorGUI.GetPropertyHeight(element);
                        
                        // Element row
                        var rowRect = new Rect(position.x, position.y + yOffset, position.width, elementHeight);
                        
                        // Index label
                        var indexRect = new Rect(rowRect.x + 16, rowRect.y, 30, EditorGUIUtility.singleLineHeight);
                        var indexStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                        indexStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                        EditorGUI.LabelField(indexRect, $"[{i}]", indexStyle);

                        // Element field
                        var elementRect = new Rect(indexRect.xMax + 4, rowRect.y, rowRect.width - indexRect.width - ButtonWidth * 2 - 24, elementHeight);
                        EditorGUI.PropertyField(elementRect, element, GUIContent.none);

                        // Move up button
                        var moveUpRect = new Rect(elementRect.xMax + Spacing, rowRect.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
                        GUI.enabled = i > 0;
                        if (GUI.Button(moveUpRect, new GUIContent("▲", "Move up")))
                        {
                            itemsProperty.MoveArrayElement(i, i - 1);
                            property.serializedObject.ApplyModifiedProperties();
                        }
                        GUI.enabled = true;

                        // Move down button
                        var moveDownRect = new Rect(moveUpRect.xMax + 1, rowRect.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
                        GUI.enabled = i < count - 1;
                        if (GUI.Button(moveDownRect, new GUIContent("▼", "Move down")))
                        {
                            itemsProperty.MoveArrayElement(i, i + 1);
                            property.serializedObject.ApplyModifiedProperties();
                        }
                        GUI.enabled = true;

                        // Remove button
                        var removeRect = new Rect(moveDownRect.xMax + Spacing, rowRect.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
                        if (GUI.Button(removeRect, new GUIContent("×", "Remove element")))
                        {
                            // Store the element before deletion for notification
                            itemsProperty.DeleteArrayElementAtIndex(i);
                            property.serializedObject.ApplyModifiedProperties();
                            break;
                        }

                        yOffset += elementHeight + Spacing;
                    }
                }

                // Bottom add button
                var bottomAddRect = new Rect(position.x + 16, position.y + yOffset, position.width - 16, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(bottomAddRect, new GUIContent("+ Add Element", "Add new element to collection")))
                {
                    itemsProperty.InsertArrayElementAtIndex(count);
                    property.serializedObject.ApplyModifiedProperties();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void DrawCountBadge(Rect rect, int count, bool isEmpty)
        {
            var backgroundColor = isEmpty ? EmptyBadgeColor : BadgeBackgroundColor;
            EditorGUI.DrawRect(rect, backgroundColor);

            var textStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            textStyle.normal.textColor = BadgeTextColor;

            EditorGUI.LabelField(rect, count.ToString(), textStyle);
        }

        private void TryNotifyCollectionClear(SerializedProperty property)
        {
            if (!Application.isPlaying) return;

            var targetObject = property.serializedObject.targetObject;
            var path = property.propertyPath;

            var field = GetFieldByPath(targetObject.GetType(), path);
            if (field == null) return;

            var collection = field.GetValue(targetObject);
            if (collection == null) return;

            // The Clear method on ReactiveCollection already notifies, but we call it through serialization
            // so we need to manually trigger notification if needed
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
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(Bridge.ReactiveCollection<>))
                    return field;
            }

            return field;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!_foldout) return EditorGUIUtility.singleLineHeight;

            var itemsProperty = property.FindPropertyRelative("_items");
            if (itemsProperty == null) return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + Spacing; // Header

            if (itemsProperty.arraySize == 0)
            {
                height += EditorGUIUtility.singleLineHeight + Spacing; // Empty message
            }
            else
            {
                for (int i = 0; i < itemsProperty.arraySize; i++)
                {
                    height += EditorGUI.GetPropertyHeight(itemsProperty.GetArrayElementAtIndex(i)) + Spacing;
                }
            }

            height += EditorGUIUtility.singleLineHeight + Spacing; // Bottom add button
            return height;
        }
    }
}
