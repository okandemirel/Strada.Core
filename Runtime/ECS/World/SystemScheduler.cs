using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.ECS
{
    public sealed class SystemScheduler : IDisposable
    {
        private readonly List<ISystem>[] _systemsByPhase;
        private readonly List<ISystem> _allSystems;
        private bool _initialized;

        public SystemScheduler()
        {
            int phaseCount = Enum.GetValues(typeof(UpdatePhase)).Length;
            _systemsByPhase = new List<ISystem>[phaseCount];
            for (int i = 0; i < phaseCount; i++)
                _systemsByPhase[i] = new List<ISystem>(8);
            _allSystems = new List<ISystem>(16);
        }

        public void AddSystem(ISystem system, UpdatePhase phase = UpdatePhase.Update)
        {
            _systemsByPhase[(int)phase].Add(system);
            _allSystems.Add(system);
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var initSystems = _systemsByPhase[(int)UpdatePhase.Initialization];
            for (int i = 0; i < initSystems.Count; i++)
                initSystems[i].Initialize();

            for (int phase = 1; phase < _systemsByPhase.Length; phase++)
            {
                var systems = _systemsByPhase[phase];
                for (int i = 0; i < systems.Count; i++)
                    systems[i].Initialize();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.Update];
            for (int i = 0; i < systems.Count; i++)
                systems[i].Update(deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LateUpdate(float deltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.LateUpdate];
            for (int i = 0; i < systems.Count; i++)
                systems[i].Update(deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixedUpdate(float fixedDeltaTime)
        {
            var systems = _systemsByPhase[(int)UpdatePhase.FixedUpdate];
            for (int i = 0; i < systems.Count; i++)
                systems[i].Update(fixedDeltaTime);
        }

        public void Dispose()
        {
            for (int i = _allSystems.Count - 1; i >= 0; i--)
                _allSystems[i].Dispose();

            _allSystems.Clear();
            for (int i = 0; i < _systemsByPhase.Length; i++)
                _systemsByPhase[i].Clear();
        }
    }
}
