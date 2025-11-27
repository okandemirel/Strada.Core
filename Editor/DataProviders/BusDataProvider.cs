using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Strada.Core.Communication;
using Strada.Core.ECS;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Provides access to StradaBus message data for editor tools.
    /// Hooks into StradaBus for message interception and logging.
    /// </summary>
    public class BusDataProvider : EditorDataProviderBase<BusSnapshot>, IBusDataProvider
    {
        private static BusDataProvider _instance;
        private readonly List<MessageLogEntry> _logEntries = new List<MessageLogEntry>();
        private readonly object _logLock = new object();
        private bool _isLogging;
        private const int MaxLogEntries = 1000;

        /// <summary>
        /// Gets the singleton instance of the BusDataProvider.
        /// </summary>
        public static BusDataProvider Instance => _instance ??= new BusDataProvider();

        private BusDataProvider() { }

        /// <summary>
        /// Gets whether the StradaBus is available.
        /// </summary>
        public override bool IsAvailable
        {
            get
            {
                if (!Application.isPlaying) return false;
                return World.Current?.MessageBus != null;
            }
        }

        /// <summary>
        /// Gets whether message logging is currently active.
        /// </summary>
        public bool IsLogging => _isLogging;

        /// <summary>
        /// Starts logging messages from StradaBus.
        /// </summary>
        public void StartLogging()
        {
            if (_isLogging) return;
            _isLogging = true;
            // Note: Actual interception would require modifying StradaBus
            // or using a wrapper. For now, we provide the infrastructure.
        }

        /// <summary>
        /// Stops logging messages.
        /// </summary>
        public void StopLogging()
        {
            _isLogging = false;
        }

        /// <summary>
        /// Clears all logged messages.
        /// </summary>
        public void ClearLog()
        {
            lock (_logLock)
            {
                _logEntries.Clear();
            }
            RaiseDataChanged();
        }

        /// <summary>
        /// Gets log entries matching the specified filter.
        /// </summary>
        public IReadOnlyList<MessageLogEntry> GetLogEntries(MessageFilter filter = null)
        {
            lock (_logLock)
            {
                if (filter == null)
                    return _logEntries.ToList();

                var filtered = _logEntries.AsEnumerable();

                if (filter.Kind.HasValue)
                    filtered = filtered.Where(e => e.Kind == filter.Kind.Value);

                if (!string.IsNullOrEmpty(filter.TypePattern))
                {
                    filtered = filtered.Where(e => 
                        MatchesTypePattern(e.MessageType?.Name, filter.TypePattern));
                }

                if (filter.StartTime.HasValue)
                    filtered = filtered.Where(e => e.Timestamp >= filter.StartTime.Value);

                if (filter.EndTime.HasValue)
                    filtered = filtered.Where(e => e.Timestamp <= filter.EndTime.Value);

                return filtered.Take(filter.MaxResults).ToList();
            }
        }

        /// <summary>
        /// Checks if a message type name matches the filter pattern.
        /// Supports wildcards (* for any characters, ? for single character) and partial matches.
        /// </summary>
        /// <param name="typeName">The type name to check.</param>
        /// <param name="pattern">The filter pattern.</param>
        /// <returns>True if the type matches the pattern.</returns>
        public static bool MatchesTypePattern(string typeName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(typeName)) return false;

            // If pattern contains wildcards, use regex matching
            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                try
                {
                    var regexPattern = "^" + Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    return Regex.IsMatch(typeName, regexPattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Fallback to simple contains if regex fails
                    return typeName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            // Simple partial match (case-insensitive)
            return typeName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Gets the subscriber count for a specific event type.
        /// </summary>
        public int GetSubscriberCount(Type messageType)
        {
            if (!IsAvailable) return 0;

            try
            {
                var bus = World.Current.MessageBus;
                
                // Use reflection to call GetSubscriberCount<T>
                var method = typeof(MessageBus).GetMethod("GetSubscriberCount");
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(messageType);
                    return (int)genericMethod.Invoke(bus, null);
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// Logs a message entry. Called by message interceptors.
        /// </summary>
        public void LogMessage(MessageLogEntry entry)
        {
            if (!_isLogging) return;

            lock (_logLock)
            {
                _logEntries.Add(entry);

                // Enforce max log size
                while (_logEntries.Count > MaxLogEntries)
                {
                    _logEntries.RemoveAt(0);
                }
            }

            RaiseDataChanged();
        }

        /// <summary>
        /// Logs an event publication.
        /// </summary>
        public void LogEvent<T>(T evt, int subscriberCount) where T : struct
        {
            LogMessage(new MessageLogEntry
            {
                Timestamp = DateTime.Now,
                Kind = MessageKind.Event,
                MessageType = typeof(T),
                Payload = evt,
                SubscriberCount = subscriberCount,
                HasHandler = subscriberCount > 0
            });
        }

        /// <summary>
        /// Logs a command send.
        /// </summary>
        public void LogCommand<T>(T command, bool hasHandler) where T : struct
        {
            LogMessage(new MessageLogEntry
            {
                Timestamp = DateTime.Now,
                Kind = MessageKind.Command,
                MessageType = typeof(T),
                Payload = command,
                HasHandler = hasHandler
            });
        }

        /// <summary>
        /// Logs a query execution.
        /// </summary>
        public void LogQuery<TQuery, TResult>(TQuery query, double processingTimeMs) 
            where TQuery : struct
        {
            LogMessage(new MessageLogEntry
            {
                Timestamp = DateTime.Now,
                Kind = MessageKind.Query,
                MessageType = typeof(TQuery),
                Payload = query,
                HasHandler = true,
                ProcessingTimeMs = processingTimeMs
            });
        }

        /// <summary>
        /// Checks if a command type has a registered handler.
        /// </summary>
        public bool HasCommandHandler(Type commandType)
        {
            if (!IsAvailable) return false;

            try
            {
                var bus = World.Current.MessageBus;
                var busType = typeof(MessageBus);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;

                var handlersField = busType.GetField("_commandHandlers", flags);
                var maxIdField = busType.GetField("_maxCommandId", flags);

                if (handlersField == null || maxIdField == null)
                    return false;

                var handlers = (object[])handlersField.GetValue(bus);
                var maxId = (int)maxIdField.GetValue(bus);

                // Get the command type ID
                var typeIdType = busType.GetNestedType("CommandTypeId`1", BindingFlags.NonPublic)
                    ?.MakeGenericType(commandType);
                
                if (typeIdType == null) return false;

                var idField = typeIdType.GetField("Id", BindingFlags.Public | BindingFlags.Static);
                if (idField == null) return false;

                var id = (int)idField.GetValue(null);
                return id <= maxId && handlers[id] != null;
            }
            catch
            {
                return false;
            }
        }

        protected override BusSnapshot FetchData()
        {
            var snapshot = new BusSnapshot
            {
                Timestamp = DateTime.Now,
                IsLogging = _isLogging,
                SubscriberCounts = new Dictionary<Type, int>()
            };

            lock (_logLock)
            {
                snapshot.TotalMessageCount = _logEntries.Count;
                snapshot.LogEntries = _logEntries.ToList();
            }

            return snapshot;
        }

        protected override void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);

            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                // Clear log when exiting play mode
                lock (_logLock)
                {
                    _logEntries.Clear();
                }
                _isLogging = false;
            }
        }
    }

    /// <summary>
    /// Extended interface for bus data provider.
    /// </summary>
    public interface IBusDataProvider : IEditorDataProvider<BusSnapshot>
    {
        void StartLogging();
        void StopLogging();
        IReadOnlyList<MessageLogEntry> GetLogEntries(MessageFilter filter);
        int GetSubscriberCount(Type messageType);
    }
}
