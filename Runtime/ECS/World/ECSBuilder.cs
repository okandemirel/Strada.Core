using System;
using System.Collections.Generic;
using Strada.Core.Communication;
using Strada.Core.ECS.Core;

namespace Strada.Core.ECS.World
{
    public sealed class ECSBuilder
    {
        private readonly List<(Type systemType, UpdatePhase phase, Func<World, ISystem> factory)> _systemFactories = new();
        private int _initialEntityCapacity = 1024;
        private EventBus _eventBus;

        public ECSBuilder WithInitialEntityCapacity(int capacity)
        {
            _initialEntityCapacity = capacity;
            return this;
        }

        public ECSBuilder WithEventBus(EventBus eventBus)
        {
            _eventBus = eventBus;
            return this;
        }

        public ECSBuilder WithSystem<T>(UpdatePhase phase = UpdatePhase.Update) where T : ISystem, new()
        {
            _systemFactories.Add((typeof(T), phase, _ => new T()));
            return this;
        }

        public ECSBuilder WithSystem<T>(Func<World, T> factory, UpdatePhase phase = UpdatePhase.Update) where T : ISystem
        {
            _systemFactories.Add((typeof(T), phase, w => factory(w)));
            return this;
        }

        public World Build()
        {
            var entities = new EntityManager();
            var scheduler = new SystemScheduler();
            var bus = _eventBus ?? new EventBus();

            var world = new World(entities, scheduler, bus);

            foreach (var (_, phase, factory) in _systemFactories)
            {
                var system = factory(world);
                scheduler.AddSystem(system, phase);
            }

            return world;
        }
    }
}
