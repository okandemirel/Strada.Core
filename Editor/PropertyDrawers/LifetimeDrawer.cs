using Strada.Core.DI;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Custom property drawer for Lifetime enum.
    /// Displays with colored badge and icon for better visual recognition.
    /// </summary>
    [CustomPropertyDrawer(typeof(Lifetime))]
    public class LifetimeDrawer : PropertyDrawer
    {
        private const float IconSize = 16f;
        private const float BadgeWidth = 20f;
        private const float Padding = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            EditorGUI.LabelField(labelRect, label);

            var badgeRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                position.y + 2,
                BadgeWidth,
                position.height - 4);

            var enumRect = new Rect(
                badgeRect.xMax + Padding,
                position.y,
                position.width - EditorGUIUtility.labelWidth - BadgeWidth - Padding,
                position.height);

            var lifetime = (Lifetime)property.enumValueIndex;
            var color = GetLifetimeColor(lifetime);

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            var badgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };

            GUI.Box(badgeRect, GetLifetimeIcon(lifetime), badgeStyle);

            GUI.backgroundColor = previousColor;

            EditorGUI.PropertyField(enumRect, property, GUIContent.none);

            EditorGUI.EndProperty();
        }

        private Color GetLifetimeColor(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => StradaEditorStyles.SingletonColor,
                Lifetime.Transient => StradaEditorStyles.TransientColor,
                Lifetime.Scoped => StradaEditorStyles.ScopedColor,
                _ => StradaEditorStyles.TextColor
            };
        }

        private string GetLifetimeIcon(Lifetime lifetime)
        {
            return lifetime switch
            {
                Lifetime.Singleton => "S",
                Lifetime.Transient => "T",
                Lifetime.Scoped => "C",
                _ => "?"
            };
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
