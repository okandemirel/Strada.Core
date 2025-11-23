using System;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Custom GUI helper methods for Strada editor tools.
    /// Provides high-level UI components with consistent styling.
    /// </summary>
    public static class StradaEditorGUI
    {
        #region Header & Sections

        /// <summary>
        /// Draws a header with optional icon.
        /// </summary>
        public static void DrawHeader(string title, GUIContent icon = null)
        {
            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
                GUILayout.Space(StradaEditorStyles.SmallSpacing);
            }

            GUILayout.Label(title, StradaEditorStyles.HeaderStyle);

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(StradaEditorStyles.SmallSpacing);
        }

        /// <summary>
        /// Draws a sub-header with optional icon.
        /// </summary>
        public static void DrawSubHeader(string title, GUIContent icon = null)
        {
            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
                GUILayout.Space(StradaEditorStyles.SmallSpacing);
            }

            GUILayout.Label(title, StradaEditorStyles.SubHeaderStyle);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Begins a collapsible section with a foldout.
        /// </summary>
        public static bool BeginFoldoutSection(string title, bool foldout, GUIContent icon = null)
        {
            EditorGUILayout.BeginVertical(StradaEditorStyles.BoxStyle);

            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
            }

            var newFoldout = EditorGUILayout.Foldout(foldout, title, true, StradaEditorStyles.FoldoutStyle);

            EditorGUILayout.EndHorizontal();

            return newFoldout;
        }

        /// <summary>
        /// Ends a foldout section.
        /// </summary>
        public static void EndFoldoutSection()
        {
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a horizontal line separator.
        /// </summary>
        public static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, StradaEditorStyles.SubtleTextColor);
            GUILayout.Space(StradaEditorStyles.SmallSpacing);
        }

        #endregion

        #region Buttons

        /// <summary>
        /// Draws a button with an icon.
        /// </summary>
        public static bool DrawButton(string text, GUIContent icon = null, params GUILayoutOption[] options)
        {
            GUIContent content;
            if (icon?.image != null)
            {
                content = new GUIContent(text, icon.image);
            }
            else
            {
                content = new GUIContent(text);
            }

            return GUILayout.Button(content, StradaEditorStyles.ButtonStyle, options);
        }

        /// <summary>
        /// Draws an icon-only button.
        /// </summary>
        public static bool DrawIconButton(GUIContent icon, string tooltip = "", float size = 24f)
        {
            var content = new GUIContent(icon.image, tooltip);
            return GUILayout.Button(content, GUILayout.Width(size), GUILayout.Height(size));
        }

        /// <summary>
        /// Draws a colored button.
        /// </summary>
        public static bool DrawColoredButton(string text, Color color, params GUILayoutOption[] options)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            var result = GUILayout.Button(text, StradaEditorStyles.ButtonStyle, options);

            GUI.backgroundColor = previousColor;

            return result;
        }

        #endregion

        #region Info Boxes

        /// <summary>
        /// Draws a help box with custom styling.
        /// </summary>
        public static void DrawHelpBox(string message, MessageType messageType = MessageType.Info)
        {
            var icon = messageType switch
            {
                MessageType.Warning => StradaEditorIcons.WarningIcon,
                MessageType.Error => StradaEditorIcons.ErrorIcon,
                _ => StradaEditorIcons.InfoIcon
            };

            var color = messageType switch
            {
                MessageType.Warning => StradaEditorStyles.WarningColor,
                MessageType.Error => StradaEditorStyles.ErrorColor,
                _ => StradaEditorStyles.PrimaryColor
            };

            DrawColoredBox(message, color, icon);
        }

        /// <summary>
        /// Draws a colored box with optional icon.
        /// </summary>
        public static void DrawColoredBox(string message, Color color, GUIContent icon = null)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = StradaEditorStyles.WithAlpha(color, 0.2f);

            EditorGUILayout.BeginVertical(StradaEditorStyles.HelpBoxStyle);

            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
                GUILayout.Space(StradaEditorStyles.SmallSpacing);
            }

            GUILayout.Label(message, StradaEditorStyles.RichTextLabelStyle);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUI.backgroundColor = previousColor;
        }

        /// <summary>
        /// Draws a validation box showing valid/invalid status.
        /// </summary>
        public static void DrawValidationBox(bool isValid, string message, bool hasWarnings = false)
        {
            var color = StradaEditorStyles.GetValidationColor(isValid, hasWarnings);
            var icon = StradaEditorIcons.GetValidationIcon(isValid, hasWarnings);

            DrawColoredBox(message, color, icon);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Draws a property field with custom label width.
        /// </summary>
        public static void DrawProperty(SerializedProperty property, string label = null, float labelWidth = -1)
        {
            var previousLabelWidth = EditorGUIUtility.labelWidth;

            if (labelWidth > 0)
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }

            if (label != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        /// <summary>
        /// Draws a read-only property field.
        /// </summary>
        public static void DrawReadOnlyProperty(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(StradaEditorStyles.LabelWidth));

            GUI.enabled = false;
            EditorGUILayout.TextField(value);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a property with an icon.
        /// </summary>
        public static void DrawPropertyWithIcon(SerializedProperty property, GUIContent icon, string label = null)
        {
            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
            }

            if (label != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Layout Helpers

        /// <summary>
        /// Begins an inspector panel with padding.
        /// </summary>
        public static void BeginInspectorPanel()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(StradaEditorStyles.MediumPadding);
        }

        /// <summary>
        /// Ends an inspector panel.
        /// </summary>
        public static void EndInspectorPanel()
        {
            GUILayout.Space(StradaEditorStyles.MediumPadding);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Begins an indented section.
        /// </summary>
        public static void BeginIndent(int levels = 1)
        {
            EditorGUI.indentLevel += levels;
        }

        /// <summary>
        /// Ends an indented section.
        /// </summary>
        public static void EndIndent(int levels = 1)
        {
            EditorGUI.indentLevel -= levels;
        }

        /// <summary>
        /// Draws a vertical space.
        /// </summary>
        public static void Space(float pixels = -1)
        {
            if (pixels < 0)
            {
                GUILayout.Space(StradaEditorStyles.MediumSpacing);
            }
            else
            {
                GUILayout.Space(pixels);
            }
        }

        #endregion

        #region Action Buttons

        /// <summary>
        /// Draws a row of action buttons.
        /// </summary>
        public static void DrawActionButtons(params (string label, Action onClick)[] buttons)
        {
            EditorGUILayout.BeginHorizontal();

            foreach (var (label, onClick) in buttons)
            {
                if (GUILayout.Button(label, StradaEditorStyles.ButtonStyle))
                {
                    onClick?.Invoke();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a row of icon buttons.
        /// </summary>
        public static void DrawIconButtons(params (GUIContent icon, string tooltip, Action onClick)[] buttons)
        {
            EditorGUILayout.BeginHorizontal();

            foreach (var (icon, tooltip, onClick) in buttons)
            {
                if (DrawIconButton(icon, tooltip))
                {
                    onClick?.Invoke();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Labels

        /// <summary>
        /// Draws a label with an icon.
        /// </summary>
        public static void DrawLabelWithIcon(string label, GUIContent icon)
        {
            EditorGUILayout.BeginHorizontal();

            if (icon?.image != null)
            {
                GUILayout.Label(icon, GUILayout.Width(StradaEditorStyles.IconSize), GUILayout.Height(StradaEditorStyles.IconSize));
                GUILayout.Space(StradaEditorStyles.SmallSpacing);
            }

            GUILayout.Label(label);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws a centered label.
        /// </summary>
        public static void DrawCenteredLabel(string text)
        {
            GUILayout.Label(text, StradaEditorStyles.CenteredLabelStyle);
        }

        /// <summary>
        /// Draws a colored label.
        /// </summary>
        public static void DrawColoredLabel(string text, Color color)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = color;

            GUILayout.Label(text);

            GUI.contentColor = previousColor;
        }

        #endregion

        #region Badge & Status

        /// <summary>
        /// Draws a small colored badge with text.
        /// </summary>
        public static void DrawBadge(string text, Color color)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };

            GUILayout.Label(text, style, GUILayout.ExpandWidth(false));

            GUI.backgroundColor = previousColor;
        }

        /// <summary>
        /// Draws a status indicator (colored dot + label).
        /// </summary>
        public static void DrawStatus(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();

            var dotTexture = StradaEditorStyles.CreateColorTexture(color);
            GUILayout.Label(dotTexture, GUILayout.Width(12), GUILayout.Height(12));
            GUILayout.Space(4);
            GUILayout.Label(label);

            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
