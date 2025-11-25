using System;
using System.Collections.Generic;
using Strada.Core.Signals;

namespace Strada.Core.ECS
{
    public sealed class WorldBuilder
    {
        private readonly List<(Type systemType, UpdatePhase phase, Func<World, ISystem> factory)> _systemFactories = new();
        private int _initialEntityCapacity = 1024;

        public WorldBuilder WithInitialEntityCapacity(int capacity)
        {
            _initialEntityCapacity = capacity;
            return this;
        }

        public WorldBuilder WithSystem<T>(UpdatePhase phase = UpdatePhase.Update) where T : ISystem, new()
        {
            _systemFactories.Add((typeof(T), phase, _ => new T()));
            return this;
        }

        public WorldBuilder WithSystem<T>(Func<World, T> factory, UpdatePhase phase = UpdatePhase.Update) where T : ISystem
        {
            _systemFactories.Add((typeof(T), phase, w => factory(w)));
            return this;
        }

        public World Build()
        {
            var entities = new EntityManager();
            var scheduler = new SystemScheduler();
            var signals = new SignalBus();

            var world = new World(entities, scheduler, signals);

            foreach (var (_, phase, factory) in _systemFactories)
            {
                var system = factory(world);
                scheduler.AddSystem(system, phase);
            }

            return world;
        }
    }
}
