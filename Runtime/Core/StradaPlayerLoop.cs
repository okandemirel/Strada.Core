using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Strada.Core.Core
{
    public static class StradaPlayerLoop
    {
        private struct StradaInitialization { }
        private struct StradaUpdate { }
        private struct StradaLateUpdate { }
        private struct StradaFixedUpdate { }

        private static readonly List<Action<float>> _updateCallbacks = new(16);
        private static readonly List<Action<float>> _lateUpdateCallbacks = new(8);
        private static readonly List<Action<float>> _fixedUpdateCallbacks = new(8);
        private static readonly List<Action> _initCallbacks = new(8);

        private static bool _initialized;
        private static bool _disposed;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _disposed = false;

            var loop = PlayerLoop.GetCurrentPlayerLoop();
            InsertStradaSystems(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
        }

        public static void Shutdown()
        {
            if (_disposed) return;
            _disposed = true;
            _initialized = false;

            _updateCallbacks.Clear();
            _lateUpdateCallbacks.Clear();
            _fixedUpdateCallbacks.Clear();
            _initCallbacks.Clear();

            var defaultLoop = PlayerLoop.GetDefaultPlayerLoop();
            PlayerLoop.SetPlayerLoop(defaultLoop);
        }

        public static void RegisterUpdate(Action<float> callback)
        {
            if (callback != null && !_updateCallbacks.Contains(callback))
                _updateCallbacks.Add(callback);
        }

        public static void RegisterLateUpdate(Action<float> callback)
        {
            if (callback != null && !_lateUpdateCallbacks.Contains(callback))
                _lateUpdateCallbacks.Add(callback);
        }

        public static void RegisterFixedUpdate(Action<float> callback)
        {
            if (callback != null && !_fixedUpdateCallbacks.Contains(callback))
                _fixedUpdateCallbacks.Add(callback);
        }

        public static void RegisterInitialization(Action callback)
        {
            if (callback != null && !_initCallbacks.Contains(callback))
                _initCallbacks.Add(callback);
        }

        public static void UnregisterUpdate(Action<float> callback)
        {
            _updateCallbacks.Remove(callback);
        }

        public static void UnregisterLateUpdate(Action<float> callback)
        {
            _lateUpdateCallbacks.Remove(callback);
        }

        public static void UnregisterFixedUpdate(Action<float> callback)
        {
            _fixedUpdateCallbacks.Remove(callback);
        }

        public static void UnregisterInitialization(Action callback)
        {
            _initCallbacks.Remove(callback);
        }

        private static void InsertStradaSystems(ref PlayerLoopSystem loop)
        {
            var subSystems = loop.subSystemList;
            if (subSystems == null) return;

            for (int i = 0; i < subSystems.Length; i++)
            {
                var subsystem = subSystems[i];

                if (subsystem.type == typeof(Initialization))
                {
                    InsertSystem<StradaInitialization>(ref subSystems[i], RunInitialization);
                }
                else if (subsystem.type == typeof(Update))
                {
                    InsertSystem<StradaUpdate>(ref subSystems[i], RunUpdate);
                }
                else if (subsystem.type == typeof(PreLateUpdate))
                {
                    InsertSystem<StradaLateUpdate>(ref subSystems[i], RunLateUpdate);
                }
                else if (subsystem.type == typeof(FixedUpdate))
                {
                    InsertSystem<StradaFixedUpdate>(ref subSystems[i], RunFixedUpdate);
                }
            }

            loop.subSystemList = subSystems;
        }

        private static void InsertSystem<T>(ref PlayerLoopSystem parent, PlayerLoopSystem.UpdateFunction callback)
        {
            var stradaSystem = new PlayerLoopSystem
            {
                type = typeof(T),
                updateDelegate = callback
            };

            var existing = parent.subSystemList ?? Array.Empty<PlayerLoopSystem>();
            var newSystems = new PlayerLoopSystem[existing.Length + 1];
            newSystems[0] = stradaSystem;
            Array.Copy(existing, 0, newSystems, 1, existing.Length);
            parent.subSystemList = newSystems;
        }

        private static void RunInitialization()
        {
            for (int i = 0; i < _initCallbacks.Count; i++)
                _initCallbacks[i]?.Invoke();
        }

        private static void RunUpdate()
        {
            if (_disposed) return;
            var dt = Time.deltaTime;
            for (int i = 0; i < _updateCallbacks.Count; i++)
                _updateCallbacks[i]?.Invoke(dt);
        }

        private static void RunLateUpdate()
        {
            if (_disposed) return;
            var dt = Time.deltaTime;
            for (int i = 0; i < _lateUpdateCallbacks.Count; i++)
                _lateUpdateCallbacks[i]?.Invoke(dt);
        }

        private static void RunFixedUpdate()
        {
            if (_disposed) return;
            var dt = Time.fixedDeltaTime;
            for (int i = 0; i < _fixedUpdateCallbacks.Count; i++)
                _fixedUpdateCallbacks[i]?.Invoke(dt);
        }
    }
}
