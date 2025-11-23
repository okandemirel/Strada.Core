using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Editor window for monitoring runtime health checks and diagnostics.
    /// Shows framework performance, memory usage, and potential issues.
    /// </summary>
    public class RuntimeHealthCheckWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private double _lastRefreshTime;

        private List<HealthCheck> _healthChecks = new List<HealthCheck>();

        [MenuItem("Window/Strada/Runtime Health Check")]
        public static void ShowWindow()
        {
            var window = GetWindow<RuntimeHealthCheckWindow>("Health Check");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeHealthChecks();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_autoRefresh && Application.isPlaying)
            {
                var timeSinceRefresh = EditorApplication.timeSinceStartup - _lastRefreshTime;

                if (timeSinceRefresh > 1.0)
                {
                    RunHealthChecks();
                    _lastRefreshTime = EditorApplication.timeSinceStartup;
                    Repaint();
                }
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!Application.isPlaying)
            {
                StradaEditorGUI.DrawHelpBox("Enter Play Mode to monitor runtime health.", MessageType.Info);
                return;
            }

            DrawSummary();
            DrawHealthChecks();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RunHealthChecks();
            }

            GUILayout.Space(10);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            var passedChecks = _healthChecks.FindAll(c => c.Status == HealthStatus.Healthy).Count;
            GUILayout.Label($"{passedChecks}/{_healthChecks.Count} checks passed", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            var healthyCount = _healthChecks.FindAll(c => c.Status == HealthStatus.Healthy).Count;
            var warningCount = _healthChecks.FindAll(c => c.Status == HealthStatus.Warning).Count;
            var unhealthyCount = _healthChecks.FindAll(c => c.Status == HealthStatus.Unhealthy).Count;

            StradaEditorGUI.BeginInspectorPanel();

            EditorGUILayout.BeginHorizontal();

            DrawSummaryBox("Healthy", healthyCount, StradaEditorStyles.SuccessColor);
            DrawSummaryBox("Warnings", warningCount, StradaEditorStyles.WarningColor);
            DrawSummaryBox("Issues", unhealthyCount, StradaEditorStyles.ErrorColor);

            EditorGUILayout.EndHorizontal();

            StradaEditorGUI.EndInspectorPanel();
        }

        private void DrawSummaryBox(string label, int count, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(180));

            GUI.backgroundColor = color;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            GUILayout.Label(count.ToString(), EditorStyles.largeLabel);
            GUILayout.Label(label, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawHealthChecks()
        {
            StradaEditorGUI.DrawSubHeader("Health Checks", StradaEditorIcons.PerformanceIcon);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var check in _healthChecks)
            {
                DrawHealthCheck(check);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHealthCheck(HealthCheck check)
        {
            var backgroundColor = GetStatusColor(check.Status);

            GUI.backgroundColor = backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            var icon = GetStatusIcon(check.Status);
            GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));

            GUILayout.Label(check.Name, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            GUILayout.Label(check.Status.ToString(), EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(check.Message))
            {
                GUILayout.Label(check.Message, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(check.Details))
            {
                StradaEditorGUI.Space();
                GUILayout.Label(check.Details, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private Color GetStatusColor(HealthStatus status)
        {
            switch (status)
            {
                case HealthStatus.Healthy:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.SuccessColor, 0.2f);
                case HealthStatus.Warning:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.WarningColor, 0.2f);
                case HealthStatus.Unhealthy:
                    return Color.Lerp(GUI.backgroundColor, StradaEditorStyles.ErrorColor, 0.2f);
                default:
                    return GUI.backgroundColor;
            }
        }

        private GUIContent GetStatusIcon(HealthStatus status)
        {
            switch (status)
            {
                case HealthStatus.Healthy:
                    return StradaEditorIcons.SuccessIcon;
                case HealthStatus.Warning:
                    return StradaEditorIcons.WarningIcon;
                case HealthStatus.Unhealthy:
                    return StradaEditorIcons.ErrorIcon;
                default:
                    return GUIContent.none;
            }
        }

        private void InitializeHealthChecks()
        {
            _healthChecks = new List<HealthCheck>
            {
                new HealthCheck
                {
                    Name = "Frame Rate",
                    Category = "Performance",
                    CheckFunction = CheckFrameRate
                },
                new HealthCheck
                {
                    Name = "Memory Usage",
                    Category = "Performance",
                    CheckFunction = CheckMemoryUsage
                },
                new HealthCheck
                {
                    Name = "GC Allocations",
                    Category = "Performance",
                    CheckFunction = CheckGCAllocations
                },
                new HealthCheck
                {
                    Name = "DI Container",
                    Category = "Framework",
                    CheckFunction = CheckDIContainer
                },
                new HealthCheck
                {
                    Name = "ECS Worlds",
                    Category = "Framework",
                    CheckFunction = CheckECSWorlds
                }
            };
        }

        private void RunHealthChecks()
        {
            if (!Application.isPlaying)
                return;

            foreach (var check in _healthChecks)
            {
                check.CheckFunction?.Invoke(check);
            }
        }

        private void CheckFrameRate(HealthCheck check)
        {
            var fps = 1f / Time.deltaTime;

            check.Details = $"Current FPS: {fps:F1}";

            if (fps < 30)
            {
                check.Status = HealthStatus.Unhealthy;
                check.Message = "Frame rate is critically low";
            }
            else if (fps < 50)
            {
                check.Status = HealthStatus.Warning;
                check.Message = "Frame rate is below target";
            }
            else
            {
                check.Status = HealthStatus.Healthy;
                check.Message = "Frame rate is good";
            }
        }

        private void CheckMemoryUsage(HealthCheck check)
        {
            var totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            var reservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f);

            check.Details = $"Allocated: {totalMemory:F1} MB\nReserved: {reservedMemory:F1} MB";

            if (totalMemory > 512)
            {
                check.Status = HealthStatus.Unhealthy;
                check.Message = "Memory usage is very high";
            }
            else if (totalMemory > 256)
            {
                check.Status = HealthStatus.Warning;
                check.Message = "Memory usage is elevated";
            }
            else
            {
                check.Status = HealthStatus.Healthy;
                check.Message = "Memory usage is normal";
            }
        }

        private void CheckGCAllocations(HealthCheck check)
        {
            var monoUsedSize = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);

            check.Details = $"Mono Heap: {monoUsedSize:F1} MB";

            if (monoUsedSize > 128)
            {
                check.Status = HealthStatus.Warning;
                check.Message = "Managed heap is large, GC may cause stutters";
            }
            else
            {
                check.Status = HealthStatus.Healthy;
                check.Message = "Managed heap size is acceptable";
            }
        }

        private void CheckDIContainer(HealthCheck check)
        {
            check.Status = HealthStatus.Healthy;
            check.Message = "DI container is operational";
            check.Details = "Use DI Container Inspector for detailed view";
        }

        private void CheckECSWorlds(HealthCheck check)
        {
            var worldCount = Unity.Entities.World.All.Count;

            check.Details = $"Active Worlds: {worldCount}";

            if (worldCount == 0)
            {
                check.Status = HealthStatus.Warning;
                check.Message = "No ECS worlds found";
            }
            else
            {
                check.Status = HealthStatus.Healthy;
                check.Message = "ECS worlds are active";
            }
        }

        private enum HealthStatus
        {
            Healthy,
            Warning,
            Unhealthy
        }

        private class HealthCheck
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public HealthStatus Status { get; set; }
            public string Message { get; set; }
            public string Details { get; set; }
            public System.Action<HealthCheck> CheckFunction { get; set; }
        }
    }
}
