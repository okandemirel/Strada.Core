using System.Collections.Generic;
using System.Linq;
using Strada.Core.Logging;
using Strada.Core.Modules;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Settings
{
    /// <summary>
    /// Enterprise-grade custom editor for StradaLogSettings with professional UI
    /// displaying LogModule enum values organized into three tiers.
    /// </summary>
    [CustomEditor(typeof(StradaLogSettings))]
    public class StradaLogSettingsEditor : UnityEditor.Editor
    {
        private bool _generalFoldout = true;
        private bool _backgroundColorsFoldout = true;
        private bool _moduleSettingsFoldout = true;
        private bool _coreModulesFoldout = true;
        private bool _stradaModulesFoldout = true;
        private bool _gameModulesFoldout = true;

        private SerializedProperty _showLogs;
        private SerializedProperty _deepLogsEnabled;
        private SerializedProperty _maxLogEntries;
        private SerializedProperty _errorBackgroundColor;
        private SerializedProperty _warningBackgroundColor;
        private SerializedProperty _infoBackgroundColor;
        private SerializedProperty _deepLogBackgroundColor;

        // Cached styles
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _tierHeaderStyle;
        private static GUIStyle _moduleRowStyle;
        private static GUIStyle _moduleRowAltStyle;
        private static GUIStyle _countBadgeStyle;
        private static GUIStyle _lockedBadgeStyle;
        private static GUIStyle _editableBadgeStyle;
        private static GUIStyle _infoMessageStyle;
        private static GUIStyle _moduleNameStyle;
        private static GUIStyle _boxStyle;
        private static bool _stylesInitialized;

        // Colors
        private static readonly Color CoreTierColor = new Color(0.4f, 0.6f, 0.9f, 1f);
        private static readonly Color StradaTierColor = new Color(0.6f, 0.4f, 0.8f, 1f);
        private static readonly Color GameTierColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        private static readonly Color RowColorLight = new Color(0.85f, 0.85f, 0.85f, 0.3f);
        private static readonly Color RowColorDark = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        private static readonly Color RowColorAltLight = new Color(0.9f, 0.9f, 0.9f, 0.3f);
        private static readonly Color RowColorAltDark = new Color(0.25f, 0.25f, 0.25f, 0.3f);

        // Icons
        private static GUIContent _lockIcon;
        private static GUIContent _settingsIcon;
        private static GUIContent _paletteIcon;
        private static GUIContent _layersIcon;

        private void OnEnable()
        {
            _showLogs = serializedObject.FindProperty("_showLogs");
            _deepLogsEnabled = serializedObject.FindProperty("_deepLogsEnabled");
            _maxLogEntries = serializedObject.FindProperty("_maxLogEntries");
            _errorBackgroundColor = serializedObject.FindProperty("_errorBackgroundColor");
            _warningBackgroundColor = serializedObject.FindProperty("_warningBackgroundColor");
            _infoBackgroundColor = serializedObject.FindProperty("_infoBackgroundColor");
            _deepLogBackgroundColor = serializedObject.FindProperty("_deepLogBackgroundColor");

            var settings = (StradaLogSettings)target;
            settings.RegisterStoredModules();
            DiscoverAndRegisterGameModules();
        }

        private void DiscoverAndRegisterGameModules()
        {
            var guids = AssetDatabase.FindAssets("t:ModuleConfig");
            var moduleConfigs = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<ModuleConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(config => config != null && config.Enabled);

            foreach (var config in moduleConfigs)
            {
                LogModuleRegistry.RegisterGameModule(config.ModuleName);
            }
        }

        private static void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _sectionHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            _tierHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                padding = new RectOffset(4, 4, 4, 4)
            };

            _moduleRowStyle = new GUIStyle
            {
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 1, 1)
            };

            _moduleRowAltStyle = new GUIStyle(_moduleRowStyle);

            _countBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10,
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 2, 2)
            };

            _lockedBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = new Color(0.9f, 0.6f, 0.2f) },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _editableBadgeStyle = new GUIStyle(_lockedBadgeStyle)
            {
                normal = { textColor = new Color(0.4f, 0.8f, 0.4f) }
            };

            _infoMessageStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 10,
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(4, 4, 4, 8)
            };

            _moduleNameStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };

            _boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(1, 1, 1, 1),
                margin = new RectOffset(0, 0, 4, 4)
            };

            // Load icons
            _lockIcon = EditorGUIUtility.IconContent("IN LockButton on");
            _settingsIcon = EditorGUIUtility.IconContent("Settings");
            _paletteIcon = EditorGUIUtility.IconContent("ColorPicker.CycleSlider");
            _layersIcon = EditorGUIUtility.IconContent("Profiler.GlobalIllumination");

            if (_lockIcon == null || _lockIcon.image == null)
                _lockIcon = new GUIContent("L");
            if (_settingsIcon == null || _settingsIcon.image == null)
                _settingsIcon = new GUIContent("");
            if (_paletteIcon == null || _paletteIcon.image == null)
                _paletteIcon = new GUIContent("");
            if (_layersIcon == null || _layersIcon.image == null)
                _layersIcon = new GUIContent("");

            _stylesInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitializeStyles();

            EditorGUILayout.Space(4);

            DrawGeneralSection();
            EditorGUILayout.Space(8);

            DrawBackgroundColorsSection();
            EditorGUILayout.Space(8);

            DrawModuleSettingsSection();

            EditorGUILayout.Space(8);
            DrawFooter();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralSection()
        {
            var headerContent = new GUIContent(" General Settings", _settingsIcon.image);
            _generalFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_generalFoldout, headerContent);

            if (_generalFoldout)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.Space(4);

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_showLogs, new GUIContent("Show Logs", "Toggle log output to the Unity console."));
                EditorGUILayout.PropertyField(_deepLogsEnabled, new GUIContent("Deep Logs Enabled", "Enable detailed flow logs for debugging."));
                EditorGUILayout.PropertyField(_maxLogEntries, new GUIContent("Max Log Entries", "Maximum number of log entries to store in the buffer."));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBackgroundColorsSection()
        {
            var headerContent = new GUIContent(" Background Colors", _paletteIcon.image);
            _backgroundColorsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_backgroundColorsFoldout, headerContent);

            if (_backgroundColorsFoldout)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.Space(4);

                DrawColorRow("Error", _errorBackgroundColor, new Color(0.8f, 0.2f, 0.2f));
                DrawColorRow("Warning", _warningBackgroundColor, new Color(0.8f, 0.8f, 0.2f));
                DrawColorRow("Info", _infoBackgroundColor, new Color(0.5f, 0.5f, 0.5f));
                DrawColorRow("Deep Log", _deepLogBackgroundColor, new Color(0.2f, 0.4f, 0.8f));

                EditorGUILayout.Space(4);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorRow(string label, SerializedProperty property, Color indicatorColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            // Color indicator
            var indicatorRect = GUILayoutUtility.GetRect(4, 18, GUILayout.Width(4));
            EditorGUI.DrawRect(indicatorRect, indicatorColor);

            GUILayout.Space(8);
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            EditorGUILayout.PropertyField(property, GUIContent.none);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleSettingsSection()
        {
            var settings = (StradaLogSettings)target;

            var coreModules = LogModuleRegistry.GetModulesByTier(LogModuleTier.StradaCore);
            var stradaModules = LogModuleRegistry.GetModulesByTier(LogModuleTier.StradaModule);
            var gameModules = LogModuleRegistry.GetModulesByTier(LogModuleTier.Game);

            var totalModules = coreModules.Count + stradaModules.Count + gameModules.Count;
            var headerContent = new GUIContent($" Module Settings ({totalModules})", _layersIcon.image);

            _moduleSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_moduleSettingsFoldout, headerContent);

            if (_moduleSettingsFoldout)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.Space(4);

                // Strada Core Tier
                DrawTierSection(
                    ref _coreModulesFoldout,
                    "Strada Core",
                    CoreTierColor,
                    coreModules,
                    settings,
                    isLocked: true,
                    description: "Framework internals (DI, ECS, Sync, etc.)"
                );

                EditorGUILayout.Space(4);

                // Strada Modules Tier
                DrawTierSection(
                    ref _stradaModulesFoldout,
                    "Strada Modules",
                    StradaTierColor,
                    stradaModules,
                    settings,
                    isLocked: true,
                    description: "Strada framework modules (Screen, etc.)"
                );

                EditorGUILayout.Space(4);

                // Game Tier
                DrawTierSection(
                    ref _gameModulesFoldout,
                    "Game Modules",
                    GameTierColor,
                    gameModules,
                    settings,
                    isLocked: false,
                    description: "Project-specific modules"
                );

                EditorGUILayout.Space(4);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTierSection(
            ref bool foldout,
            string title,
            Color tierColor,
            List<LogModule> modules,
            StradaLogSettings settings,
            bool isLocked,
            string description)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Tier Header
            EditorGUILayout.BeginHorizontal();

            // Color bar
            var colorBarRect = GUILayoutUtility.GetRect(4, 20, GUILayout.Width(4));
            EditorGUI.DrawRect(colorBarRect, tierColor);

            GUILayout.Space(8);

            // Foldout with title
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldout);

            GUILayout.FlexibleSpace();

            // Count badge
            DrawCountBadge(modules.Count, tierColor);

            GUILayout.Space(4);

            // Locked/Editable badge
            if (isLocked)
            {
                GUILayout.Label("LOCKED", _lockedBadgeStyle);
            }
            else
            {
                GUILayout.Label("EDITABLE", _editableBadgeStyle);
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                // Description
                EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
                EditorGUILayout.Space(4);

                if (modules.Count == 0)
                {
                    EditorGUILayout.LabelField("No modules registered in this tier.", _infoMessageStyle);
                }
                else
                {
                    // Table header
                    DrawModuleTableHeader(isLocked);

                    // Separator
                    DrawSeparator();

                    // Module rows
                    for (int i = 0; i < modules.Count; i++)
                    {
                        DrawModuleRow(settings, modules[i], isLocked, i % 2 == 1, tierColor);
                    }

                    // Action buttons for editable tier
                    if (!isLocked && modules.Count > 0)
                    {
                        EditorGUILayout.Space(8);
                        DrawModuleButtons(settings, modules);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCountBadge(int count, Color color)
        {
            var badgeContent = new GUIContent(count.ToString());
            var badgeSize = _countBadgeStyle.CalcSize(badgeContent);
            badgeSize.x = Mathf.Max(badgeSize.x, 24);

            var badgeRect = GUILayoutUtility.GetRect(badgeSize.x, 18);
            var darkerColor = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);

            // Draw rounded badge background
            EditorGUI.DrawRect(badgeRect, darkerColor);
            GUI.Label(badgeRect, badgeContent, _countBadgeStyle);
        }

        private void DrawModuleTableHeader(bool isLocked)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            if (isLocked)
            {
                GUILayout.Space(24); // Space for lock icon
            }

            EditorGUILayout.LabelField("Module", EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
            EditorGUILayout.LabelField("Visible", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Color", EditorStyles.miniBoldLabel, GUILayout.Width(80));

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            rect.x += 8;
            rect.width -= 16;
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
        }

        private void DrawModuleRow(StradaLogSettings settings, LogModule module, bool isLocked, bool isAlt, Color tierColor)
        {
            var rowColor = isAlt
                ? (EditorGUIUtility.isProSkin ? RowColorAltDark : RowColorAltLight)
                : (EditorGUIUtility.isProSkin ? RowColorDark : RowColorLight);

            var rowRect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rowRect, rowColor);

            GUILayout.Space(8);

            // Lock icon for locked tiers
            if (isLocked)
            {
                var iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                GUI.Label(iconRect, _lockIcon);
                GUILayout.Space(4);
            }

            // Module color indicator
            var moduleColor = settings.GetModuleColor(module);
            var colorIndicatorRect = GUILayoutUtility.GetRect(12, 18, GUILayout.Width(12));
            colorIndicatorRect.y += 2;
            colorIndicatorRect.height -= 4;
            EditorGUI.DrawRect(colorIndicatorRect, moduleColor);

            GUILayout.Space(8);

            // Module name
            var displayName = LogModuleRegistry.GetDisplayName(module);
            EditorGUILayout.LabelField(displayName, _moduleNameStyle, GUILayout.MinWidth(100));

            // Visibility toggle
            bool visible = settings.IsModuleVisible(module);
            EditorGUI.BeginDisabledGroup(isLocked);
            var toggleRect = GUILayoutUtility.GetRect(50, 18, GUILayout.Width(50));
            bool newVisible = EditorGUI.Toggle(toggleRect, visible);
            EditorGUI.EndDisabledGroup();

            if (!isLocked && newVisible != visible)
            {
                Undo.RecordObject(target, "Change Module Visibility");
                settings.SetModuleVisible(module, newVisible);
                EditorUtility.SetDirty(target);
            }

            // Color picker
            var colorRect = GUILayoutUtility.GetRect(60, 18, GUILayout.Width(60));
            Color newColor = EditorGUI.ColorField(colorRect, GUIContent.none, moduleColor, false, true, false);
            if (newColor != moduleColor)
            {
                Undo.RecordObject(target, "Change Module Color");
                settings.SetModuleColor(module, newColor);
                EditorUtility.SetDirty(target);
            }

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(1);
        }

        private void DrawModuleButtons(StradaLogSettings settings, List<LogModule> modules)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(12, 12, 4, 4)
            };

            if (GUILayout.Button("Show All", buttonStyle, GUILayout.Width(80)))
            {
                Undo.RecordObject(target, "Show All Game Modules");
                foreach (var module in modules)
                {
                    settings.SetModuleVisible(module, true);
                }
                EditorUtility.SetDirty(target);
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Hide All", buttonStyle, GUILayout.Width(80)))
            {
                Undo.RecordObject(target, "Hide All Game Modules");
                foreach (var module in modules)
                {
                    settings.SetModuleVisible(module, false);
                }
                EditorUtility.SetDirty(target);
            }

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.4f, 0.4f, 0.4f) }
            };

            GUILayout.Label("Strada Logging System", footerStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}
