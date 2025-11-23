using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for monitoring Strada commands and events in real-time.
    /// Shows command queue, event history, and communication flow between MVCS and ECS.
    /// </summary>
    public class CommandEventMonitorWindow : EditorWindow
    {
        private enum MonitorTab
        {
            Commands,
            Events,
            Statistics
        }

        private MonitorTab _currentTab = MonitorTab.Commands;
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private bool _pauseCapture = false;

        private List<CommandLogEntry> _commandLog = new List<CommandLogEntry>();
        private List<EventLogEntry> _eventLog = new List<EventLogEntry>();
        private int _maxLogEntries = 100;

        private Dictionary<string, int> _commandStats = new Dictionary<string, int>();
        private Dictionary<string, int> _eventStats = new Dictionary<string, int>();

        [MenuItem("Window/Strada/Command & Event Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<CommandEventMonitorWindow>("Commands & Events");
            window.minSize = new Vector2(700, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_autoRefresh && Application.isPlaying && !_pauseCapture)
            {
                CaptureData();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawTabButtons();

            switch (_currentTab)
            {
                case MonitorTab.Commands:
                    DrawCommandsTab();
                    break;

                case MonitorTab.Events:
                    DrawEventsTab();
                    break;

                case MonitorTab.Statistics:
                    DrawStatisticsTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(_pauseCapture ? "Resume" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _pauseCapture = !_pauseCapture;
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ClearAll();
            }

            GUILayout.Space(10);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Commands: {_commandLog.Count}", EditorStyles.miniLabel);
            GUILayout.Label($"Events: {_eventLog.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_currentTab == MonitorTab.Commands, "Commands", EditorStyles.toolbarButton))
                _currentTab = MonitorTab.Commands;

            if (GUILayout.Toggle(_currentTab == MonitorTab.Events, "Events", EditorStyles.toolbarButton))
                _currentTab = MonitorTab.Events;

            if (GUILayout.Toggle(_currentTab == MonitorTab.Statistics, "Statistics", EditorStyles.toolbarButton))
                _currentTab = MonitorTab.Statistics;

            EditorGUILayout.EndHorizontal();
            StradaEditorGUI.Space();
        }

        private void DrawCommandsTab()
        {
            StradaEditorGUI.DrawHeader("Command Queue", StradaEditorIcons.ArrowRightIcon);

            if (!Application.isPlaying)
            {
                StradaEditorGUI.DrawHelpBox("Enter Play Mode to monitor commands.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var entry in _commandLog.AsEnumerable().Reverse())
            {
                DrawCommandEntry(entry);
            }

            if (_commandLog.Count == 0)
            {
                GUILayout.Label("No commands captured yet.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCommandEntry(CommandLogEntry entry)
        {
            var age = (float)(EditorApplication.timeSinceStartup - entry.Timestamp);
            var alpha = Mathf.Clamp01(1f - age / 5f);

            var backgroundColor = Color.Lerp(GUI.backgroundColor, StradaEditorStyles.PrimaryColor, alpha * 0.3f);
            GUI.backgroundColor = backgroundColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(StradaEditorIcons.ArrowRightIcon, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(entry.CommandType, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            var timeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");
            GUILayout.Label(timeStr, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(entry.Details))
            {
                GUILayout.Label(entry.Details, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawEventsTab()
        {
            StradaEditorGUI.DrawHeader("Event History", StradaEditorIcons.InfoIcon);

            if (!Application.isPlaying)
            {
                StradaEditorGUI.DrawHelpBox("Enter Play Mode to monitor events.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var entry in _eventLog.AsEnumerable().Reverse())
            {
                DrawEventEntry(entry);
            }

            if (_eventLog.Count == 0)
            {
                GUILayout.Label("No events captured yet.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEventEntry(EventLogEntry entry)
        {
            var age = (float)(EditorApplication.timeSinceStartup - entry.Timestamp);
            var alpha = Mathf.Clamp01(1f - age / 5f);

            var backgroundColor = Color.Lerp(GUI.backgroundColor, StradaEditorStyles.SuccessColor, alpha * 0.3f);
            GUI.backgroundColor = backgroundColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(StradaEditorIcons.InfoIcon, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(entry.EventType, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            var timeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");
            GUILayout.Label(timeStr, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"Subscribers: {entry.SubscriberCount}", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(entry.Details))
            {
                GUILayout.Label(entry.Details, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawStatisticsTab()
        {
            StradaEditorGUI.DrawHeader("Communication Statistics", StradaEditorIcons.PerformanceIcon);

            EditorGUILayout.BeginHorizontal();

            DrawCommandStatistics();
            DrawEventStatistics();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCommandStatistics()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));

            StradaEditorGUI.DrawSubHeader("Command Statistics", StradaEditorIcons.ArrowRightIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            StradaEditorGUI.DrawReadOnlyProperty("Total Commands", _commandLog.Count.ToString());
            StradaEditorGUI.DrawReadOnlyProperty("Unique Types", _commandStats.Count.ToString());

            StradaEditorGUI.Space();

            if (_commandStats.Count > 0)
            {
                GUILayout.Label("Top Commands:", EditorStyles.boldLabel);

                var sorted = _commandStats.OrderByDescending(kvp => kvp.Value).Take(5);
                foreach (var kvp in sorted)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(kvp.Key, GUILayout.Width(200));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(kvp.Value.ToString(), EditorStyles.boldLabel, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawEventStatistics()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));

            StradaEditorGUI.DrawSubHeader("Event Statistics", StradaEditorIcons.InfoIcon);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            StradaEditorGUI.DrawReadOnlyProperty("Total Events", _eventLog.Count.ToString());
            StradaEditorGUI.DrawReadOnlyProperty("Unique Types", _eventStats.Count.ToString());

            StradaEditorGUI.Space();

            if (_eventStats.Count > 0)
            {
                GUILayout.Label("Top Events:", EditorStyles.boldLabel);

                var sorted = _eventStats.OrderByDescending(kvp => kvp.Value).Take(5);
                foreach (var kvp in sorted)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(kvp.Key, GUILayout.Width(200));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(kvp.Value.ToString(), EditorStyles.boldLabel, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void CaptureData()
        {
        }

        private void ClearAll()
        {
            _commandLog.Clear();
            _eventLog.Clear();
            _commandStats.Clear();
            _eventStats.Clear();
        }

        private class CommandLogEntry
        {
            public double Timestamp { get; set; }
            public string CommandType { get; set; }
            public string Details { get; set; }
        }

        private class EventLogEntry
        {
            public double Timestamp { get; set; }
            public string EventType { get; set; }
            public int SubscriberCount { get; set; }
            public string Details { get; set; }
        }
    }
}
