using System;
using System.Collections.Generic;
using System.Diagnostics;
using Strada.Core.ECS;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor;
using UnityEngine;
using UpdatePhase = Strada.Core.Editor.DataProviders.Models.UpdatePhase;

namespace Strada.Core.Editor.Profiling
{
    /// <summary>
    /// Hooks into the SystemScheduler to capture timing data for each system execution.
    /// Uses EditorApplication.update to sample system timings during Play Mode.
    /// </summary>
    public static class SystemProfilerHook
    {
        private static SystemProfiler _activeProfiler;
        private static readonly Dictionary<Type, Stopwatch> _systemTimers = new Dictionary<Type, Stopwatch>();
        private static bool _isHooked;
        
        /// <summary>
        /// Gets or sets the active profiler instance.
        /// </summary>
        public static SystemProfiler ActiveProfiler
        {
            get => _activeProfiler;
            set
            {
                _activeProfiler = value;
                if (value != null && !_isHooked)
                {
                    Hook();
                }
                else if (value == null && _isHooked)
                {
                    Unhook();
                }
            }
        }
        
        /// <summary>
        /// Hooks into the editor update loop to capture system timings.
        /// </summary>
        public static void Hook()
        {
            if (_isHooked) return;
            _isHooked = true;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        /// <summary>
        /// Unhooks from the editor update loop.
        /// </summary>
        public static void Unhook()
        {
            if (!_isHooked) return;
            _isHooked = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _systemTimers.Clear();
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _systemTimers.Clear();
            }
        }
        
        /// <summary>
        /// Captures timing for a system execution.
        /// Call this before and after system.Update() to measure execution time.
        /// </summary>
        public static void BeginSystemTiming(ISystem system)
        {
            if (_activeProfiler == null || !_activeProfiler.IsRecording) return;
            
            var systemType = system.GetType();
            if (!_systemTimers.TryGetValue(systemType, out var timer))
            {
                timer = new Stopwatch();
                _systemTimers[systemType] = timer;
            }
            
            timer.Restart();
        }
        
        /// <summary>
        /// Ends timing for a system execution and records the sample.
        /// </summary>
        public static void EndSystemTiming(ISystem system)
        {
            if (_activeProfiler == null || !_activeProfiler.IsRecording) return;
            
            var systemType = system.GetType();
            if (_systemTimers.TryGetValue(systemType, out var timer))
            {
                timer.Stop();
                _activeProfiler.RecordSample(systemType, timer.Elapsed.TotalMilliseconds);
            }
        }
        
        /// <summary>
        /// Registers all systems from the current world with the profiler.
        /// </summary>
        public static void RegisterSystemsFromWorld()
        {
            if (_activeProfiler == null || World.Current == null) return;
            
            var scheduler = World.Current.SystemScheduler;
            if (scheduler == null) return;
            
            try
            {
                var schedulerType = scheduler.GetType();
                var systemsByPhaseField = schedulerType.GetField("_systemsByPhase", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (systemsByPhaseField != null)
                {
                    var systemsByPhase = systemsByPhaseField.GetValue(scheduler) as System.Collections.IList[];
                    if (systemsByPhase != null)
                    {
                        for (int phaseIndex = 0; phaseIndex < systemsByPhase.Length; phaseIndex++)
                        {
                            var systems = systemsByPhase[phaseIndex];
                            if (systems == null) continue;
                            
                            var phase = ConvertToEditorPhase((Strada.Core.ECS.UpdatePhase)phaseIndex);
                            
                            foreach (var system in systems)
                            {
                                if (system != null)
                                {
                                    _activeProfiler.RegisterSystem(system.GetType(), phase);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SystemProfilerHook] Failed to register systems: {ex.Message}");
            }
        }
        
        private static UpdatePhase ConvertToEditorPhase(Strada.Core.ECS.UpdatePhase runtimePhase)
        {
            return runtimePhase switch
            {
                Strada.Core.ECS.UpdatePhase.Initialization => UpdatePhase.PreUpdate,
                Strada.Core.ECS.UpdatePhase.Update => UpdatePhase.Update,
                Strada.Core.ECS.UpdatePhase.LateUpdate => UpdatePhase.LateUpdate,
                Strada.Core.ECS.UpdatePhase.FixedUpdate => UpdatePhase.FixedUpdate,
                _ => UpdatePhase.Update
            };
        }
    }
}
