using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Utilities
{
    /// <summary>
    /// Shared styles and colors for consistent UI across all Strada editor windows.
    /// Provides centralized styling to ensure visual consistency.
    /// </summary>
    public static class StradaEditorStyles
    {
        public static readonly Color SingletonColor = new Color(0.4f, 0.8f, 0.4f);
        public static readonly Color TransientColor = new Color(1.0f, 0.6f, 0.2f);
        public static readonly Color ScopedColor = new Color(0.4f, 0.6f, 0.9f);

        public static readonly Color SuccessColor = new Color(0.4f, 0.8f, 0.4f);
        public static readonly Color WarningColor = new Color(1.0f, 0.85f, 0.4f);
        public static readonly Color ErrorColor = new Color(1.0f, 0.4f, 0.4f);
        public static readonly Color InfoColor = new Color(0.4f, 0.7f, 1.0f);

        public static readonly Color NormalColor = new Color(0.7f, 0.9f, 0.7f);
        public static readonly Color CriticalColor = new Color(1.0f, 0.4f, 0.4f);

        public static readonly Color SelectionColor = new Color(0.24f, 0.49f, 0.91f, 0.4f);
        public static readonly Color HoverColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);
        public static readonly Color SeparatorColor = new Color(0.15f, 0.15f, 0.15f);
        public static readonly Color HeaderBackgroundColor = new Color(0.2f, 0.2f, 0.2f);

        public static readonly Color EventColor = new Color(0.4f, 0.7f, 0.4f);
        public static readonly Color CommandColor = new Color(0.5f, 0.6f, 0.9f);
        public static readonly Color QueryColor = new Color(0.9f, 0.7f, 0.4f);

        private static GUIStyle _headerStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _statsBoxStyle;
        private static GUIStyle _toolbarSearchStyle;
        private static GUIStyle _selectedItemStyle;
        private static GUIStyle _foldoutHeaderStyle;
        private static GUIStyle _miniLabelStyle;
        private static GUIStyle _centeredLabelStyle;
        private static GUIStyle _linkLabelStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _tooltipStyle;

        private static bool _stylesInitialized;

        /// <summary>
        /// Large bold header style for section titles.
        /// </summary>
        public static GUIStyle HeaderStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _headerStyle;
            }
        }

        /// <summary>
        /// Medium bold header style for subsections.
        /// </summary>
        public static GUIStyle SubHeaderStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _subHeaderStyle;
            }
        }

        /// <summary>
        /// Box style for statistics panels.
        /// </summary>
        public static GUIStyle StatsBoxStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _statsBoxStyle;
            }
        }

        /// <summary>
        /// Style for selected list items.
        /// </summary>
        public static GUIStyle SelectedItemStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _selectedItemStyle;
            }
        }

        /// <summary>
        /// Style for foldout headers.
        /// </summary>
        public static GUIStyle FoldoutHeaderStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _foldoutHeaderStyle;
            }
        }

        /// <summary>
        /// Small label style for secondary information.
        /// </summary>
        public static GUIStyle MiniLabelStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _miniLabelStyle;
            }
        }

        /// <summary>
        /// Centered label style.
        /// </summary>
        public static GUIStyle CenteredLabelStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _centeredLabelStyle;
            }
        }

        /// <summary>
        /// Clickable link label style.
        /// </summary>
        public static GUIStyle LinkLabelStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _linkLabelStyle;
            }
        }

        /// <summary>
        /// Badge/pill style for status indicators.
        /// </summary>
        public static GUIStyle BadgeStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _badgeStyle;
            }
        }

        private static void EnsureStylesInitialized()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 10, 5)
            };

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(5, 5, 8, 4)
            };

            _statsBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(5, 5, 5, 5)
            };

            _selectedItemStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(4, 4, 3, 3),
                margin = new RectOffset(2, 2, 1, 1),
                normal = { background = CreateColorTexture(SelectionColor) }
            };

            _foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            _miniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(2, 2, 1, 1)
            };

            _centeredLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _linkLabelStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                padding = new RectOffset(0, 0, 0, 0)
            };

            _badgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(6, 6, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        /// <summary>
        /// Forces style reinitialization. Call this if styles appear broken.
        /// </summary>
        public static void ResetStyles()
        {
            _stylesInitialized = false;
        }

        /// <summary>
        /// Creates a 1x1 texture with the specified color.
        /// </summary>
        public static Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Gets the appropriate color for a lifetime type.
        /// </summary>
        public static Color GetLifetimeColor(DI.Lifetime lifetime)
        {
            return lifetime switch
            {
                DI.Lifetime.Singleton => SingletonColor,
                DI.Lifetime.Transient => TransientColor,
                DI.Lifetime.Scoped => ScopedColor,
                _ => Color.gray
            };
        }

        /// <summary>
        /// Gets the appropriate color for a threshold level.
        /// </summary>
        public static Color GetThresholdColor(double value, double warningThreshold, double criticalThreshold)
        {
            if (value >= criticalThreshold) return CriticalColor;
            if (value >= warningThreshold) return WarningColor;
            return NormalColor;
        }

        /// <summary>
        /// Draws a horizontal separator line.
        /// </summary>
        public static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        /// <summary>
        /// Draws a colored badge/pill.
        /// </summary>
        public static void DrawBadge(string text, Color color, float width = 60f)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, BadgeStyle, GUILayout.Width(width));
            GUI.backgroundColor = prevBg;
        }

        /// <summary>
        /// Draws a status indicator dot.
        /// </summary>
        public static void DrawStatusDot(Color color, float size = 10f)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>
        /// Draws a progress bar with custom colors.
        /// </summary>
        public static void DrawProgressBar(Rect rect, float progress, Color backgroundColor, Color fillColor)
        {
            EditorGUI.DrawRect(rect, backgroundColor);
            var fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
            EditorGUI.DrawRect(fillRect, fillColor);
        }

        /// <summary>
        /// Creates a tooltip content with consistent formatting.
        /// </summary>
        public static GUIContent CreateTooltip(string text, string tooltip)
        {
            return new GUIContent(text, tooltip);
        }

        /// <summary>
        /// Draws a section header with optional collapse functionality.
        /// </summary>
        public static bool DrawSectionHeader(string title, bool isExpanded)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, FoldoutHeaderStyle);
            EditorGUILayout.EndHorizontal();
            return isExpanded;
        }

        /// <summary>
        /// Standard minimum window size for Strada editor windows.
        /// </summary>
        public static readonly Vector2 StandardMinSize = new Vector2(600, 400);

        /// <summary>
        /// Large minimum window size for complex windows like Dashboard.
        /// </summary>
        public static readonly Vector2 LargeMinSize = new Vector2(900, 600);

        /// <summary>
        /// Small minimum window size for simple dialogs.
        /// </summary>
        public static readonly Vector2 SmallMinSize = new Vector2(400, 300);

        /// <summary>
        /// Draws a mini sparkline chart within the given rect.
        /// Values are scaled proportionally between their min and max.
        /// </summary>
        public static void DrawSparkline(Rect rect, IList<float> values, Color lineColor, Color bgColor)
        {
            EditorGUI.DrawRect(rect, bgColor);

            if (values == null || values.Count == 0) return;

            float min = values[0];
            float max = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }

            float range = max - min;
            if (range < Mathf.Epsilon) range = 1f;

            float barWidth = rect.width / values.Count;

            for (int i = 0; i < values.Count; i++)
            {
                float normalized = (values[i] - min) / range;
                float barHeight = normalized * rect.height;
                var barRect = new Rect(
                    rect.x + i * barWidth,
                    rect.y + rect.height - barHeight,
                    Mathf.Max(1f, barWidth),
                    barHeight
                );
                EditorGUI.DrawRect(barRect, lineColor);
            }
        }

        /// <summary>
        /// Draws a small bar chart with individually colored bars.
        /// </summary>
        public static void DrawMiniBarChart(Rect rect, float[] values, Color[] colors)
        {
            if (values == null || values.Length == 0) return;

            float max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max) max = values[i];
            }

            if (max < Mathf.Epsilon) max = 1f;

            float barWidth = rect.width / values.Length;
            float spacing = Mathf.Max(1f, barWidth * 0.1f);

            for (int i = 0; i < values.Length; i++)
            {
                float normalized = values[i] / max;
                float barHeight = normalized * rect.height;
                Color color = (colors != null && i < colors.Length) ? colors[i] : Color.white;
                var barRect = new Rect(
                    rect.x + i * barWidth + spacing * 0.5f,
                    rect.y + rect.height - barHeight,
                    barWidth - spacing,
                    barHeight
                );
                EditorGUI.DrawRect(barRect, color);
            }
        }

        /// <summary>
        /// Draws a horizontal timeline with a playhead indicator.
        /// </summary>
        public static void DrawTimeline(Rect rect, int totalFrames, int currentFrame, Color trackColor, Color playheadColor)
        {
            EditorGUI.DrawRect(rect, trackColor);

            if (totalFrames <= 0) return;

            float playheadX = rect.x + (rect.width * Mathf.Clamp01((float)currentFrame / totalFrames));
            var playheadRect = new Rect(playheadX - 1f, rect.y, 2f, rect.height);
            EditorGUI.DrawRect(playheadRect, playheadColor);
        }

        /// <summary>
        /// Saves a string value to EditorPrefs for persistent editor state.
        /// </summary>
        public static void SaveEditorState(string windowKey, string key, string value)
        {
            EditorPrefs.SetString($"Strada.{windowKey}.{key}", value);
        }

        /// <summary>
        /// Loads a string value from EditorPrefs for persistent editor state.
        /// </summary>
        public static string LoadEditorState(string windowKey, string key, string defaultValue = "")
        {
            return EditorPrefs.GetString($"Strada.{windowKey}.{key}", defaultValue);
        }

        /// <summary>
        /// Saves an int value to EditorPrefs for persistent editor state.
        /// </summary>
        public static void SaveEditorStateInt(string windowKey, string key, int value)
        {
            EditorPrefs.SetInt($"Strada.{windowKey}.{key}", value);
        }

        /// <summary>
        /// Loads an int value from EditorPrefs for persistent editor state.
        /// </summary>
        public static int LoadEditorStateInt(string windowKey, string key, int defaultValue = 0)
        {
            return EditorPrefs.GetInt($"Strada.{windowKey}.{key}", defaultValue);
        }

        /// <summary>
        /// Saves a bool value to EditorPrefs for persistent editor state.
        /// </summary>
        public static void SaveEditorStateBool(string windowKey, string key, bool value)
        {
            EditorPrefs.SetBool($"Strada.{windowKey}.{key}", value);
        }

        /// <summary>
        /// Loads a bool value from EditorPrefs for persistent editor state.
        /// </summary>
        public static bool LoadEditorStateBool(string windowKey, string key, bool defaultValue = false)
        {
            return EditorPrefs.GetBool($"Strada.{windowKey}.{key}", defaultValue);
        }

        /// <summary>
        /// Draws a context menu button ("...") and returns true if clicked.
        /// </summary>
        public static bool DrawContextMenuButton(float width = 20f)
        {
            return GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(width));
        }
    }
}
