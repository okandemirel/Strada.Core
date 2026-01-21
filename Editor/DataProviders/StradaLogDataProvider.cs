using System;
using System.Collections.Generic;
using Strada.Core.Logging;
using UnityEditor;
using UnityEngine;
using StradaLogType = Strada.Core.Logging.LogType;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Data provider for StradaLog entries in the editor.
    /// Subscribes to log events and provides filtered access to entries.
    /// </summary>
    public sealed class StradaLogDataProvider : IDisposable
    {
        private static StradaLogDataProvider _instance;

        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly object _lock = new object();
        private bool _isPaused;
        private bool _isDisposed;

        /// <summary>
        /// Gets the singleton instance of the StradaLogDataProvider.
        /// </summary>
        public static StradaLogDataProvider Instance => _instance ??= new StradaLogDataProvider();

        /// <summary>
        /// Event raised when a new log entry is received.
        /// </summary>
        public event Action<LogEntry> OnLogReceived;

        /// <summary>
        /// Event raised when the log is cleared.
        /// </summary>
        public event Action OnLogCleared;

        /// <summary>
        /// Gets or sets whether log capture is paused.
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        /// <summary>
        /// Gets the number of captured log entries.
        /// </summary>
        public int EntryCount
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }

        private StradaLogDataProvider()
        {
            StradaLog.OnLogAdded += HandleLogAdded;
            Application.logMessageReceived += HandleUnityLog;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Gets all log entries.
        /// </summary>
        public List<LogEntry> GetEntries()
        {
            lock (_lock)
            {
                var result = new List<LogEntry>(_entries.Count);
                for (int i = 0; i < _entries.Count; i++)
                {
                    result.Add(_entries[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets log entries filtered by module.
        /// </summary>
        public List<LogEntry> GetEntriesByModule(LogModule module)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Module == module)
                        result.Add(_entries[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets log entries filtered by multiple modules.
        /// </summary>
        public List<LogEntry> GetEntriesByModules(HashSet<LogModule> modules)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (modules.Contains(_entries[i].Module))
                        result.Add(_entries[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets log entries filtered by type.
        /// </summary>
        public List<LogEntry> GetEntriesByType(StradaLogType type)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Type == type)
                        result.Add(_entries[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets log entries matching a search filter.
        /// </summary>
        public List<LogEntry> GetFilteredEntries(LogFilter filter)
        {
            lock (_lock)
            {
                var result = new List<LogEntry>();

                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];

                    if (filter.Modules != null && filter.Modules.Count > 0)
                    {
                        if (!filter.Modules.Contains(entry.Module))
                            continue;
                    }

                    if (filter.Types != null && filter.Types.Count > 0)
                    {
                        if (!filter.Types.Contains(entry.Type))
                            continue;
                    }

                    if (filter.ShowDeepLogs.HasValue)
                    {
                        if (entry.IsDeepLog != filter.ShowDeepLogs.Value)
                            continue;
                    }

                    if (!string.IsNullOrEmpty(filter.SearchText))
                    {
                        var searchLower = filter.SearchText.ToLowerInvariant();
                        var messageLower = entry.Message.ToLowerInvariant();

                        if (!messageLower.Contains(searchLower))
                            continue;
                    }

                    result.Add(entry);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the count of entries by module.
        /// </summary>
        public Dictionary<LogModule, int> GetCountsByModule()
        {
            lock (_lock)
            {
                var counts = new Dictionary<LogModule, int>();

                for (int i = 0; i < _entries.Count; i++)
                {
                    var module = _entries[i].Module;
                    if (counts.ContainsKey(module))
                        counts[module]++;
                    else
                        counts[module] = 1;
                }

                return counts;
            }
        }

        /// <summary>
        /// Gets the count of entries by type.
        /// </summary>
        public Dictionary<StradaLogType, int> GetCountsByType()
        {
            lock (_lock)
            {
                var counts = new Dictionary<StradaLogType, int>();

                for (int i = 0; i < _entries.Count; i++)
                {
                    var type = _entries[i].Type;
                    if (counts.ContainsKey(type))
                        counts[type]++;
                    else
                        counts[type] = 1;
                }

                return counts;
            }
        }

        /// <summary>
        /// Clears all captured log entries.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }

            try
            {
                OnLogCleared?.Invoke();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Opens the source file at the line specified in the log entry.
        /// </summary>
        public static void OpenSourceFile(LogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.FilePath))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.FilePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, entry.LineNumber);
                return;
            }

            var fullPath = System.IO.Path.GetFullPath(entry.FilePath);
            if (System.IO.File.Exists(fullPath))
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, entry.LineNumber);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            StradaLog.OnLogAdded -= HandleLogAdded;
            Application.logMessageReceived -= HandleUnityLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void HandleLogAdded(LogEntry entry)
        {
            if (_isPaused)
                return;

            lock (_lock)
            {
                var maxEntries = StradaLogSettings.Instance.MaxLogEntries;

                if (_entries.Count >= maxEntries)
                {
                    _entries.RemoveAt(0);
                }

                _entries.Add(entry);
            }

            try
            {
                OnLogReceived?.Invoke(entry);
            }
            catch
            {
            }
        }

        private void HandleUnityLog(string condition, string stackTrace, UnityEngine.LogType type)
        {
            if (_isPaused)
                return;

            if (condition.StartsWith("[Strada]"))
                return;

            var logType = ConvertLogType(type);
            var module = ParseModuleFromMessage(condition);

            var entry = new LogEntry(condition, logType, module, stackTrace, false);

            lock (_lock)
            {
                var maxEntries = StradaLogSettings.Instance.MaxLogEntries;

                if (_entries.Count >= maxEntries)
                {
                    _entries.RemoveAt(0);
                }

                _entries.Add(entry);
            }

            try
            {
                OnLogReceived?.Invoke(entry);
            }
            catch
            {
            }
        }

        private static StradaLogType ConvertLogType(UnityEngine.LogType unityType)
        {
            switch (unityType)
            {
                case UnityEngine.LogType.Warning:
                    return StradaLogType.Warning;
                case UnityEngine.LogType.Error:
                    return StradaLogType.Error;
                case UnityEngine.LogType.Exception:
                    return StradaLogType.Exception;
                default:
                    return StradaLogType.Info;
            }
        }

        private static LogModule ParseModuleFromMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return LogModule.Unknown;

            int startBracket = message.IndexOf('[');
            if (startBracket < 0)
                return LogModule.Unknown;

            int endBracket = message.IndexOf(']', startBracket);
            if (endBracket < 0)
                return LogModule.Unknown;

            var tag = message.Substring(startBracket + 1, endBracket - startBracket - 1);

            var moduleValues = System.Enum.GetValues(typeof(LogModule));
            for (int i = 0; i < moduleValues.Length; i++)
            {
                var moduleValue = (LogModule)moduleValues.GetValue(i);
                if (moduleValue.ToString().Equals(tag, System.StringComparison.OrdinalIgnoreCase))
                    return moduleValue;
            }

            return LogModule.Unknown;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _isPaused = false;
            }
        }
    }

    /// <summary>
    /// Filter criteria for log entries.
    /// </summary>
    public sealed class LogFilter
    {
        /// <summary>
        /// Modules to include (null or empty means all).
        /// </summary>
        public HashSet<LogModule> Modules { get; set; }

        /// <summary>
        /// Log types to include (null or empty means all).
        /// </summary>
        public HashSet<StradaLogType> Types { get; set; }

        /// <summary>
        /// Text to search for in messages.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Filter for deep logs (null means include all).
        /// </summary>
        public bool? ShowDeepLogs { get; set; }
    }
}
