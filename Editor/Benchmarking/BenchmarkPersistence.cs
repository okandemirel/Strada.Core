using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Strada.Core.Editor.Benchmarking
{
    /// <summary>
    /// Handles persistence of benchmark results to JSON files.
    /// Supports saving sessions and loading historical results for comparison.
    /// </summary>
    public static class BenchmarkPersistence
    {
        private const string DefaultDirectory = "BenchmarkResults";
        private const string SessionFilePattern = "benchmark_session_*.json";
        
        /// <summary>
        /// Gets the default directory for benchmark results.
        /// </summary>
        public static string GetDefaultDirectory()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectPath, DefaultDirectory);
        }
        
        /// <summary>
        /// Saves a benchmark session to a JSON file.
        /// </summary>
        /// <param name="session">The session to save.</param>
        /// <param name="directory">Optional directory path. Uses default if not specified.</param>
        /// <returns>The path to the saved file.</returns>
        public static string SaveSession(BenchmarkSession session, string directory = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            
            directory = directory ?? GetDefaultDirectory();
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var filename = $"benchmark_session_{session.Timestamp:yyyyMMdd_HHmmss}_{session.SessionId}.json";
            var path = Path.Combine(directory, filename);
            
            var wrapper = new BenchmarkSessionWrapper(session);
            var json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(path, json);
            
            return path;
        }
        
        /// <summary>
        /// Loads a benchmark session from a JSON file.
        /// </summary>
        /// <param name="path">The path to the JSON file.</param>
        /// <returns>The loaded session, or null if loading fails.</returns>
        public static BenchmarkSession LoadSession(string path)
        {
            if (!File.Exists(path))
                return null;
            
            try
            {
                var json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<BenchmarkSessionWrapper>(json);
                return wrapper?.ToSession();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BenchmarkPersistence] Failed to load session from {path}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets all saved session files in the default directory.
        /// </summary>
        /// <returns>List of session file paths, ordered by date descending.</returns>
        public static List<string> GetSavedSessions(string directory = null)
        {
            directory = directory ?? GetDefaultDirectory();
            
            if (!Directory.Exists(directory))
                return new List<string>();
            
            return Directory.GetFiles(directory, "benchmark_session_*.json")
                .OrderByDescending(f => f)
                .ToList();
        }
        
        /// <summary>
        /// Loads the most recent session.
        /// </summary>
        public static BenchmarkSession LoadLatestSession(string directory = null)
        {
            var sessions = GetSavedSessions(directory);
            if (sessions.Count == 0)
                return null;
            
            return LoadSession(sessions[0]);
        }
        
        /// <summary>
        /// Loads all sessions from the default directory.
        /// </summary>
        public static List<BenchmarkSession> LoadAllSessions(string directory = null)
        {
            var sessions = new List<BenchmarkSession>();
            foreach (var path in GetSavedSessions(directory))
            {
                var session = LoadSession(path);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            return sessions;
        }
        
        /// <summary>
        /// Deletes a session file.
        /// </summary>
        public static bool DeleteSession(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BenchmarkPersistence] Failed to delete session: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Exports a session to a custom path.
        /// </summary>
        public static bool ExportSession(BenchmarkSession session, string path)
        {
            try
            {
                var wrapper = new BenchmarkSessionWrapper(session);
                var json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BenchmarkPersistence] Failed to export session: {ex.Message}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// Wrapper class for JSON serialization of BenchmarkSession.
    /// Unity's JsonUtility requires specific structure for serialization.
    /// </summary>
    [Serializable]
    internal class BenchmarkSessionWrapper
    {
        public string sessionId;
        public string timestamp;
        public string unityVersion;
        public string platform;
        public List<BenchmarkResultWrapper> results = new List<BenchmarkResultWrapper>();
        
        public BenchmarkSessionWrapper() { }
        
        public BenchmarkSessionWrapper(BenchmarkSession session)
        {
            sessionId = session.SessionId;
            timestamp = session.Timestamp.ToString("o");
            unityVersion = session.UnityVersion;
            platform = session.Platform;
            
            foreach (var result in session.Results)
            {
                results.Add(new BenchmarkResultWrapper(result));
            }
        }
        
        public BenchmarkSession ToSession()
        {
            var session = new BenchmarkSession
            {
                SessionId = sessionId,
                Timestamp = DateTime.Parse(timestamp),
                UnityVersion = unityVersion,
                Platform = platform
            };
            
            foreach (var wrapper in results)
            {
                session.Results.Add(wrapper.ToResult());
            }
            
            return session;
        }
    }
    
    [Serializable]
    internal class BenchmarkResultWrapper
    {
        public string name;
        public string category;
        public string timestamp;
        public int iterations;
        public double totalTimeMs;
        public double averageTimeMs;
        public double operationsPerSecond;
        public long memoryAllocatedBytes;
        public double minTimeMs;
        public double maxTimeMs;
        public double standardDeviation;
        public bool passed;
        public string errorMessage;
        
        public BenchmarkResultWrapper() { }
        
        public BenchmarkResultWrapper(BenchmarkResult result)
        {
            name = result.Name;
            category = result.Category;
            timestamp = result.Timestamp.ToString("o");
            iterations = result.Iterations;
            totalTimeMs = result.TotalTimeMs;
            averageTimeMs = result.AverageTimeMs;
            operationsPerSecond = result.OperationsPerSecond;
            memoryAllocatedBytes = result.MemoryAllocatedBytes;
            minTimeMs = result.MinTimeMs;
            maxTimeMs = result.MaxTimeMs;
            standardDeviation = result.StandardDeviation;
            passed = result.Passed;
            errorMessage = result.ErrorMessage;
        }
        
        public BenchmarkResult ToResult()
        {
            return new BenchmarkResult
            {
                Name = name,
                Category = category,
                Timestamp = DateTime.Parse(timestamp),
                Iterations = iterations,
                TotalTimeMs = totalTimeMs,
                AverageTimeMs = averageTimeMs,
                OperationsPerSecond = operationsPerSecond,
                MemoryAllocatedBytes = memoryAllocatedBytes,
                MinTimeMs = minTimeMs,
                MaxTimeMs = maxTimeMs,
                StandardDeviation = standardDeviation,
                Passed = passed,
                ErrorMessage = errorMessage
            };
        }
    }
}
