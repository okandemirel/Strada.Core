using System;
using System.Collections.Generic;
using Strada.Core.Logging;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Settings
{
    /// <summary>
    /// Settings provider for StradaLog configuration in Project Settings.
    /// Provides UI for configuring log output, deep logging, colors, and module visibility.
    /// </summary>
    public class StradaLogSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Strada/Logging";

        private SerializedObject _serializedSettings;
        private bool _generalFoldout = true;
        private bool _colorsFoldout = true;
        private bool _visibilityFoldout = true;
        private Vector2 _scrollPosition;

        public StradaLogSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawGeneralSection();

            EditorGUILayout.Space(10);

            DrawColorsSection();

            EditorGUILayout.Space(10);

            DrawVisibilitySection();

            EditorGUILayout.Space(20);

            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGeneralSection()
        {
            _generalFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_generalFoldout, "General Settings");

            if (_generalFoldout)
            {
                EditorGUI.indentLevel++;

                var showLogs = EditorGUILayout.Toggle(
                    new GUIContent("Show Logs in Console", "Output StradaLog messages to Unity's console window."),
                    StradaLogSettings.Instance.ShowLogs);

                if (showLogs != StradaLogSettings.Instance.ShowLogs)
                {
                    StradaLogSettings.Instance.ShowLogs = showLogs;
                    MarkSettingsDirty();
                }

                EditorGUILayout.Space(5);

                var deepLogs = EditorGUILayout.Toggle(
                    new GUIContent("Enable Deep Logs", "Enable detailed flow logging for debugging. Deep logs provide more granular information about system execution."),
                    StradaLogSettings.Instance.DeepLogsEnabled);

                if (deepLogs != StradaLogSettings.Instance.DeepLogsEnabled)
                {
                    StradaLogSettings.Instance.DeepLogsEnabled = deepLogs;
                    MarkSettingsDirty();
                }

                EditorGUILayout.Space(5);

                var maxEntries = EditorGUILayout.IntSlider(
                    new GUIContent("Max Log Entries", "Maximum number of log entries to store in the buffer."),
                    StradaLogSettings.Instance.MaxLogEntries, 100, 10000);

                if (maxEntries != StradaLogSettings.Instance.MaxLogEntries)
                {
                    StradaLogSettings.Instance.MaxLogEntries = maxEntries;
                    MarkSettingsDirty();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorsSection()
        {
            _colorsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_colorsFoldout, "Colors");

            if (_colorsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Background Colors", EditorStyles.boldLabel);

                DrawColorField("Error Background", StradaLogSettings.Instance.ErrorBackgroundColor,
                    color => SetBackgroundColor("_errorBackgroundColor", color));

                DrawColorField("Warning Background", StradaLogSettings.Instance.WarningBackgroundColor,
                    color => SetBackgroundColor("_warningBackgroundColor", color));

                DrawColorField("Info Background", StradaLogSettings.Instance.InfoBackgroundColor,
                    color => SetBackgroundColor("_infoBackgroundColor", color));

                DrawColorField("Deep Log Background", StradaLogSettings.Instance.DeepLogBackgroundColor,
                    color => SetBackgroundColor("_deepLogBackgroundColor", color));

                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Module Colors", EditorStyles.boldLabel);

                var moduleValues = Enum.GetValues(typeof(LogModule));
                for (int i = 0; i < moduleValues.Length; i++)
                {
                    var module = (LogModule)moduleValues.GetValue(i);
                    var currentColor = StradaLogSettings.Instance.GetModuleColor(module);

                    var newColor = EditorGUILayout.ColorField(
                        new GUIContent(module.ToString()),
                        currentColor);

                    if (newColor != currentColor)
                    {
                        StradaLogSettings.Instance.SetModuleColor(module, newColor);
                        MarkSettingsDirty();
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorField(string label, Color currentColor, Action<Color> setter)
        {
            var newColor = EditorGUILayout.ColorField(new GUIContent(label), currentColor);
            if (newColor != currentColor)
            {
                setter(newColor);
                MarkSettingsDirty();
            }
        }

        private void SetBackgroundColor(string fieldName, Color color)
        {
            var field = typeof(StradaLogSettings).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(StradaLogSettings.Instance, color);
            }
        }

        private void DrawVisibilitySection()
        {
            _visibilityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_visibilityFoldout, "Module Visibility");

            if (_visibilityFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Toggle which modules appear as filter tabs in the Log Viewer window.",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                var moduleValues = Enum.GetValues(typeof(LogModule));
                for (int i = 0; i < moduleValues.Length; i++)
                {
                    var module = (LogModule)moduleValues.GetValue(i);
                    var isVisible = StradaLogSettings.Instance.IsModuleVisible(module);

                    var newVisible = EditorGUILayout.Toggle(
                        new GUIContent(module.ToString()),
                        isVisible);

                    if (newVisible != isVisible)
                    {
                        StradaLogSettings.Instance.SetModuleVisible(module, newVisible);
                        MarkSettingsDirty();
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Show All", GUILayout.Width(80)))
                {
                    for (int i = 0; i < moduleValues.Length; i++)
                    {
                        var module = (LogModule)moduleValues.GetValue(i);
                        StradaLogSettings.Instance.SetModuleVisible(module, true);
                    }
                    MarkSettingsDirty();
                }

                if (GUILayout.Button("Hide All", GUILayout.Width(80)))
                {
                    for (int i = 0; i < moduleValues.Length; i++)
                    {
                        var module = (LogModule)moduleValues.GetValue(i);
                        StradaLogSettings.Instance.SetModuleVisible(module, false);
                    }
                    MarkSettingsDirty();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all StradaLog settings to their defaults?",
                    "Reset", "Cancel"))
                {
                    StradaLogSettings.Instance.ResetToDefaults();
                    MarkSettingsDirty();
                }
            }

            if (GUILayout.Button("Open Log Viewer", GUILayout.Height(25)))
            {
                Windows.StradaLogWindow.ShowWindow();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "StradaLogSettings asset location:\n" +
                "Create via: Assets > Create > Strada > Log Settings\n" +
                "Place in a Resources folder for automatic loading.",
                MessageType.Info);
        }

        private void MarkSettingsDirty()
        {
            EditorUtility.SetDirty(StradaLogSettings.Instance);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new StradaLogSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                keywords = new HashSet<string>(new[]
                {
                    "Strada", "Log", "Logging", "Debug", "Console", "Deep", "Module"
                })
            };
        }
    }
}
