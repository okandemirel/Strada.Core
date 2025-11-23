using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor
{
    /// <summary>
    /// Centralized styles, colors, and layout constants for Strada editor tools.
    /// Provides a consistent design language across all custom inspectors and editor windows.
    /// </summary>
    public static class StradaEditorStyles
    {
        #region Colors

        // Primary Colors (Unity-compatible)
        public static readonly Color PrimaryColor = new Color(0.13f, 0.59f, 0.95f); // #2196F3
        public static readonly Color SuccessColor = new Color(0.30f, 0.69f, 0.31f); // #4CAF50
        public static readonly Color WarningColor = new Color(1.00f, 0.60f, 0.00f); // #FF9800
        public static readonly Color ErrorColor = new Color(0.96f, 0.26f, 0.21f); // #F44336

        // Lifetime Colors
        public static readonly Color SingletonColor = new Color(0.61f, 0.15f, 0.69f); // #9C27B0 (Purple)
        public static readonly Color TransientColor = new Color(0.00f, 0.74f, 0.83f); // #00BCD4 (Cyan)
        public static readonly Color ScopedColor = new Color(1.00f, 0.92f, 0.23f); // #FFEB3B (Yellow)

        // Background Colors (adapts to Pro/Personal skin)
        public static Color BackgroundColor => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.76f, 0.76f, 0.76f);

        public static Color HeaderColor => EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f)
            : new Color(0.70f, 0.70f, 0.70f);

        // Text Colors
        public static Color TextColor => EditorGUIUtility.isProSkin
            ? new Color(0.82f, 0.82f, 0.82f)
            : new Color(0.09f, 0.09f, 0.09f);

        public static Color SubtleTextColor => EditorGUIUtility.isProSkin
            ? new Color(0.56f, 0.56f, 0.56f)
            : new Color(0.44f, 0.44f, 0.44f);

        #endregion

        #region Spacing & Layout

        public const float SmallPadding = 4f;
        public const float MediumPadding = 8f;
        public const float LargePadding = 16f;

        public const float SmallSpacing = 2f;
        public const float MediumSpacing = 4f;
        public const float LargeSpacing = 8f;

        public const float LineHeight = 20f;
        public const float IconSize = 16f;
        public const float ButtonHeight = 24f;

        public const float IndentWidth = 15f;
        public const float LabelWidth = 140f;

        #endregion

        #region GUIStyles (Lazy Initialization)

        private static GUIStyle _headerStyle;
        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        padding = new RectOffset((int)MediumPadding, (int)MediumPadding, (int)SmallPadding, (int)SmallPadding),
                        normal = { textColor = TextColor }
                    };
                }
                return _headerStyle;
            }
        }

        private static GUIStyle _subHeaderStyle;
        public static GUIStyle SubHeaderStyle
        {
            get
            {
                if (_subHeaderStyle == null)
                {
                    _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        padding = new RectOffset((int)SmallPadding, (int)SmallPadding, (int)SmallPadding, (int)SmallPadding),
                        normal = { textColor = TextColor }
                    };
                }
                return _subHeaderStyle;
            }
        }

        private static GUIStyle _helpBoxStyle;
        public static GUIStyle HelpBoxStyle
        {
            get
            {
                if (_helpBoxStyle == null)
                {
                    _helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset((int)MediumPadding, (int)MediumPadding, (int)MediumPadding, (int)MediumPadding),
                        margin = new RectOffset(0, 0, (int)SmallSpacing, (int)SmallSpacing)
                    };
                }
                return _helpBoxStyle;
            }
        }

        private static GUIStyle _boxStyle;
        public static GUIStyle BoxStyle
        {
            get
            {
                if (_boxStyle == null)
                {
                    _boxStyle = new GUIStyle(GUI.skin.box)
                    {
                        padding = new RectOffset((int)MediumPadding, (int)MediumPadding, (int)MediumPadding, (int)MediumPadding),
                        margin = new RectOffset(0, 0, (int)MediumSpacing, (int)MediumSpacing)
                    };
                }
                return _boxStyle;
            }
        }

        private static GUIStyle _richTextLabelStyle;
        public static GUIStyle RichTextLabelStyle
        {
            get
            {
                if (_richTextLabelStyle == null)
                {
                    _richTextLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        richText = true,
                        wordWrap = true
                    };
                }
                return _richTextLabelStyle;
            }
        }

        private static GUIStyle _centeredLabelStyle;
        public static GUIStyle CenteredLabelStyle
        {
            get
            {
                if (_centeredLabelStyle == null)
                {
                    _centeredLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _centeredLabelStyle;
            }
        }

        private static GUIStyle _buttonStyle;
        public static GUIStyle ButtonStyle
        {
            get
            {
                if (_buttonStyle == null)
                {
                    _buttonStyle = new GUIStyle(GUI.skin.button)
                    {
                        padding = new RectOffset((int)MediumPadding, (int)MediumPadding, (int)SmallPadding, (int)SmallPadding),
                        fixedHeight = ButtonHeight
                    };
                }
                return _buttonStyle;
            }
        }

        private static GUIStyle _foldoutStyle;
        public static GUIStyle FoldoutStyle
        {
            get
            {
                if (_foldoutStyle == null)
                {
                    _foldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _foldoutStyle;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a colored texture for use in GUI backgrounds.
        /// </summary>
        public static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Returns a color with adjusted alpha.
        /// </summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>
        /// Returns a lighter version of the color.
        /// </summary>
        public static Color Lighten(Color color, float amount = 0.2f)
        {
            return Color.Lerp(color, Color.white, amount);
        }

        /// <summary>
        /// Returns a darker version of the color.
        /// </summary>
        public static Color Darken(Color color, float amount = 0.2f)
        {
            return Color.Lerp(color, Color.black, amount);
        }

        /// <summary>
        /// Gets a color for a specific lifetime type.
        /// </summary>
        public static Color GetLifetimeColor(string lifetime)
        {
            return lifetime switch
            {
                "Singleton" => SingletonColor,
                "Transient" => TransientColor,
                "Scoped" => ScopedColor,
                _ => TextColor
            };
        }

        /// <summary>
        /// Gets an icon name for a specific lifetime type.
        /// </summary>
        public static string GetLifetimeIcon(string lifetime)
        {
            return lifetime switch
            {
                "Singleton" => "d_winbtn_mac_max", // Single instance icon
                "Transient" => "d_Refresh", // Multiple instances icon
                "Scoped" => "d_SceneAsset Icon", // Scoped icon
                _ => "d_Favorite"
            };
        }

        #endregion

        #region Message Box Styles

        /// <summary>
        /// Draws a colored help box with an icon.
        /// </summary>
        public static void DrawMessageBox(string message, MessageType messageType, bool richText = false)
        {
            var color = messageType switch
            {
                MessageType.Info => PrimaryColor,
                MessageType.Warning => WarningColor,
                MessageType.Error => ErrorColor,
                _ => SuccessColor
            };

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = WithAlpha(color, 0.2f);

            var style = richText ? RichTextLabelStyle : EditorStyles.label;
            EditorGUILayout.HelpBox(message, messageType);

            GUI.backgroundColor = previousColor;
        }

        #endregion

        #region Validation Colors

        public static Color GetValidationColor(bool isValid, bool hasWarnings = false)
        {
            if (!isValid) return ErrorColor;
            if (hasWarnings) return WarningColor;
            return SuccessColor;
        }

        #endregion
    }
}
