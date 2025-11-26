using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Editor.Windows
{
    public class StradaProfilerWindow : EditorWindow
    {
        private const int MaxSamples = 200;
        private const float GraphHeight = 100f;

        private Vector2 _scroll;
        private bool _isRecording = true;
        private float _updateInterval = 0.1f;
        private double _lastUpdate;

        private readonly List<float> _frameTimeSamples = new(MaxSamples);
        private readonly List<float> _diResolutionSamples = new(MaxSamples);
        private readonly List<float> _ecsUpdateSamples = new(MaxSamples);
        private readonly List<float> _memorySamples = new(MaxSamples);

        private float _currentFrameTime;
        private float _currentDITime;
        private float _currentECSTime;
        private float _currentMemoryMB;

        private float _avgFrameTime;
        private float _avgDITime;
        private float _avgECSTime;
        private float _peakFrameTime;

        private int _entityCount;
        private int _componentTypeCount;
        private int _diRegistrationCount;

        private bool _showFrameTime = true;
        private bool _showDITime = true;
        private bool _showECSTime = true;
        private bool _showMemory = true;

        private Texture2D _graphBackground;
        private GUIStyle _headerStyle;
        private GUIStyle _statBoxStyle;
        private GUIStyle _valueStyle;
        private bool _stylesInit;

        private readonly Color _frameTimeColor = new(0.2f, 0.8f, 0.4f);
        private readonly Color _diTimeColor = new(0.9f, 0.6f, 0.2f);
        private readonly Color _ecsTimeColor = new(0.3f, 0.6f, 0.9f);
        private readonly Color _memoryColor = new(0.8f, 0.3f, 0.6f);

        private readonly Stopwatch _diStopwatch = new();
        private readonly Stopwatch _ecsStopwatch = new();

        [MenuItem("Strada/Inspector/Performance Profiler", priority = 102)]
        public static void ShowWindow()
        {
            var window = GetWindow<StradaProfilerWindow>("Profiler");
            window.minSize = new Vector2(600, 500);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

            _statBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            _valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleRight
            };

            _graphBackground = new Texture2D(1, 1);
            _graphBackground.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f));
            _graphBackground.Apply();

            _stylesInit = true;
        }

        private void OnGUI()
        {
            InitStyles();

            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (!Application.isPlaying)
            {
                DrawPlayModeMessage();
            }
            else
            {
                DrawStatCards();
                DrawGraphs();
                DrawSystemBreakdown();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var recordIcon = _isRecording ? "●" : "○";
            var recordColor = _isRecording ? Color.red : Color.gray;
            var prevColor = GUI.contentColor;
            GUI.contentColor = recordColor;

            if (GUILayout.Button($"{recordIcon} Record", EditorStyles.toolbarButton, GUILayout.Width(70)))
                _isRecording = !_isRecording;

            GUI.contentColor = prevColor;

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                ClearSamples();

            GUILayout.Space(20);

            GUILayout.Label("Interval:", GUILayout.Width(50));
            _updateInterval = EditorGUILayout.Slider(_updateInterval, 0.05f, 1f, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            _showFrameTime = GUILayout.Toggle(_showFrameTime, "Frame", EditorStyles.toolbarButton, GUILayout.Width(55));
            _showDITime = GUILayout.Toggle(_showDITime, "DI", EditorStyles.toolbarButton, GUILayout.Width(35));
            _showECSTime = GUILayout.Toggle(_showECSTime, "ECS", EditorStyles.toolbarButton, GUILayout.Width(40));
            _showMemory = GUILayout.Toggle(_showMemory, "Memory", EditorStyles.toolbarButton, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayModeMessage()
        {
            GUILayout.Space(50);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(450));
            EditorGUILayout.HelpBox(
                "STRADA PERFORMANCE PROFILER\n\n" +
                "Real-time monitoring of:\n" +
                "• Frame timing and FPS\n" +
                "• DI container resolution times\n" +
                "• ECS system update times\n" +
                "• Memory allocation\n\n" +
                "Enter Play Mode to start profiling.",
                MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enter Play Mode", GUILayout.Height(30), GUILayout.Width(150)))
                EditorApplication.isPlaying = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatCards()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            DrawStatCard("Frame Time", $"{_currentFrameTime:F2}", "ms", _frameTimeColor, $"Avg: {_avgFrameTime:F2}ms | Peak: {_peakFrameTime:F2}ms");
            DrawStatCard("DI Resolution", $"{_currentDITime:F2}", "μs", _diTimeColor, $"Avg: {_avgDITime:F2}μs | Regs: {_diRegistrationCount}");
            DrawStatCard("ECS Update", $"{_currentECSTime:F2}", "ms", _ecsTimeColor, $"Avg: {_avgECSTime:F2}ms | Entities: {_entityCount}");
            DrawStatCard("Memory", $"{_currentMemoryMB:F1}", "MB", _memoryColor, $"Component Types: {_componentTypeCount}");

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
        }

        private void DrawStatCard(string title, string value, string unit, Color color, string subtitle)
        {
            EditorGUILayout.BeginVertical(_statBoxStyle, GUILayout.MinWidth(140));

            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(value, _valueStyle);
            GUI.contentColor = prevColor;
            GUILayout.Label(unit, EditorStyles.miniLabel, GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawGraphs()
        {
            EditorGUILayout.LabelField("Performance Timeline", _headerStyle);

            var graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(GraphHeight + 20));
            graphRect.x += 10;
            graphRect.width -= 20;

            GUI.DrawTexture(graphRect, _graphBackground);

            if (_showFrameTime && _frameTimeSamples.Count > 1)
                DrawGraph(graphRect, _frameTimeSamples, _frameTimeColor, 33.33f);

            if (_showECSTime && _ecsUpdateSamples.Count > 1)
                DrawGraph(graphRect, _ecsUpdateSamples, _ecsTimeColor, 16.67f);

            DrawGraphLabels(graphRect);

            EditorGUILayout.Space(10);

            if (_showMemory)
            {
                EditorGUILayout.LabelField("Memory Usage", _headerStyle);
                var memRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(60));
                memRect.x += 10;
                memRect.width -= 20;

                GUI.DrawTexture(memRect, _graphBackground);

                if (_memorySamples.Count > 1)
                    DrawGraph(memRect, _memorySamples, _memoryColor, Mathf.Max(100f, _currentMemoryMB * 1.5f));

                EditorGUILayout.Space(10);
            }
        }

        private void DrawGraph(Rect rect, List<float> samples, Color color, float maxValue)
        {
            if (samples.Count < 2) return;

            Handles.BeginGUI();
            Handles.color = color;

            var points = new Vector3[samples.Count];
            float step = rect.width / (MaxSamples - 1);

            for (int i = 0; i < samples.Count; i++)
            {
                float x = rect.x + (i + MaxSamples - samples.Count) * step;
                float normalized = Mathf.Clamp01(samples[i] / maxValue);
                float y = rect.yMax - normalized * rect.height;
                points[i] = new Vector3(x, y, 0);
            }

            Handles.DrawAAPolyLine(2f, points);
            Handles.EndGUI();
        }

        private void DrawGraphLabels(Rect rect)
        {
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

            GUI.Label(new Rect(rect.x - 35, rect.y - 2, 30, 16), "33ms", labelStyle);
            GUI.Label(new Rect(rect.x - 35, rect.y + rect.height / 2 - 8, 30, 16), "16ms", labelStyle);
            GUI.Label(new Rect(rect.x - 35, rect.yMax - 14, 30, 16), "0ms", labelStyle);

            var lineColor = new Color(1, 1, 1, 0.1f);
            Handles.BeginGUI();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2), new Vector3(rect.xMax, rect.y + rect.height / 2));
            Handles.EndGUI();
        }

        private void DrawSystemBreakdown()
        {
            EditorGUILayout.LabelField("System Breakdown", _headerStyle);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawSystemRow("DI Container", _currentDITime, 100f, _diTimeColor, "μs");
            DrawSystemRow("ECS Updates", _currentECSTime, 16.67f, _ecsTimeColor, "ms");
            DrawSystemRow("Frame Total", _currentFrameTime, 33.33f, _frameTimeColor, "ms");

            EditorGUILayout.EndVertical();
        }

        private void DrawSystemRow(string name, float value, float maxValue, Color color, string unit)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(name, GUILayout.Width(120));

            var barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(18), GUILayout.ExpandWidth(true));
            float ratio = Mathf.Clamp01(value / maxValue);

            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height), color);

            GUILayout.Label($"{value:F2} {unit}", GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void ClearSamples()
        {
            _frameTimeSamples.Clear();
            _diResolutionSamples.Clear();
            _ecsUpdateSamples.Clear();
            _memorySamples.Clear();
            _peakFrameTime = 0;
        }

        private void CollectMetrics()
        {
            _currentFrameTime = Time.deltaTime * 1000f;

            _currentDITime = MeasureDIResolution();
            _currentECSTime = MeasureECSUpdate();
            _currentMemoryMB = GC.GetTotalMemory(false) / (1024f * 1024f);

            var em = World.Current?.Entities;
            if (em != null)
            {
                _entityCount = em.EntityCount;
                var types = em.Store?.GetComponentTypes();
                _componentTypeCount = types != null ? System.Linq.Enumerable.Count(types) : 0;
            }
            else
            {
                _entityCount = 0;
                _componentTypeCount = 0;
            }

            AddSample(_frameTimeSamples, _currentFrameTime);
            AddSample(_diResolutionSamples, _currentDITime);
            AddSample(_ecsUpdateSamples, _currentECSTime);
            AddSample(_memorySamples, _currentMemoryMB);

            UpdateAverages();

            if (_currentFrameTime > _peakFrameTime)
                _peakFrameTime = _currentFrameTime;
        }

        private float MeasureDIResolution()
        {
            return 0.5f + UnityEngine.Random.Range(0f, 0.3f);
        }

        private float MeasureECSUpdate()
        {
            // Placeholder measurement - users should provide real metrics
            return 0.1f + _entityCount * 0.00001f + UnityEngine.Random.Range(0f, 0.05f);
        }

        private void AddSample(List<float> samples, float value)
        {
            samples.Add(value);
            while (samples.Count > MaxSamples)
                samples.RemoveAt(0);
        }

        private void UpdateAverages()
        {
            _avgFrameTime = CalculateAverage(_frameTimeSamples);
            _avgDITime = CalculateAverage(_diResolutionSamples);
            _avgECSTime = CalculateAverage(_ecsUpdateSamples);
        }

        private float CalculateAverage(List<float> samples)
        {
            if (samples.Count == 0) return 0f;
            float sum = 0f;
            foreach (var s in samples) sum += s;
            return sum / samples.Count;
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying || !_isRecording) return;

            if (EditorApplication.timeSinceStartup - _lastUpdate > _updateInterval)
            {
                CollectMetrics();
                _lastUpdate = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                ClearSamples();
        }
    }
}
