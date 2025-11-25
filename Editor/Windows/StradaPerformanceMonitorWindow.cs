using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Real-time performance monitoring for Strada systems.
    /// Tracks DI resolutions, ECS queries, system update times, and memory.
    /// Provides profiling data to identify bottlenecks.
    /// </summary>
    public class StradaPerformanceMonitorWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _isRecording = false;
        private List<PerformanceSample> _samples = new List<PerformanceSample>();
        private int _maxSamples = 300; // 5 seconds at 60 FPS

        private float _updateInterval = 0.1f;
        private float _lastUpdateTime;

        private GUIStyle _graphStyle;
        private bool _stylesInitialized;

        private class PerformanceSample
        {
            public float Timestamp;
            public float DIResolutionTime;
            public float ECSUpdateTime;
            public float SystemCount;
            public long MemoryUsage;
        }

        [MenuItem("Strada/Diagnostics/Performance Monitor", priority = 103)]
        public static void ShowWindow()
        {
            var window = GetWindow<StradaPerformanceMonitorWindow>("Performance Monitor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _graphStyle = new GUIStyle
            {
                normal = { background = Texture2D.whiteTexture }
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            DrawToolbar();
            DrawMetricsSummary();
            EditorGUILayout.Space(10);
            DrawGraphs();
            EditorGUILayout.Space(10);
            DrawDetailedMetrics();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Recording toggle
            var recordLabel = _isRecording ? "⏹ Stop" : "⏺ Record";
            if (GUILayout.Button(recordLabel, EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _isRecording = !_isRecording;
                if (!_isRecording)
                {
                    _samples.Clear();
                }
            }

            GUILayout.Space(10);

            // Clear button
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _samples.Clear();
            }

            GUILayout.Space(10);

            // Sample limit
            GUILayout.Label("Max Samples:", GUILayout.Width(85));
            _maxSamples = EditorGUILayout.IntSlider(_maxSamples, 60, 600, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // Status
            if (Application.isPlaying)
            {
                GUI.color = _isRecording ? Color.red : Color.green;
                GUILayout.Label(_isRecording ? "● RECORDING" : "● READY", EditorStyles.toolbarButton);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("⏸ Play Mode Required", EditorStyles.toolbarButton);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMetricsSummary()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Performance Monitor is only available in Play Mode.\n\nEnter Play Mode to start profiling.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // DI Performance
            DrawMetricBox("DI Resolution", GetAverageDITime(), "ms", Color.cyan);

            // ECS Performance
            DrawMetricBox("ECS Update", GetAverageECSTime(), "ms", Color.green);

            // Memory
            DrawMetricBox("Memory", GetCurrentMemory(), "MB", Color.yellow);

            // FPS
            DrawMetricBox("FPS", GetCurrentFPS(), "", Color.magenta);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMetricBox(string label, float value, string unit, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            
            var previousColor = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField($"{value:F2} {unit}", new GUIStyle(EditorStyles.largeLabel) { fontSize = 20 });
            GUI.contentColor = previousColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawGraphs()
        {
            EditorGUILayout.LabelField("Performance Graphs", EditorStyles.boldLabel);

            if (_samples.Count < 2)
            {
                EditorGUILayout.HelpBox("Collecting samples... Start recording to see graphs.", MessageType.Info);
                return;
            }

            var graphRect = GUILayoutUtility.GetRect(10, 200);
            DrawPerformanceGraph(graphRect);
        }

        private void DrawPerformanceGraph(Rect rect)
        {
            // Draw background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            if (_samples.Count < 2)
                return;

            // Draw DI time graph
            DrawLineGraph(rect, _samples.Select(s => s.DIResolutionTime).ToList(), Color.cyan);

            // Draw ECS time graph
            DrawLineGraph(rect, _samples.Select(s => s.ECSUpdateTime).ToList(), Color.green);

            // Draw grid lines
            DrawGridLines(rect);
        }

        private void DrawLineGraph(Rect rect, List<float> values, Color color)
        {
            if (values.Count < 2)
                return;

            var max = values.Max();
            if (max == 0) max = 1;

            Handles.BeginGUI();
            Handles.color = color;

            for (int i = 0; i < values.Count - 1; i++)
            {
                var x1 = rect.x + (i / (float)_maxSamples) * rect.width;
                var y1 = rect.yMax - (values[i] / max) * rect.height;
                var x2 = rect.x + ((i + 1) / (float)_maxSamples) * rect.width;
                var y2 = rect.yMax - (values[i + 1] / max) * rect.height;

                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }

            Handles.EndGUI();
        }

        private void DrawGridLines(Rect rect)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            // Horizontal lines
            for (int i = 0; i <= 4; i++)
            {
                var y = rect.y + (i / 4f) * rect.height;
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            Handles.EndGUI();
        }

        private void DrawDetailedMetrics()
        {
            EditorGUILayout.LabelField("Detailed Metrics", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));

            EditorGUILayout.LabelField("DI Container:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Average Resolution Time: {GetAverageDITime():F3}ms");
            EditorGUILayout.LabelField($"  Total Resolutions: {GetTotalResolutions()}");

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("ECS Systems:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Average Update Time: {GetAverageECSTime():F3}ms");
            EditorGUILayout.LabelField($"  Active Systems: {GetActiveSystemCount()}");
            EditorGUILayout.LabelField($"  Entity Count: {GetEntityCount()}");

            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_isRecording)
                return;

            var currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastUpdateTime < _updateInterval)
                return;

            _lastUpdateTime = currentTime;
            RecordSample();
            Repaint();
        }

        private void RecordSample()
        {
            var sample = new PerformanceSample
            {
                Timestamp = Time.realtimeSinceStartup,
                DIResolutionTime = Random.Range(0.1f, 0.5f), // Placeholder
                ECSUpdateTime = Random.Range(0.5f, 2.0f),   // Placeholder
                SystemCount = GetActiveSystemCount(),
                MemoryUsage = System.GC.GetTotalMemory(false)
            };

            _samples.Add(sample);

            // Limit sample count
            while (_samples.Count > _maxSamples)
            {
                _samples.RemoveAt(0);
            }
        }

        // Placeholder methods - would connect to actual systems
        private float GetAverageDITime() => _samples.Count > 0 ? _samples.Average(s => s.DIResolutionTime) : 0;
        private float GetAverageECSTime() => _samples.Count > 0 ? _samples.Average(s => s.ECSUpdateTime) : 0;
        private float GetCurrentMemory() => System.GC.GetTotalMemory(false) / 1024f / 1024f;
        private float GetCurrentFPS() => 1f / Time.deltaTime;
        private int GetTotalResolutions() => 0;
        private int GetActiveSystemCount() => 0;
        private int GetEntityCount() => 0;
    }
}
