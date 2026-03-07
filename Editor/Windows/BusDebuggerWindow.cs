using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for debugging EventBus messages.
    /// Provides message logging, filtering, and payload inspection.
    /// Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6
    /// </summary>
    public class BusDebuggerWindow : EditorWindow
    {
        private const int MaxLogEntries = 1000;
        private const float MinMessageListWidth = 300f;
        private const float MaxMessageListWidth = 600f;
        private const float DefaultMessageListWidth = 450f;

        private float _messageListWidth = DefaultMessageListWidth;
        private bool _isResizing;

        private Vector2 _messageListScrollPosition;
        private Vector2 _detailScrollPosition;

        private int _selectedMessageIndex = -1;

        private string _typeFilterPattern = "";
        private MessageKind? _kindFilter;
        private bool _showFilterOptions;

        private enum Tab { Log, FlowGraph }
        private Tab _currentTab = Tab.Log;
        private Vector2 _graphScrollPosition;
        private List<Type> _cachedEventTypes = new List<Type>();
        private bool _graphNeedsRefresh = true;

        private bool _isPaused;
        private bool _autoScroll = true;

        private List<MessageLogEntry> _displayedEntries = new List<MessageLogEntry>();
        private double _lastRefreshTime;
        private float _refreshInterval = 0.1f;

        private GUIStyle _headerStyle;
        private GUIStyle _messageItemStyle;
        private GUIStyle _selectedMessageStyle;
        private GUIStyle _eventStyle;
        private GUIStyle _commandStyle;
        private GUIStyle _queryStyle;
        private GUIStyle _warningIconStyle;
        private bool _stylesInitialized;

        private readonly Color _eventColor = new Color(0.4f, 0.7f, 0.4f);
        private readonly Color _commandColor = new Color(0.5f, 0.6f, 0.9f);
        private readonly Color _queryColor = new Color(0.9f, 0.7f, 0.4f);
        private readonly Color _warningColor = new Color(1.0f, 0.6f, 0.2f);
        private readonly Color _selectedColor = new Color(0.24f, 0.49f, 0.91f, 0.4f);
        private readonly Color _resizeHandleColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private BusDataProvider _busDataProvider;

        public static void ShowWindow()
        {
            var window = GetWindow<BusDebuggerWindow>("Bus Debugger");
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            _busDataProvider = BusDataProvider.Instance;
            _busDataProvider.OnDataChanged += OnDataChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            if (_busDataProvider != null)
            {
                _busDataProvider.OnDataChanged -= OnDataChanged;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnDataChanged()
        {
            if (!_isPaused)
            {
                RefreshDisplayedEntries();
                Repaint();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                _displayedEntries.Clear();
                _selectedMessageIndex = -1;
                if (state == PlayModeStateChange.ExitingPlayMode)
                    _isPaused = false;
            }
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (!Application.isPlaying || _isPaused) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                RefreshDisplayedEntries();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(5, 5, 8, 4)
            };

            _messageItemStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(4, 4, 3, 3),
                margin = new RectOffset(2, 2, 1, 1),
                richText = true
            };

            _selectedMessageStyle = new GUIStyle(_messageItemStyle)
            {
                normal = { background = CreateColorTexture(_selectedColor) }
            };

            _eventStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = _eventColor },
                fontStyle = FontStyle.Bold
            };

            _commandStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = _commandColor },
                fontStyle = FontStyle.Bold
            };

            _queryStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = _queryColor },
                fontStyle = FontStyle.Bold
            };

            _warningIconStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = _warningColor },
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            _stylesInitialized = true;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (!Application.isPlaying)
            {
                DrawNotPlayingMessage();
                return;
            }

            if (!_busDataProvider.IsAvailable)
            {
                DrawNoBusMessage();
                return;
            }

            DrawToolbar();
            
            if (_currentTab == Tab.Log)
            {
                DrawFilterBar();
                DrawSplitView();
            }
            else
            {
                DrawFlowGraph();
            }
        }

        private void DrawNotPlayingMessage()
        {
            GUILayout.Space(50);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(450));
            EditorGUILayout.HelpBox(
                "STRADABUS MESSAGE DEBUGGER\n\n" +
                "Real-time monitoring of EventBus messages:\n" +
                "• View Events, Signals, and Queries\n" +
                "• Filter by message type pattern\n" +
                "• Inspect message payloads\n" +
                "• Detect unhandled signals\n\n" +
                "Enter Play Mode to start debugging.",
                MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Enter Play Mode", GUILayout.Height(30), GUILayout.Width(150)))
            {
                EditorApplication.isPlaying = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoBusMessage()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "No active EventBus found.\n\nEnsure a World with a Bus is created.",
                MessageType.Warning,
                true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }


        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, new[] { "Log", "Flow Graph" }, EditorStyles.toolbarButton, GUILayout.Width(150));
            GUILayout.Space(10);

            var isLogging = _busDataProvider.IsLogging;
            var logIcon = isLogging ? "●" : "○";
            var logColor = isLogging ? Color.green : Color.gray;
            var prevColor = GUI.contentColor;
            GUI.contentColor = logColor;

            if (GUILayout.Button($"{logIcon} Log", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                if (isLogging)
                    _busDataProvider.StopLogging();
                else
                    _busDataProvider.StartLogging();
            }

            GUI.contentColor = prevColor;

            var pauseIcon = _isPaused ? "▶" : "❚❚";
            var pauseTooltip = _isPaused ? "Resume" : "Pause";
            if (GUILayout.Button(new GUIContent(pauseIcon, pauseTooltip), EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _isPaused = !_isPaused;
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                ClearLog();
            }

            GUILayout.Space(10);

            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            var totalCount = _busDataProvider.GetLogEntries().Count;
            var filteredCount = _displayedEntries.Count;
            GUILayout.Label($"Messages: {filteredCount}/{totalCount}", EditorStyles.toolbarButton);

            if (totalCount >= MaxLogEntries)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _warningColor;
                GUILayout.Label("Buffer Full", EditorStyles.toolbarButton, GUILayout.Width(70));
                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFlowGraph()
        {
            if (_graphNeedsRefresh)
            {
                RefreshGraphData();
                _graphNeedsRefresh = false;
            }

            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh Graph", EditorStyles.toolbarButton))
            {
                _graphNeedsRefresh = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _graphScrollPosition = EditorGUILayout.BeginScrollView(_graphScrollPosition);
            
            if (_cachedEventTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No events found or not in Play Mode.", MessageType.Info);
            }
            else
            {
                foreach (var eventType in _cachedEventTypes)
                {
                    DrawEventNode(eventType);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RefreshGraphData()
        {
            _cachedEventTypes.Clear();
            if (!Application.isPlaying) return;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try 
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum && 
                           (type.Name.EndsWith("Event") || type.Name.EndsWith("Signal")))
                        {
                            _cachedEventTypes.Add(type);
                        }
                    }
                }
                catch {}
            }
            _cachedEventTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        private void DrawEventNode(Type eventType)
        {
            var subscribers = _busDataProvider.GetSubscriberDetails(eventType);
            if (subscribers.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(eventType.Name, EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{subscribers.Count} Subscribers", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            foreach (var sub in subscribers)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("↳", GUILayout.Width(15));
                
                var targetName = sub.Target?.GetType().Name ?? "Static/Unknown";
                var methodName = sub.Method?.Name ?? "Unknown";
                
                if (GUILayout.Button($"{targetName}.{methodName}", EditorStyles.label))
                {
                    if (sub.Target is MonoBehaviour mb)
                    {
                        EditorGUIUtility.PingObject(mb);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            _showFilterOptions = GUILayout.Toggle(_showFilterOptions, "Filters", EditorStyles.toolbarButton, GUILayout.Width(55));

            if (_showFilterOptions)
            {
                GUILayout.Space(10);

                GUILayout.Label("Type:", GUILayout.Width(35));
                var newPattern = EditorGUILayout.TextField(_typeFilterPattern, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (newPattern != _typeFilterPattern)
                {
                    _typeFilterPattern = newPattern;
                    RefreshDisplayedEntries();
                }

                if (!string.IsNullOrEmpty(_typeFilterPattern))
                {
                    GUILayout.Label("(* = wildcard)", EditorStyles.miniLabel, GUILayout.Width(80));
                }

                GUILayout.Space(10);

                GUILayout.Label("Kind:", GUILayout.Width(35));
                var kindOptions = new[] { "All", "Event", "Command", "Query" };
                var currentKindIndex = _kindFilter.HasValue ? (int)_kindFilter.Value + 1 : 0;
                var newKindIndex = EditorGUILayout.Popup(currentKindIndex, kindOptions, EditorStyles.toolbarPopup, GUILayout.Width(80));
                
                var newKindFilter = newKindIndex == 0 ? (MessageKind?)null : (MessageKind)(newKindIndex - 1);
                if (newKindFilter != _kindFilter)
                {
                    _kindFilter = newKindFilter;
                    RefreshDisplayedEntries();
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Clear Filters", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _typeFilterPattern = "";
                    _kindFilter = null;
                    RefreshDisplayedEntries();
                }
            }

            GUILayout.FlexibleSpace();

            DrawLegend();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLegend()
        {
            GUILayout.Label("Legend:", GUILayout.Width(50));

            DrawLegendSwatch(_eventColor, "Event", 35);
            DrawLegendSwatch(_commandColor, "Cmd", 30);
            DrawLegendSwatch(_queryColor, "Query", 35);

            GUILayout.Label("⚠", _warningIconStyle, GUILayout.Width(15));
            GUILayout.Label("No Handler", GUILayout.Width(65));
        }

        private void DrawLegendSwatch(Color color, string label, float labelWidth)
        {
            var rect = GUILayoutUtility.GetRect(10, 10);
            EditorGUI.DrawRect(rect, color);
            GUILayout.Label(label, GUILayout.Width(labelWidth));
        }

        private void DrawSplitView()
        {
            EditorGUILayout.BeginHorizontal();

            DrawMessageListPanel();

            DrawResizeHandle();

            DrawMessageDetailsPanel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMessageListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_messageListWidth));

            EditorGUILayout.LabelField("Message Log", _headerStyle);

            _messageListScrollPosition = EditorGUILayout.BeginScrollView(_messageListScrollPosition);

            if (_displayedEntries.Count == 0)
            {
                if (_busDataProvider.IsLogging)
                {
                    EditorGUILayout.HelpBox("Waiting for messages...\nPublish events, commands, or queries to see them here.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Logging is disabled.\nClick 'Log' to start capturing messages.", MessageType.Info);
                }
            }
            else
            {
                for (int i = 0; i < _displayedEntries.Count; i++)
                {
                    DrawMessageListItem(i, _displayedEntries[i]);
                }

                if (_autoScroll && !_isPaused && Event.current.type == EventType.Repaint)
                {
                    _messageListScrollPosition.y = float.MaxValue;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        private void DrawMessageListItem(int index, MessageLogEntry entry)
        {
            var isSelected = index == _selectedMessageIndex;
            var style = isSelected ? _selectedMessageStyle : _messageItemStyle;

            EditorGUILayout.BeginHorizontal(style);

            if (entry.Kind == MessageKind.Command && !entry.HasHandler)
            {
                GUILayout.Label("⚠", _warningIconStyle, GUILayout.Width(18));
            }
            else
            {
                GUILayout.Space(18);
            }

            var timeStr = entry.Timestamp.ToString("HH:mm:ss.fff");
            GUILayout.Label(timeStr, EditorStyles.miniLabel, GUILayout.Width(75));

            var kindStyle = GetKindStyle(entry.Kind);
            var kindLabel = GetKindLabel(entry.Kind);
            GUILayout.Label(kindLabel, kindStyle, GUILayout.Width(45));

            var typeName = entry.MessageType?.Name ?? "Unknown";
            if (GUILayout.Button(typeName, EditorStyles.label, GUILayout.ExpandWidth(true)))
            {
                _selectedMessageIndex = index;
            }

            if (entry.Kind == MessageKind.Event)
            {
                GUILayout.Label($"[{entry.SubscriberCount}]", EditorStyles.miniLabel, GUILayout.Width(30));
            }
            else if (entry.Kind == MessageKind.Query && entry.ProcessingTimeMs > 0)
            {
                GUILayout.Label($"{entry.ProcessingTimeMs:F2}ms", EditorStyles.miniLabel, GUILayout.Width(50));
            }
            else
            {
                GUILayout.Space(30);
            }

            EditorGUILayout.EndHorizontal();
        }

        private GUIStyle GetKindStyle(MessageKind kind)
        {
            return kind switch
            {
                MessageKind.Event => _eventStyle,
                MessageKind.Command => _commandStyle,
                MessageKind.Query => _queryStyle,
                _ => EditorStyles.miniLabel
            };
        }

        private string GetKindLabel(MessageKind kind)
        {
            return kind switch
            {
                MessageKind.Event => "EVT",
                MessageKind.Command => "CMD",
                MessageKind.Query => "QRY",
                _ => "???"
            };
        }

        private void DrawResizeHandle()
        {
            var resizeRect = GUILayoutUtility.GetRect(4f, 4f, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
                Event.current.Use();
            }

            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    _messageListWidth = Mathf.Clamp(Event.current.mousePosition.x, MinMessageListWidth, MaxMessageListWidth);
                    Repaint();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                }
            }

            EditorGUI.DrawRect(resizeRect, _resizeHandleColor);
        }

        private void DrawMessageDetailsPanel()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("Message Details", _headerStyle);

            if (_selectedMessageIndex < 0 || _selectedMessageIndex >= _displayedEntries.Count)
            {
                EditorGUILayout.HelpBox("Select a message from the list to view its details.", MessageType.Info);
            }
            else
            {
                var entry = _displayedEntries[_selectedMessageIndex];
                DrawMessageDetails(entry);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMessageDetails(MessageLogEntry entry)
        {
            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawDetailRow("Type:", entry.MessageType?.FullName ?? "Unknown");
            DrawDetailRow("Kind:", entry.Kind.ToString(), GetKindStyle(entry.Kind));
            DrawDetailRow("Timestamp:", entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            if (entry.Kind == MessageKind.Event)
            {
                DrawDetailRow("Subscribers:", entry.SubscriberCount.ToString());
            }

            if (entry.Kind == MessageKind.Command)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Has Handler:", EditorStyles.boldLabel, GUILayout.Width(80));
                if (entry.HasHandler)
                {
                    EditorGUILayout.LabelField("Yes", EditorStyles.label);
                }
                else
                {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = _warningColor;
                    EditorGUILayout.LabelField("⚠ No - Command will not be processed!", EditorStyles.boldLabel);
                    GUI.contentColor = prevColor;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (entry.Kind == MessageKind.Query && entry.ProcessingTimeMs > 0)
            {
                DrawDetailRow("Processing:", $"{entry.ProcessingTimeMs:F3} ms");
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Payload", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (entry.Payload != null)
            {
                DrawPayloadFields(entry.Payload);
            }
            else
            {
                EditorGUILayout.LabelField("(null)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetailRow(string label, string value, GUIStyle valueStyle = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField(value, valueStyle ?? EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPayloadFields(object payload)
        {
            var type = payload.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (fields.Length == 0)
            {
                EditorGUILayout.LabelField("(no public fields)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            foreach (var field in fields)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.Name, GUILayout.Width(150));

                var value = field.GetValue(payload);
                var valueStr = FormatValue(value);
                EditorGUILayout.SelectableLabel(valueStr, EditorStyles.textField, GUILayout.Height(18));

                EditorGUILayout.EndHorizontal();
            }
        }

        private string FormatValue(object value)
        {
            if (value == null) return "(null)";

            var type = value.GetType();

            if (type == typeof(Vector2)) return ((Vector2)value).ToString("F3");
            if (type == typeof(Vector3)) return ((Vector3)value).ToString("F3");
            if (type == typeof(Vector4)) return ((Vector4)value).ToString("F3");
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return $"({q.x:F3}, {q.y:F3}, {q.z:F3}, {q.w:F3})";
            }
            if (type == typeof(Color)) return ((Color)value).ToString();
            if (type == typeof(float)) return ((float)value).ToString("F4");
            if (type == typeof(double)) return ((double)value).ToString("F4");

            return value.ToString();
        }

        /// <summary>
        /// Refreshes the displayed entries based on current filter settings.
        /// </summary>
        private void RefreshDisplayedEntries()
        {
            var filter = BuildMessageFilter();
            _busDataProvider.GetLogEntriesNonAlloc(_displayedEntries, filter);
        }

        /// <summary>
        /// Builds a MessageFilter from the current UI filter settings.
        /// </summary>
        private MessageFilter BuildMessageFilter()
        {
            return new MessageFilter
            {
                TypePattern = string.IsNullOrEmpty(_typeFilterPattern) ? null : _typeFilterPattern,
                Kind = _kindFilter,
                MaxResults = MaxLogEntries
            };
        }

        /// <summary>
        /// Checks if a message type matches the filter pattern.
        /// Supports wildcards (* for any characters) and partial matches.
        /// </summary>
        /// <param name="typeName">The type name to check.</param>
        /// <param name="pattern">The filter pattern.</param>
        /// <returns>True if the type matches the pattern.</returns>
        internal static bool MatchesTypePattern(string typeName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(typeName)) return false;

            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            try
            {
                return Regex.IsMatch(typeName, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return typeName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>
        /// Clears all logged messages.
        /// </summary>
        private void ClearLog()
        {
            _busDataProvider.ClearLog();
            _displayedEntries.Clear();
            _selectedMessageIndex = -1;
            Repaint();
        }

        /// <summary>
        /// Gets the current displayed entries count.
        /// Exposed for testing purposes.
        /// </summary>
        internal int DisplayedEntriesCount => _displayedEntries.Count;

        /// <summary>
        /// Gets whether the debugger is currently paused.
        /// </summary>
        internal bool IsPaused => _isPaused;

        /// <summary>
        /// Gets the current type filter pattern.
        /// </summary>
        internal string TypeFilterPattern => _typeFilterPattern;

        /// <summary>
        /// Gets the current kind filter.
        /// </summary>
        internal MessageKind? KindFilter => _kindFilter;

        /// <summary>
        /// Sets the type filter pattern programmatically.
        /// </summary>
        internal void SetTypeFilter(string pattern)
        {
            _typeFilterPattern = pattern ?? "";
            RefreshDisplayedEntries();
        }

        /// <summary>
        /// Sets the kind filter programmatically.
        /// </summary>
        internal void SetKindFilter(MessageKind? kind)
        {
            _kindFilter = kind;
            RefreshDisplayedEntries();
        }

        /// <summary>
        /// Gets the displayed entries for testing.
        /// </summary>
        internal IReadOnlyList<MessageLogEntry> GetDisplayedEntries() => _displayedEntries;
    }
}
