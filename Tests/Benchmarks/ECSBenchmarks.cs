using System;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Benchmarks
{
    public class ECSBenchmarks
    {
        private World _world;

        [SetUp]
        public void Setup()
        {
            _world = new ECSBuilder().Build();
        }

        [TearDown]
        public void Teardown()
        {
            _world?.Dispose();
        }

        [Test, Performance]
        public void CreateEntity_Benchmark()
        {
            Measure.Method(() =>
            {
                _world.EntityManager.CreateEntity();
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        [Test, Performance]
        public void AddComponent_Benchmark()
        {
            var entity = _world.EntityManager.CreateEntity();
            
            Measure.Method(() =>
            {
                _world.EntityManager.AddComponent(entity, new TestComponent { Value = 1 });
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1000)
            .Run();
        }

        [Test, Performance]
        public void Iteration_10k_Entities_Benchmark()
        {
            int count = 10000;
            for (int i = 0; i < count; i++)
            {
                var e = _world.EntityManager.CreateEntity();
                _world.EntityManager.AddComponent(e, new TestComponent { Value = i });
            }

            Measure.Method(() =>
            {
                foreach (var index in _world.EntityManager.GetAllEntities())
                {
                    var entity = _world.EntityManager.GetEntity(index);
                    if (_world.EntityManager.HasComponent<TestComponent>(entity))
                    {
                        var cmp = _world.EntityManager.GetComponent<TestComponent>(entity);
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();
        }

        private struct TestComponent : IComponent
        {
            public int Value;
        }
    }
}
