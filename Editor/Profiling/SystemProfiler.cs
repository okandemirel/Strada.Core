using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Strada.Core.Editor.DataProviders.Models;
using UpdatePhase = Strada.Core.Editor.DataProviders.Models.UpdatePhase;

namespace Strada.Core.Editor.Profiling
{
    /// <summary>
    /// Captures and analyzes system execution timing data.
    /// Implements a circular buffer for efficient sample storage.
    /// </summary>
    public class SystemProfiler : IDisposable
    {
        private const int DefaultBufferSize = 1000;
        
        private readonly Dictionary<Type, CircularBuffer<SystemTimingSample>> _sampleBuffers;
        private readonly Dictionary<Type, UpdatePhase> _systemPhases;
        private readonly Dictionary<Type, Stopwatch> _activeTimers;
        private readonly Stopwatch _globalStopwatch;
        
        private bool _isRecording;
        private bool _isDisposed;
        private int _bufferSize;
        
        public bool IsRecording => _isRecording;
        public int BufferSize => _bufferSize;
        
        public event Action<SystemTimingSample> OnSampleCaptured;
        
        public SystemProfiler(int bufferSize = DefaultBufferSize)
        {
            _bufferSize = bufferSize;
            _sampleBuffers = new Dictionary<Type, CircularBuffer<SystemTimingSample>>();
            _systemPhases = new Dictionary<Type, UpdatePhase>();
            _activeTimers = new Dictionary<Type, Stopwatch>();
            _globalStopwatch = Stopwatch.StartNew();
        }
        
        /// <summary>
        /// Registers a system for profiling with its update phase.
        /// </summary>
        public void RegisterSystem(Type systemType, UpdatePhase phase)
        {
            if (!_sampleBuffers.ContainsKey(systemType))
            {
                _sampleBuffers[systemType] = new CircularBuffer<SystemTimingSample>(_bufferSize);
                _systemPhases[systemType] = phase;
                _activeTimers[systemType] = new Stopwatch();
            }
        }
        
        /// <summary>
        /// Starts recording timing samples.
        /// </summary>
        public void StartRecording()
        {
            _isRecording = true;
        }
        
        /// <summary>
        /// Stops recording timing samples.
        /// </summary>
        public void StopRecording()
        {
            _isRecording = false;
        }
        
        /// <summary>
        /// Clears all recorded samples.
        /// </summary>
        public void Clear()
        {
            foreach (var buffer in _sampleBuffers.Values)
            {
                buffer.Clear();
            }
        }

        /// <summary>
        /// Begins timing a system execution.
        /// </summary>
        public void BeginSystemTiming(Type systemType)
        {
            if (!_isRecording) return;
            
            if (_activeTimers.TryGetValue(systemType, out var timer))
            {
                timer.Restart();
            }
        }
        
        /// <summary>
        /// Ends timing a system execution and records the sample.
        /// </summary>
        public void EndSystemTiming(Type systemType)
        {
            if (!_isRecording) return;
            
            if (_activeTimers.TryGetValue(systemType, out var timer))
            {
                timer.Stop();
                
                var sample = new SystemTimingSample
                {
                    SystemType = systemType,
                    Phase = _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update),
                    ExecutionTimeMs = timer.Elapsed.TotalMilliseconds,
                    Timestamp = _globalStopwatch.ElapsedMilliseconds
                };
                
                if (_sampleBuffers.TryGetValue(systemType, out var buffer))
                {
                    buffer.Add(sample);
                }
                
                OnSampleCaptured?.Invoke(sample);
            }
        }
        
        /// <summary>
        /// Records a timing sample directly (for external timing sources).
        /// </summary>
        public void RecordSample(Type systemType, double executionTimeMs)
        {
            if (!_isRecording) return;
            
            if (!_sampleBuffers.ContainsKey(systemType))
            {
                RegisterSystem(systemType, UpdatePhase.Update);
            }
            
            var sample = new SystemTimingSample
            {
                SystemType = systemType,
                Phase = _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update),
                ExecutionTimeMs = executionTimeMs,
                Timestamp = _globalStopwatch.ElapsedMilliseconds
            };
            
            _sampleBuffers[systemType].Add(sample);
            OnSampleCaptured?.Invoke(sample);
        }
        
        /// <summary>
        /// Gets all samples for a specific system.
        /// </summary>
        public IReadOnlyList<SystemTimingSample> GetSamples(Type systemType)
        {
            if (_sampleBuffers.TryGetValue(systemType, out var buffer))
            {
                return buffer.ToList();
            }
            return Array.Empty<SystemTimingSample>();
        }
        
        /// <summary>
        /// Gets all samples across all systems.
        /// </summary>
        public IReadOnlyList<SystemTimingSample> GetAllSamples()
        {
            var allSamples = new List<SystemTimingSample>();
            foreach (var buffer in _sampleBuffers.Values)
            {
                allSamples.AddRange(buffer.ToList());
            }
            return allSamples.OrderBy(s => s.Timestamp).ToList();
        }
        
        /// <summary>
        /// Gets all registered system types.
        /// </summary>
        public IEnumerable<Type> GetRegisteredSystems()
        {
            return _sampleBuffers.Keys;
        }
        
        /// <summary>
        /// Gets the update phase for a system.
        /// </summary>
        public UpdatePhase GetSystemPhase(Type systemType)
        {
            return _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update);
        }

        /// <summary>
        /// Calculates metrics for a specific system.
        /// </summary>
        public SystemMetrics GetMetrics(Type systemType)
        {
            if (!_sampleBuffers.TryGetValue(systemType, out var buffer) || buffer.Count == 0)
            {
                return new SystemMetrics
                {
                    SystemType = systemType,
                    Phase = _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update)
                };
            }
            
            var samples = buffer.ToList();
            return CalculateMetrics(systemType, samples);
        }
        
        /// <summary>
        /// Calculates metrics for all registered systems.
        /// </summary>
        public IReadOnlyList<SystemMetrics> GetAllMetrics()
        {
            var metrics = new List<SystemMetrics>();
            foreach (var kvp in _sampleBuffers)
            {
                if (kvp.Value.Count > 0)
                {
                    metrics.Add(CalculateMetrics(kvp.Key, kvp.Value.ToList()));
                }
            }
            return metrics;
        }
        
        /// <summary>
        /// Gets metrics grouped by update phase.
        /// </summary>
        public Dictionary<UpdatePhase, List<SystemMetrics>> GetMetricsByPhase()
        {
            var result = new Dictionary<UpdatePhase, List<SystemMetrics>>();
            foreach (UpdatePhase phase in Enum.GetValues(typeof(UpdatePhase)))
            {
                result[phase] = new List<SystemMetrics>();
            }
            
            foreach (var metrics in GetAllMetrics())
            {
                result[metrics.Phase].Add(metrics);
            }
            
            return result;
        }
        
        private SystemMetrics CalculateMetrics(Type systemType, List<SystemTimingSample> samples)
        {
            if (samples.Count == 0)
            {
                return new SystemMetrics
                {
                    SystemType = systemType,
                    Phase = _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update)
                };
            }
            
            double sum = 0;
            double min = double.MaxValue;
            double max = double.MinValue;
            
            foreach (var sample in samples)
            {
                sum += sample.ExecutionTimeMs;
                if (sample.ExecutionTimeMs < min) min = sample.ExecutionTimeMs;
                if (sample.ExecutionTimeMs > max) max = sample.ExecutionTimeMs;
            }
            
            double average = sum / samples.Count;

            double sumSquaredDiff = 0;
            foreach (var sample in samples)
            {
                double diff = sample.ExecutionTimeMs - average;
                sumSquaredDiff += diff * diff;
            }
            double stdDev = Math.Sqrt(sumSquaredDiff / samples.Count);
            
            return new SystemMetrics
            {
                SystemType = systemType,
                Phase = _systemPhases.GetValueOrDefault(systemType, UpdatePhase.Update),
                AverageMs = average,
                MinMs = min == double.MaxValue ? 0 : min,
                MaxMs = max == double.MinValue ? 0 : max,
                StandardDeviation = stdDev,
                SampleCount = samples.Count,
                LastExecutionMs = samples[samples.Count - 1].ExecutionTimeMs
            };
        }
        
        /// <summary>
        /// Exports all profiling data to a serializable snapshot.
        /// </summary>
        public ProfilingSnapshot ExportSnapshot()
        {
            var snapshot = new ProfilingSnapshot
            {
                Timestamp = DateTime.Now,
                SessionId = Guid.NewGuid().ToString("N").Substring(0, 8)
            };

            foreach (var sample in GetAllSamples())
            {
                snapshot.Samples.Add(new SystemTimingSampleData
                {
                    SystemTypeName = sample.SystemType.FullName,
                    Phase = sample.Phase.ToString(),
                    ExecutionTimeMs = sample.ExecutionTimeMs,
                    Timestamp = sample.Timestamp
                });
            }

            foreach (var metrics in GetAllMetrics())
            {
                snapshot.Metrics.Add(new SystemMetricsData
                {
                    SystemTypeName = metrics.SystemType.FullName,
                    Phase = metrics.Phase.ToString(),
                    AverageMs = metrics.AverageMs,
                    MinMs = metrics.MinMs,
                    MaxMs = metrics.MaxMs,
                    StandardDeviation = metrics.StandardDeviation,
                    SampleCount = metrics.SampleCount
                });
            }

            snapshot.Metadata["BufferSize"] = _bufferSize;
            snapshot.Metadata["SystemCount"] = _sampleBuffers.Count;
            snapshot.Metadata["TotalSamples"] = snapshot.Samples.Count;
            
            return snapshot;
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _isRecording = false;
            _sampleBuffers.Clear();
            _systemPhases.Clear();
            _activeTimers.Clear();
        }
    }
    
    /// <summary>
    /// A simple circular buffer implementation for efficient sample storage.
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;
        
        public int Count => _count;
        public int Capacity => _buffer.Length;
        
        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }
        
        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
        
        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
        
        public List<T> ToList()
        {
            var result = new List<T>(_count);
            if (_count == 0) return result;
            
            int start = (_count < _buffer.Length) ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int index = (start + i) % _buffer.Length;
                result.Add(_buffer[index]);
            }
            return result;
        }
    }
}
