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
        private static GUIStyle _selectedItemStyle;
        private static GUIStyle _foldoutHeaderStyle;
        private static GUIStyle _miniLabelStyle;
        private static GUIStyle _centeredLabelStyle;
        private static GUIStyle _linkLabelStyle;
        private static GUIStyle _badgeStyle;

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
            if (value >= criticalThreshold) return ErrorColor;
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
    }
}
